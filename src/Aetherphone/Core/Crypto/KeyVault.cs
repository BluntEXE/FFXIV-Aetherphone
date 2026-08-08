using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal enum KeyVaultState
{
    Unavailable = 0,
    Provisioning = 1,
    Unlocked = 2,
    Unsupported = 3,
    Locked = 4,
}

internal enum RecoveryCodeCreationStatus
{
    Failed = 0,
    Created = 1,
    KeyChangedElsewhere = 2,
}

internal readonly record struct RecoveryCodeCreation(RecoveryCodeCreationStatus Status, string? Code)
{
    public static readonly RecoveryCodeCreation Failure = new(RecoveryCodeCreationStatus.Failed, null);

    public static readonly RecoveryCodeCreation KeyChangedElsewhere =
        new(RecoveryCodeCreationStatus.KeyChangedElsewhere, null);

    public static RecoveryCodeCreation Created(string code)
    {
        return new RecoveryCodeCreation(RecoveryCodeCreationStatus.Created, code);
    }
}

internal sealed class KeyVault : IDisposable
{
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly KeysClient client;
    private readonly SemaphoreSlim gate = new(1, 1);
    private EcPrivateKey? privateKey;
    private EcPrivateKey[] recoveredPreviousKeys = Array.Empty<EcPrivateKey>();
    private MyKeysDto? serverBundle;
    private volatile bool refreshing;

    public KeyVault(Configuration configuration, AethernetSession session, KeysClient client)
    {
        this.configuration = configuration;
        this.session = session;
        this.client = client;
        session.Changed += OnSessionChanged;
    }

    private void OnSessionChanged()
    {
        if (session.IsSignedIn)
        {
            return;
        }

        _ = RefreshAsync(CancellationToken.None);
    }

    public KeyVaultState State { get; private set; } = KeyVaultState.Unavailable;

    public bool LocalCacheUnavailable { get; private set; }

    public int KeyVersion => serverBundle?.KeyVersion ?? 0;

    public string? PublicKey => serverBundle?.PublicKey;

    public string? MyUserId => session.CurrentUser?.Id;

    public bool RecoveryConfigured => serverBundle?.PrivateKey is not null;

    public bool IsRefreshing => refreshing;

    public event Action? Changed;

    public event Action? PreviousKeysRestored;

    public async Task RefreshAsync(CancellationToken token)
    {
        if (!session.IsSignedIn)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ClearKey();
                serverBundle = null;
                SetState(KeyVaultState.Unavailable);
            }
            finally
            {
                gate.Release();
            }

            return;
        }

        if (State == KeyVaultState.Unsupported)
        {
            return;
        }

        refreshing = true;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var (bundle, status) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (status == 404)
            {
                serverBundle = null;
                var existingKey = privateKey ?? ImportStoredKeyForCurrentUser();
                if (existingKey is not null)
                {
                    AepLog.Warning(
                        "[Encryption] server reported no key for this account but this device already has one; re-uploading it instead of creating a new key.");
                    await ReuploadExistingIdentityAsync(existingKey, token).ConfigureAwait(false);
                    return;
                }

                await ProvisionAsync(0, token).ConfigureAwait(false);
                return;
            }

            if (bundle is null)
            {
                return;
            }

            serverBundle = bundle;
            if (privateKey is not null
                && string.Equals(CryptoBox.ExportPublicKey(privateKey), bundle.PublicKey, StringComparison.Ordinal))
            {
                EnsureLocalCachePersisted();
                SetState(KeyVaultState.Unlocked);
                return;
            }

            ClearKey();
            if (TryLoadLocalCache(bundle))
            {
                SetState(KeyVaultState.Unlocked);
                return;
            }

            if (MyUserId is null)
            {
                return;
            }

            AepLog.Warning(
                $"[Encryption] no usable local key for this account; locking this device instead of creating a new key (recovery available: {bundle.PrivateKey is not null}).");
            SetState(KeyVaultState.Locked);
        }
        finally
        {
            refreshing = false;
            gate.Release();
        }
    }

    public async Task<bool> ResetAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await ProvisionAsync(serverBundle?.KeyVersion ?? 0, token).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RecoveryCodeCreation> CreateRecoveryCodeAsync(CancellationToken token)
    {
        var rotatedElsewhere = false;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var key = privateKey;
            var bundle = serverBundle;
            if (State != KeyVaultState.Unlocked || key is null || bundle is null)
            {
                return RecoveryCodeCreation.Failure;
            }

            var (fetched, _) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (fetched is null)
            {
                return RecoveryCodeCreation.Failure;
            }

            if (!string.Equals(fetched.PublicKey, CryptoBox.ExportPublicKey(key), StringComparison.Ordinal))
            {
                AepLog.Warning(
                    "[Encryption] the account key was rotated on another device; refusing to overwrite it with this device's key.");
                rotatedElsewhere = true;
                return RecoveryCodeCreation.KeyChangedElsewhere;
            }

            serverBundle = fetched;
            var pkcs8 = CryptoBox.TryExportPrivateKey(key);
            if (pkcs8 is null)
            {
                return RecoveryCodeCreation.Failure;
            }

            var code = RecoveryKey.GenerateCode();
            var escrow = RecoveryKey.Wrap(pkcs8, code);
            CryptographicOperations.ZeroMemory(pkcs8);
            if (escrow is null)
            {
                return RecoveryCodeCreation.Failure;
            }

            var (stored, status) = await client.PutMyKeysAsync(
                new PutMyKeysRequest(fetched.PublicKey, escrow, fetched.KeyVersion), token).ConfigureAwait(false);
            if (status == 409)
            {
                AepLog.Warning(
                    "[Encryption] the account key was rotated on another device while saving the recovery code; keeping the newer key.");
                rotatedElsewhere = true;
                return RecoveryCodeCreation.KeyChangedElsewhere;
            }

            if (stored is null)
            {
                return RecoveryCodeCreation.Failure;
            }

            serverBundle = stored;
            return RecoveryCodeCreation.Created(code);
        }
        finally
        {
            gate.Release();
            if (rotatedElsewhere)
            {
                _ = RefreshAsync(CancellationToken.None);
            }
        }
    }

    public async Task<bool> RecoverWithCodeAsync(string code, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var (bundle, _) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (bundle is null)
            {
                return false;
            }

            serverBundle = bundle;
            if (bundle.PrivateKey is null)
            {
                return false;
            }

            var pkcs8 = RecoveryKey.Unwrap(bundle.PrivateKey, code);
            if (pkcs8 is null)
            {
                return false;
            }

            var imported = CryptoBox.ImportPrivateKey(pkcs8);
            if (imported is null)
            {
                CryptographicOperations.ZeroMemory(pkcs8);
                return false;
            }

            if (!string.Equals(CryptoBox.ExportPublicKey(imported), bundle.PublicKey, StringComparison.Ordinal))
            {
                CryptographicOperations.ZeroMemory(pkcs8);
                return false;
            }

            ClearKey();
            privateKey = imported;
            StoreLocalCache(pkcs8);
            CryptographicOperations.ZeroMemory(pkcs8);
            SetState(KeyVaultState.Unlocked);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public byte[]? UnwrapCek(string wrappedKey)
    {
        return UnwrapCek(wrappedKey, out _);
    }

    public byte[]? UnwrapCek(string wrappedKey, out bool privateKeyWasLoaded)
    {
        var key = privateKey;
        privateKeyWasLoaded = key is not null;
        if (key is not null)
        {
            var unwrapped = CryptoBox.UnwrapCek(wrappedKey, key);
            if (unwrapped is not null)
            {
                return unwrapped;
            }
        }

        var recovered = recoveredPreviousKeys;
        for (var index = 0; index < recovered.Length; index++)
        {
            var unwrapped = CryptoBox.UnwrapCek(wrappedKey, recovered[index]);
            if (unwrapped is not null)
            {
                return unwrapped;
            }
        }

        return null;
    }

    public async Task<bool> HasArchivedEscrowsAsync(CancellationToken token)
    {
        if (!session.IsSignedIn)
        {
            return false;
        }

        var escrows = await client.MyKeyEscrowsAsync(token).ConfigureAwait(false);
        return escrows is not null && escrows.Items.Length > 0;
    }

    public async Task<int> RestorePreviousKeysAsync(string code, CancellationToken token)
    {
        var restoredCount = 0;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (State != KeyVaultState.Unlocked)
            {
                return 0;
            }

            var canonical = RecoveryKey.Canonicalize(code);
            if (canonical.Length == 0)
            {
                return 0;
            }

            var escrows = await client.MyKeyEscrowsAsync(token).ConfigureAwait(false);
            if (escrows is null || escrows.Items.Length == 0)
            {
                return 0;
            }

            var knownPublicKeys = CollectKnownPublicKeys();
            var restored = new List<EcPrivateKey>();
            for (var index = 0; index < escrows.Items.Length; index++)
            {
                var item = escrows.Items[index];
                if (!knownPublicKeys.Add(item.PublicKey))
                {
                    continue;
                }

                var imported = TryRecoverArchivedKey(item, canonical);
                if (imported is null)
                {
                    knownPublicKeys.Remove(item.PublicKey);
                    continue;
                }

                restored.Add(imported);
            }

            if (restored.Count == 0)
            {
                return 0;
            }

            var merged = new EcPrivateKey[recoveredPreviousKeys.Length + restored.Count];
            recoveredPreviousKeys.CopyTo(merged, 0);
            for (var index = 0; index < restored.Count; index++)
            {
                merged[recoveredPreviousKeys.Length + index] = restored[index];
            }

            recoveredPreviousKeys = merged;
            restoredCount = restored.Count;
            return restoredCount;
        }
        finally
        {
            gate.Release();
            if (restoredCount > 0)
            {
                PreviousKeysRestored?.Invoke();
            }
        }
    }

    private HashSet<string> CollectKnownPublicKeys()
    {
        var knownPublicKeys = new HashSet<string>(StringComparer.Ordinal);
        var currentKey = privateKey;
        if (currentKey is not null)
        {
            var currentPublicKey = CryptoBox.TryExportPublicKey(currentKey);
            if (currentPublicKey is not null)
            {
                knownPublicKeys.Add(currentPublicKey);
            }
        }

        for (var index = 0; index < recoveredPreviousKeys.Length; index++)
        {
            var publicKey = CryptoBox.TryExportPublicKey(recoveredPreviousKeys[index]);
            if (publicKey is not null)
            {
                knownPublicKeys.Add(publicKey);
            }
        }

        return knownPublicKeys;
    }

    private static EcPrivateKey? TryRecoverArchivedKey(ArchivedKeyEscrowDto item, string canonicalCode)
    {
        var pkcs8 = RecoveryKey.Unwrap(item.Escrow, canonicalCode);
        if (pkcs8 is null)
        {
            return null;
        }

        var imported = CryptoBox.ImportPrivateKey(pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
        if (imported is null)
        {
            return null;
        }

        return string.Equals(CryptoBox.TryExportPublicKey(imported), item.PublicKey, StringComparison.Ordinal)
            ? imported
            : null;
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        ClearKey();
        gate.Dispose();
    }

    private async Task<bool> ProvisionAsync(int expectedKeyVersion, CancellationToken token)
    {
        if (MyUserId is null)
        {
            AepLog.Warning("[Encryption] skipped creating a key because the account has not resolved yet.");
            return false;
        }

        SetState(KeyVaultState.Provisioning);
        var identity = CryptoBox.TryGenerateIdentity();
        var publicKey = identity is null ? null : CryptoBox.TryExportPublicKey(identity);
        if (identity is null || publicKey is null)
        {
            AepLog.Warning("[Encryption] identity unsupported: this system cannot create an encryption key.");
            SetState(KeyVaultState.Unsupported);
            return false;
        }

        var (stored, status) = await client.PutMyKeysAsync(
            new PutMyKeysRequest(publicKey, null, expectedKeyVersion), token).ConfigureAwait(false);
        if (status == 409)
        {
            AepLog.Warning("[Encryption] the account key changed on another device while creating a new key; keeping the newer key.");
            return false;
        }

        if (stored is null)
        {
            return false;
        }

        serverBundle = stored;
        ClearKey();
        privateKey = identity;
        var pkcs8 = CryptoBox.TryExportPrivateKey(identity);
        if (pkcs8 is not null)
        {
            StoreLocalCache(pkcs8);
            CryptographicOperations.ZeroMemory(pkcs8);
        }

        SetState(KeyVaultState.Unlocked);
        return true;
    }

    private bool TryLoadLocalCache(MyKeysDto bundle)
    {
        var imported = ImportStoredKeyForCurrentUser();
        if (imported is null)
        {
            return false;
        }

        if (!string.Equals(CryptoBox.ExportPublicKey(imported), bundle.PublicKey, StringComparison.Ordinal))
        {
            return false;
        }

        privateKey = imported;
        return true;
    }

    private EcPrivateKey? ImportStoredKeyForCurrentUser()
    {
        var userId = MyUserId;
        if (userId is null
            || configuration.EncryptionKeyCache.Length == 0
            || !string.Equals(configuration.EncryptionKeyCacheUserId, userId, StringComparison.Ordinal))
        {
            return null;
        }

        var pkcs8 = LocalKeyProtector.Unprotect(configuration.EncryptionKeyCache, userId);
        if (pkcs8 is null)
        {
            return null;
        }

        var imported = CryptoBox.ImportPrivateKey(pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
        return imported;
    }

    private async Task ReuploadExistingIdentityAsync(EcPrivateKey existingKey, CancellationToken token)
    {
        var publicKey = CryptoBox.TryExportPublicKey(existingKey);
        if (publicKey is null)
        {
            return;
        }

        var (stored, status) = await client.PutMyKeysAsync(
            new PutMyKeysRequest(publicKey, null, 0), token).ConfigureAwait(false);
        if (status == 409)
        {
            AepLog.Warning("[Encryption] the server reported a missing key but one exists after all; keeping the server's key.");
            return;
        }

        if (stored is null)
        {
            return;
        }

        serverBundle = stored;
        privateKey = existingKey;
        var pkcs8 = CryptoBox.TryExportPrivateKey(existingKey);
        if (pkcs8 is not null)
        {
            StoreLocalCache(pkcs8);
            CryptographicOperations.ZeroMemory(pkcs8);
        }

        SetState(KeyVaultState.Unlocked);
    }

    private void StoreLocalCache(byte[] pkcs8)
    {
        var userId = MyUserId;
        if (userId is null)
        {
            return;
        }

        var protectedBlob = LocalKeyProtector.Protect(pkcs8, userId);
        LocalCacheUnavailable = LocalKeyProtector.IsUnprotected(protectedBlob);
        configuration.EncryptionKeyCache = protectedBlob;
        configuration.EncryptionKeyCacheUserId = userId;
        configuration.Save();
        session.PersistActiveKeyCache();
    }

    private void EnsureLocalCachePersisted()
    {
        var userId = MyUserId;
        var key = privateKey;
        if (userId is null || key is null)
        {
            return;
        }

        var currentPublicKey = CryptoBox.TryExportPublicKey(key);
        if (currentPublicKey is null)
        {
            return;
        }

        var stored = ImportStoredKeyForCurrentUser();
        if (stored is not null
            && string.Equals(CryptoBox.ExportPublicKey(stored), currentPublicKey, StringComparison.Ordinal))
        {
            return;
        }

        var pkcs8 = CryptoBox.TryExportPrivateKey(key);
        if (pkcs8 is null)
        {
            return;
        }

        StoreLocalCache(pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
    }

    private void ClearKey()
    {
        privateKey = null;
        recoveredPreviousKeys = Array.Empty<EcPrivateKey>();
    }

    private void SetState(KeyVaultState next)
    {
        if (State == next)
        {
            return;
        }

        State = next;
        Changed?.Invoke();
    }
}

internal static class LocalKeyProtector
{
    private const string RawPrefix = "raw.";

    public static string Protect(byte[] secret, string userId)
    {
        try
        {
            var entropy = Encoding.UTF8.GetBytes(userId);
            var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(secret, entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Key protection unavailable ({exception.GetType().Name}); storing the key with basic protection instead.");
            return RawPrefix + Convert.ToBase64String(secret);
        }
    }

    public static bool IsUnprotected(string stored)
    {
        return stored.StartsWith(RawPrefix, StringComparison.Ordinal);
    }

    public static byte[]? Unprotect(string stored, string userId)
    {
        if (stored.StartsWith(RawPrefix, StringComparison.Ordinal))
        {
            try
            {
                return Convert.FromBase64String(stored[RawPrefix.Length..]);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(stored);
            var entropy = Encoding.UTF8.GetBytes(userId);
            return System.Security.Cryptography.ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
