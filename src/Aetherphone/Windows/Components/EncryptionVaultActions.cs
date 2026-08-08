using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;

namespace Aetherphone.Windows.Components;

internal sealed class EncryptionVaultActions : IDisposable
{
    private readonly KeyVault vault;
    private readonly ConfirmService confirm;
    private readonly CancellationTokenSource cancellation = new();
    private volatile string status = string.Empty;
    private volatile bool busy;
    private volatile string generatedCode = string.Empty;
    private volatile bool hasArchivedEscrows;

    public string CodeEntry = string.Empty;

    public EncryptionVaultActions(KeyVault vault, ConfirmService confirm)
    {
        this.vault = vault;
        this.confirm = confirm;
    }

    public KeyVault Vault => vault;

    public bool Busy => busy;

    public string GeneratedCode => generatedCode;

    public bool HasArchivedEscrows => hasArchivedEscrows;

    public string Status
    {
        get => status;
        set => status = value;
    }

    public void AcknowledgeGeneratedCode()
    {
        generatedCode = string.Empty;
        status = string.Empty;
    }

    public void AskReset()
    {
        AskReset(Loc.T(L.Encryption.ForgotBody));
    }

    public void AskResetWithoutRecovery()
    {
        AskReset(Loc.T(L.Encryption.ForgotNoRecoveryBody));
    }

    private void AskReset(string message)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = message,
            ConfirmLabel = Loc.T(L.Encryption.ForgotConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done =>
            {
                Reset();
                done(true);
            },
        });
    }

    public void BeginCreateRecoveryCode()
    {
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var creation = await vault.CreateRecoveryCodeAsync(cancellation.Token).ConfigureAwait(false);
                if (creation.Status == RecoveryCodeCreationStatus.Created && creation.Code is not null)
                {
                    generatedCode = creation.Code;
                    status = string.Empty;
                }
                else if (creation.Status == RecoveryCodeCreationStatus.KeyChangedElsewhere)
                {
                    status = Loc.T(L.Encryption.RecoveryKeyChanged);
                }
                else
                {
                    status = Loc.T(L.Encryption.Failed);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Recovery code setup failed");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    public void BeginRecover()
    {
        var code = CodeEntry;
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var recovered = await vault.RecoverWithCodeAsync(code, cancellation.Token).ConfigureAwait(false);
                if (recovered)
                {
                    CodeEntry = string.Empty;
                    status = string.Empty;
                    await RestorePreviousKeysSilentlyAsync(code).ConfigureAwait(false);
                }
                else
                {
                    status = Loc.T(L.Encryption.RecoveryWrongCode);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Recovery failed");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    private async Task RestorePreviousKeysSilentlyAsync(string code)
    {
        try
        {
            await vault.RestorePreviousKeysAsync(code, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Previous key restore failed");
        }
    }

    public void RefreshArchivedEscrows()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                hasArchivedEscrows = await vault.HasArchivedEscrowsAsync(cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Archived escrow lookup failed");
            }
        });
    }

    public void BeginRestorePreviousKeys()
    {
        var code = CodeEntry;
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var restored = await vault.RestorePreviousKeysAsync(code, cancellation.Token).ConfigureAwait(false);
                if (restored > 0)
                {
                    CodeEntry = string.Empty;
                    status = Loc.T(L.Encryption.RestoreOlderDone, restored);
                }
                else
                {
                    status = Loc.T(L.Encryption.RestoreOlderNoMatch);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Previous key restore failed");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    private void Reset()
    {
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var succeeded = await vault.ResetAsync(cancellation.Token).ConfigureAwait(false);
                status = succeeded ? string.Empty : Loc.T(L.Encryption.Failed);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Encryption reset failed");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
