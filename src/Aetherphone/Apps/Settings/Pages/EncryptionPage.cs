using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class EncryptionPage : ISettingsPage, IDisposable
{
    public string Title => Loc.T(L.Encryption.Title);

    public string Summary => vault.State switch
    {
        KeyVaultState.Unlocked when vault.UnsavedRecoveryCode is not null =>
            Loc.T(L.Encryption.SummaryUnsavedCode),
        KeyVaultState.Unlocked when !vault.RecoveryConfigured => Loc.T(L.Encryption.SummaryNoRecovery),
        KeyVaultState.Unlocked => Loc.T(L.Encryption.StateActive),
        KeyVaultState.Provisioning => Loc.T(L.Encryption.StateSettingUp),
        KeyVaultState.Locked => Loc.T(L.Encryption.StateLocked),
        KeyVaultState.Unsupported => Loc.T(L.Encryption.StateUnsupported),
        _ => Loc.T(L.Encryption.StateUnavailable),
    };

    public FontAwesomeIcon Icon => FontAwesomeIcon.Lock;
    public Vector4 Tint => new(0.38f, 0.66f, 0.42f, 1f);

    private readonly AethernetSession session;
    private readonly KeyVault vault;
    private readonly EncryptionVaultActions actions;
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool refreshRequested;
    private int lastDrawnFrame;
    private bool restoreEntryOpen;

    public EncryptionPage(AethernetSession session, KeyVault vault, ConfirmService confirm)
    {
        this.session = session;
        this.vault = vault;
        actions = new EncryptionVaultActions(vault, confirm);
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var frame = ImGui.GetFrameCount();
        if (frame - lastDrawnFrame > 1)
        {
            restoreEntryOpen = false;
            actions.RefreshArchivedEscrows();
        }

        lastDrawnFrame = frame;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            EnsureRefreshed();
            if (actions.GeneratedCode.Length > 0)
            {
                DrawGeneratedCode(theme);
            }
            else
            {
                switch (vault.State)
                {
                    case KeyVaultState.Unavailable:
                        DrawUnavailable(theme);
                        break;
                    case KeyVaultState.Provisioning:
                        DrawProvisioning(theme);
                        break;
                    case KeyVaultState.Unsupported:
                        DrawUnsupported(theme);
                        break;
                    case KeyVaultState.Locked:
                        DrawLocked(theme);
                        break;
                    default:
                        DrawActive(theme);
                        break;
                }
            }

            DrawStatus(theme);
        }
    }

    private void EnsureRefreshed()
    {
        if (refreshRequested || !session.IsSignedIn || session.CurrentUser is null || vault.IsRefreshing)
        {
            if (!session.IsSignedIn)
            {
                refreshRequested = false;
            }

            return;
        }

        refreshRequested = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await vault.RefreshAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Encryption key refresh failed");
            }
        });
    }

    private void DrawUnavailable(PhoneTheme theme)
    {
        ImGui.Dummy(new Vector2(0f, 8f * UiScale.Current));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.NotSignedIn));
        }
    }

    private void DrawUnsupported(PhoneTheme theme)
    {
        ImGui.Dummy(new Vector2(0f, 8f * UiScale.Current));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.UnsupportedBody));
        }
    }

    private void DrawProvisioning(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.Intro));
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            Typography.Wrapped(Loc.T(L.Encryption.SettingUp));
        }
    }

    private void DrawLocked(PhoneTheme theme)
    {
        if (vault.RecoveryConfigured)
        {
            DrawLockedRecover(theme);
            return;
        }

        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(vault.LocalKeyUnreadable
                ? Loc.T(L.Encryption.UnreadableKeyBody)
                : Loc.T(L.Encryption.LockedNoRecoveryBody));
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        if (ThemeButton.Draw(Loc.T(L.Encryption.NewKeyButton), theme) && !actions.Busy)
        {
            actions.AskResetWithoutRecovery();
        }
    }

    private void DrawLockedRecover(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.LockedRecoverBody));
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        DrawCodeInput(theme);
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (ThemeButton.Draw(Loc.T(L.Encryption.RecoveryUnlockButton), theme)
            && !actions.Busy && RecoveryKey.Canonicalize(actions.CodeEntry).Length > 0)
        {
            actions.BeginRecover();
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        if (ThemeButton.Draw(Loc.T(L.Encryption.NewKeyButton), theme) && !actions.Busy)
        {
            actions.AskReset();
        }
    }

    private void DrawCodeInput(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Plain(Loc.T(L.Encryption.RecoveryCodeLabel));
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
            ImGui.GetColorU32(theme.GroupedCard));
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 12f * scale,
            origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)).Push(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputText("##recoveryCode", ref actions.CodeEntry, 64);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawGeneratedCode(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            Typography.Wrapped(Loc.T(L.Encryption.RecoverySaveTitle));
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 46f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 10f * scale,
            ImGui.GetColorU32(theme.GroupedCard));
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f), actions.GeneratedCode,
            theme.TextStrong, 1.15f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (ThemeButton.Draw(Loc.T(L.Encryption.RecoveryCopy), theme))
        {
            ImGui.SetClipboardText(actions.GeneratedCode);
            ShellToast.Show();
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.RecoverySaveBody));
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        if (ThemeButton.Draw(Loc.T(L.Encryption.RecoverySavedButton), theme))
        {
            actions.AcknowledgeGeneratedCode();
        }
    }

    private void DrawRecoverySection(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var unsaved = vault.UnsavedRecoveryCode;
        if (unsaved is not null)
        {
            DrawUnsavedCodeSection(theme, unsaved, scale);
            return;
        }

        ImGui.Dummy(new Vector2(0f, 14f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            Typography.Plain(Loc.T(L.Encryption.RecoverySectionTitle));
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(vault.RecoveryConfigured
                ? Loc.T(L.Encryption.RecoveryConfiguredBody)
                : Loc.T(L.Encryption.RecoveryNotSetBody));
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var label = vault.RecoveryConfigured
            ? Loc.T(L.Encryption.RecoveryRegenerateButton)
            : Loc.T(L.Encryption.RecoverySetupButton);
        if (ThemeButton.Draw(label, theme) && !actions.Busy)
        {
            actions.BeginCreateRecoveryCode();
        }
    }

    private void DrawActive(PhoneTheme theme)
    {
        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.Intro));
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            Typography.Wrapped(Loc.T(L.Encryption.ActiveHint));
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            Typography.Wrapped(Loc.T(L.Encryption.NewDeviceHint));
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            Typography.Plain(Loc.T(L.Encryption.KeyVersion, vault.KeyVersion));
        }

        if (vault.LocalCacheUnavailable)
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                Typography.Wrapped(Loc.T(L.Encryption.LocalStoreUnavailable));
            }
        }

        DrawRecoverySection(theme);
        DrawRestoreOlderSection(theme);

        ImGui.Dummy(new Vector2(0f, 14f * scale));
        if (ThemeButton.Draw(Loc.T(L.Encryption.ResetButton), theme) && !actions.Busy)
        {
            actions.AskReset();
        }
    }

    private void DrawUnsavedCodeSection(PhoneTheme theme, string code, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        if (actions.VerifyingSavedCode)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                Typography.Plain(Loc.T(L.Encryption.GuideVerifyTitle));
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                Typography.Wrapped(Loc.T(L.Encryption.GuideVerifyBody, actions.ExpectedVerifyGroup ?? string.Empty));
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##encryptionVerifySettings", ref actions.VerifyEntry, 64);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            if (ThemeButton.Draw(Loc.T(L.Encryption.GuideVerifyConfirm), theme))
            {
                actions.TryConfirmSavedCode();
            }

            ImGui.Dummy(new Vector2(0f, 6f * scale));
            if (ThemeButton.Draw(Loc.T(L.Encryption.GuideShowAgain), theme))
            {
                actions.VerifyingSavedCode = false;
                actions.Status = string.Empty;
            }

            return;
        }

        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            Typography.Plain(Loc.T(L.Encryption.RecoverySaveTitle));
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.SaveCodeIntro));
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            Typography.Plain(code);
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (ThemeButton.Draw(Loc.T(L.Encryption.RecoveryCopy), theme))
        {
            ImGui.SetClipboardText(code);
            ShellToast.Show();
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        if (ThemeButton.Draw(Loc.T(L.Encryption.GuideWroteItDown), theme))
        {
            actions.VerifyingSavedCode = true;
            actions.Status = string.Empty;
        }
    }

    private void DrawRestoreOlderSection(PhoneTheme theme)
    {
        var olderKeysHeldHere = actions.Vault.OlderKeysHeldHere;
        if (!actions.HasArchivedEscrows && olderKeysHeldHere == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            Typography.Plain(Loc.T(L.Encryption.RestoreOlderTitle));
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(Loc.T(L.Encryption.RestoreOlderBody));
            if (olderKeysHeldHere > 0)
            {
                Typography.Wrapped(Loc.T(L.Encryption.OlderKeysHeldHere, olderKeysHeldHere));
            }
        }

        if (!actions.HasArchivedEscrows)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (!restoreEntryOpen)
        {
            if (ThemeButton.Draw(Loc.T(L.Encryption.RestoreOlderButton), theme) && !actions.Busy)
            {
                actions.CodeEntry = string.Empty;
                restoreEntryOpen = true;
            }

            return;
        }

        DrawCodeInput(theme);
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (ThemeButton.Draw(Loc.T(L.Encryption.RestoreOlderConfirm), theme)
            && !actions.Busy && RecoveryKey.Canonicalize(actions.CodeEntry).Length > 0)
        {
            actions.BeginRestorePreviousKeys();
        }
    }

    private void DrawStatus(PhoneTheme theme)
    {
        var message = actions.Status;
        if (message.Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, 8f * UiScale.Current));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Wrapped(message);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
        actions.Dispose();
    }
}
