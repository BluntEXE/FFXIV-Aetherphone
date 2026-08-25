using System.Text.Json;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Mods;

internal enum ModsLibraryState : byte
{
    Idle,
    Scanning,
    Ready,
    NoPenumbra,
    Failed,
}

internal sealed class InstalledMod
{
    public required string Directory { get; init; }
    public required string FullPath { get; init; }
    public required Guid PackageId { get; init; }
    public required Guid VariantId { get; init; }
    public required Guid VersionId { get; init; }
    public required string Name { get; init; }
    public required string Variant { get; init; }
    public required string Version { get; init; }
    public required string Author { get; init; }
    public required string Tagline { get; init; }
    public required string CoverPath { get; init; }
    public required string TitleId { get; init; }
    public required string ToggleId { get; init; }
    public required string UpdateId { get; init; }
    public required string ByLine { get; init; }
    public required string Subtitle { get; init; }
    public bool? Enabled { get; set; }
    public Guid NewestVersionId { get; set; }
    public string NewestVersion { get; set; } = string.Empty;
    public string NewestChangelog { get; set; } = string.Empty;
    public string UpdateLine { get; set; } = string.Empty;

    public string CoverKey => "modcover:" + Directory;
    public bool HasUpdate => NewestVersionId != Guid.Empty && NewestVersionId != VersionId;
    public bool HasCover => CoverPath.Length > 0;

    private Func<CancellationToken, Task<byte[]?>>? coverLoader;

    public Func<CancellationToken, Task<byte[]?>> CoverLoader => coverLoader ??= ReadCoverAsync;

    private async Task<byte[]?> ReadCoverAsync(CancellationToken token)
    {
        try
        {
            return await File.ReadAllBytesAsync(CoverPath, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AepLog.Debug(exception, $"[Mods] cover unreadable for {Directory}");
            return null;
        }
    }
}

internal sealed class ModsLibraryStore : IDisposable
{
    private const int UpdateBatch = 50;
    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan RetryWithoutPenumbra = TimeSpan.FromSeconds(5);

    private readonly HeliosphereApi api;
    private readonly CancellationTokenSource lifetime = new();
    private readonly PenumbraModEvents events;
    private int scanning;
    private volatile bool stale = true;
    private volatile InstalledMod[] installed = Array.Empty<InstalledMod>();
    private volatile ModsLibraryState state = ModsLibraryState.Idle;
    private volatile bool checkingUpdates;
    private volatile int updateCount;
    private volatile int version;
    private DateTime lastScanUtc;
    private DateTime nextAttemptUtc;
    private Guid collectionId;
    private string collectionName = string.Empty;
    private string modRoot = string.Empty;

    public ModsLibraryStore(HeliosphereApi api)
    {
        this.api = api;
        events = PenumbraBridge.SubscribeModChanges(Invalidate);
    }

    public InstalledMod[] Installed => installed;
    public ModsLibraryState State => state;
    public bool CheckingUpdates => checkingUpdates;
    public int UpdateCount => updateCount;
    public int Version => version;
    public string CollectionName => collectionName;
    public string ModRoot => modRoot;
    public bool Scanning => Volatile.Read(ref scanning) == 1;

    public void Invalidate() => stale = true;

    public void Refresh(bool force)
    {
        if (Volatile.Read(ref scanning) == 1)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!force && now < nextAttemptUtc)
        {
            return;
        }

        if (!force && !stale && now - lastScanUtc < FreshFor)
        {
            return;
        }

        if (!PenumbraBridge.TryGetModDirectory(out var root))
        {
            nextAttemptUtc = now + RetryWithoutPenumbra;
            state = ModsLibraryState.NoPenumbra;
            installed = Array.Empty<InstalledMod>();
            stale = true;
            version++;
            return;
        }

        if (Interlocked.CompareExchange(ref scanning, 1, 0) != 0)
        {
            return;
        }

        stale = false;
        modRoot = root;
        if (PenumbraBridge.TryGetPlayerCollection(out var id, out var name))
        {
            collectionId = id;
            collectionName = name;
        }

        if (state != ModsLibraryState.Ready)
        {
            state = ModsLibraryState.Scanning;
        }

        var previous = installed;
        var collection = collectionId;
        _ = Task.Run(() => ScanAsync(root, collection, previous, lifetime.Token), lifetime.Token);
    }

    public bool TryFindVariant(Guid variantId, out InstalledMod mod)
    {
        var current = installed;
        for (var index = 0; index < current.Length; index++)
        {
            if (current[index].VariantId == variantId)
            {
                mod = current[index];
                return true;
            }
        }

        mod = null!;
        return false;
    }

    public bool TryFindPackage(Guid packageId, out InstalledMod mod)
    {
        var current = installed;
        for (var index = 0; index < current.Length; index++)
        {
            if (current[index].PackageId == packageId)
            {
                mod = current[index];
                return true;
            }
        }

        mod = null!;
        return false;
    }

    public bool SetEnabled(InstalledMod mod, bool enabled)
    {
        if (collectionId == Guid.Empty || !PenumbraBridge.SetModEnabled(collectionId, mod.Directory, enabled))
        {
            return false;
        }

        mod.Enabled = enabled;
        version++;
        return true;
    }

    public bool OpenInPenumbra(InstalledMod mod) => PenumbraBridge.OpenMod(mod.Directory);

    private async Task ScanAsync(string root, Guid collection, InstalledMod[] previous, CancellationToken token)
    {
        try
        {
            var mods = ReadLibrary(root, previous);
            token.ThrowIfCancellationRequested();
            await Plugin.Framework.RunOnFrameworkThread(() => ReadEnabledFlags(mods, collection)).ConfigureAwait(false);
            installed = mods;
            state = ModsLibraryState.Ready;
            lastScanUtc = DateTime.UtcNow;
            RecountUpdates();
            version++;
            await CheckUpdatesAsync(mods, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Mods] library scan failed");
            state = installed.Length > 0 ? ModsLibraryState.Ready : ModsLibraryState.Failed;
            version++;
        }
        finally
        {
            Interlocked.Exchange(ref scanning, 0);
        }
    }

    private static InstalledMod[] ReadLibrary(string root, InstalledMod[] previous)
    {
        if (!Directory.Exists(root))
        {
            return Array.Empty<InstalledMod>();
        }

        var mods = new List<InstalledMod>();
        foreach (var directory in Directory.EnumerateDirectories(root, ModsContent.InstalledDirectoryPrefix + "*"))
        {
            var metaPath = Path.Combine(directory, ModsContent.MetaFileName);
            if (!File.Exists(metaPath))
            {
                continue;
            }

            HeliosphereMeta? meta;
            try
            {
                meta = JsonSerializer.Deserialize(File.ReadAllBytes(metaPath), HeliosphereJsonContext.Default.HeliosphereMeta);
            }
            catch (Exception exception)
            {
                AepLog.Debug(exception, $"[Mods] unreadable {ModsContent.MetaFileName} in {directory}");
                continue;
            }

            if (meta is null || meta.Id == Guid.Empty || meta.VariantId == Guid.Empty)
            {
                continue;
            }

            var coverPath = Path.Combine(directory, ModsContent.CoverFileName);
            var directoryName = Path.GetFileName(directory);
            var mod = new InstalledMod
            {
                Directory = directoryName,
                FullPath = directory,
                PackageId = meta.Id,
                VariantId = meta.VariantId,
                VersionId = meta.VersionId,
                Name = meta.Name,
                Variant = meta.Variant,
                Version = meta.Version,
                Author = meta.Author,
                Tagline = meta.Tagline,
                CoverPath = File.Exists(coverPath) ? coverPath : string.Empty,
                TitleId = "mods.installed.title." + directoryName,
                ToggleId = "mods.toggle." + directoryName,
                UpdateId = "mods.update." + directoryName,
                ByLine = meta.Author.Length == 0 ? string.Empty : Loc.T(L.Mods.By, meta.Author),
                Subtitle = SubtitleFor(meta),
            };
            CarryNewest(mod, previous);
            mods.Add(mod);
        }

        mods.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return mods.ToArray();
    }

    private static string SubtitleFor(HeliosphereMeta meta)
    {
        if (meta.Variant.Length == 0
            || string.Equals(meta.Variant, ModsContent.DefaultVariantName, StringComparison.Ordinal))
        {
            return "v" + meta.Version;
        }

        return meta.Variant + "  v" + meta.Version;
    }

    private static void CarryNewest(InstalledMod mod, InstalledMod[] previous)
    {
        for (var index = 0; index < previous.Length; index++)
        {
            if (previous[index].VariantId != mod.VariantId)
            {
                continue;
            }

            mod.NewestVersionId = previous[index].NewestVersionId;
            mod.NewestVersion = previous[index].NewestVersion;
            mod.NewestChangelog = previous[index].NewestChangelog;
            mod.UpdateLine = previous[index].UpdateLine;
            return;
        }
    }

    private static void ReadEnabledFlags(InstalledMod[] mods, Guid collection)
    {
        if (collection == Guid.Empty)
        {
            return;
        }

        for (var index = 0; index < mods.Length; index++)
        {
            mods[index].Enabled = PenumbraBridge.IsModEnabled(collection, mods[index].Directory);
        }
    }

    private async Task CheckUpdatesAsync(InstalledMod[] mods, CancellationToken token)
    {
        if (mods.Length == 0)
        {
            return;
        }

        checkingUpdates = true;
        version++;
        try
        {
            for (var start = 0; start < mods.Length; start += UpdateBatch)
            {
                var count = Math.Min(UpdateBatch, mods.Length - start);
                var ids = new Guid[count];
                for (var index = 0; index < count; index++)
                {
                    ids[index] = mods[start + index].VariantId;
                }

                var variants = await api.VariantsAsync(ids, token).ConfigureAwait(false);
                if (variants is null)
                {
                    return;
                }

                ApplyNewest(mods, variants);
                RecountUpdates();
                version++;
            }
        }
        finally
        {
            checkingUpdates = false;
            version++;
        }
    }

    private static void ApplyNewest(InstalledMod[] mods, VariantDto[] variants)
    {
        for (var variantIndex = 0; variantIndex < variants.Length; variantIndex++)
        {
            var variant = variants[variantIndex];
            if (variant.Versions.Length == 0)
            {
                continue;
            }

            var newest = variant.Versions[0];
            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                if (mods[modIndex].VariantId != variant.Id)
                {
                    continue;
                }

                mods[modIndex].NewestVersionId = newest.Id;
                mods[modIndex].NewestVersion = newest.Version;
                mods[modIndex].NewestChangelog = newest.Changelog ?? string.Empty;
                mods[modIndex].UpdateLine = Loc.T(L.Mods.VersionChange, mods[modIndex].Version, newest.Version);
            }
        }
    }

    private void RecountUpdates()
    {
        var current = installed;
        var count = 0;
        for (var index = 0; index < current.Length; index++)
        {
            if (current[index].HasUpdate)
            {
                count++;
            }
        }

        updateCount = count;
    }

    public void Dispose()
    {
        events.Dispose();
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
