using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float SettingsSliderRowHeight = 52f;
    private const float SliderLabelWidth = 0.42f;
    private const float PopoutOpacityMinimum = 0.5f;

    private static readonly float[] TextScaleChoices = { 0.8f, 0.9f, 1f, 1.15f, 1.3f, 1.5f };

    private readonly DropdownMenu settingsMenu = new();
    private readonly List<DropdownMenu.Item> settingsItems = new(6);

    private void DrawSettings(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, Loc.T(L.Linkpearl.ChatSettings), backToList);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            DrawNotificationSettings(scale);
            DrawPopoutSettings(scale);
            DrawHistorySettings(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }

        DrawSettingsMenu(area);
    }

    private void DrawNotificationSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Common.Alerts), frameTheme);
        var card = GroupCard.Begin(frameTheme, 1);
        var paused = SettingsRow.Bool(card.NextRow(), Loc.T(L.Messages.PauseNotifications), notificationGate.Paused,
            frameTheme, "linkpearl.settings.pause");
        if (paused != notificationGate.Paused)
        {
            notificationGate.SetPaused(paused);
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.PauseHint), frameTheme);
    }

    private void DrawPopoutSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.PopoutSection), frameTheme);
        var card = GroupCard.Begin(frameTheme, 3);
        var popTells = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.PopoutTells),
            configuration.LinkpearlPopoutTells, frameTheme, "linkpearl.settings.popTells");
        if (popTells != configuration.LinkpearlPopoutTells)
        {
            configuration.LinkpearlPopoutTells = popTells;
            configuration.Save();
        }

        DrawOpacityRow(card.NextRow(), scale);
        var textSizeRow = card.NextRow();
        if (SettingsRow.Disclosure(textSizeRow, Loc.T(L.Linkpearl.PopoutTextSize),
                PercentLabel(configuration.LinkpearlPopoutTextScale), frameTheme, "linkpearl.settings.textSize"))
        {
            settingsMenu.Toggle("linkpearl.settings.textSize", textSizeRow);
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.PopoutHint), frameTheme);
        if (popouts.OpenCount == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var closeCard = GroupCard.Begin(frameTheme, 1);
        if (SettingsRow.Action(closeCard.NextRow(), Loc.T(L.Linkpearl.CloseAllPopouts, popouts.OpenCount),
                frameTheme.Accent, frameTheme))
        {
            popouts.CloseAll();
        }

        closeCard.End();
    }

    private void DrawOpacityRow(Rect row, float scale)
    {
        var label = Loc.T(L.Linkpearl.PopoutOpacity);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        var labelWidth = row.Width * SliderLabelWidth;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f),
            Typography.FitText(label, labelWidth, TextStyles.BodyEmphasized), frameTheme.TextStrong,
            TextStyles.BodyEmphasized);
        var normalized = (Math.Clamp(configuration.LinkpearlPopoutOpacity, PopoutOpacityMinimum, 1f) - PopoutOpacityMinimum) /
                         (1f - PopoutOpacityMinimum);
        var result = Slider.Draw("linkpearl.settings.opacity", row, normalized, frameTheme,
            labelWidth + Metrics.Space.Md * scale, Metrics.Space.Xs * scale);
        var next = PopoutOpacityMinimum + result.Value * (1f - PopoutOpacityMinimum);
        if (MathF.Abs(next - configuration.LinkpearlPopoutOpacity) > 0.002f)
        {
            configuration.LinkpearlPopoutOpacity = next;
        }

        if (result.Released)
        {
            configuration.Save();
        }
    }

    private void DrawHistorySettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.KeepHistory), frameTheme);
        var card = GroupCard.Begin(frameTheme, 2);
        var stored = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.StoreHistory), configuration.ArchiveTellsToDisk,
            frameTheme, "linkpearl.settings.store");
        if (stored != configuration.ArchiveTellsToDisk)
        {
            configuration.ArchiveTellsToDisk = stored;
            configuration.Save();
        }

        var defaultPolicy = (HistoryPolicy)Math.Clamp(configuration.LinkpearlHistory, 0, (int)HistoryPolicy.Forever);
        var historyRow = card.NextRow();
        if (SettingsRow.Disclosure(historyRow, Loc.T(L.Linkpearl.HistoryDefault),
                Loc.T(HistoryLabelFor(defaultPolicy)), frameTheme, "linkpearl.settings.history",
                !configuration.ArchiveTellsToDisk))
        {
            settingsMenu.Toggle("linkpearl.settings.history", historyRow);
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.StoredOnThisPc), frameTheme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        var dangerCard = GroupCard.Begin(frameTheme, 1);
        if (SettingsRow.Action(dangerCard.NextRow(), Loc.T(L.Linkpearl.ClearAllHistory), frameTheme.Danger, frameTheme))
        {
            AskClearAllHistory();
        }

        dangerCard.End();
    }

    private void DrawSettingsMenu(Rect area)
    {
        if (settingsMenu.IsOpenFor("linkpearl.settings.textSize"))
        {
            settingsItems.Clear();
            for (var index = 0; index < TextScaleChoices.Length; index++)
            {
                settingsItems.Add(new DropdownMenu.Item(PercentLabel(TextScaleChoices[index]), string.Empty, false,
                    MathF.Abs(TextScaleChoices[index] - configuration.LinkpearlPopoutTextScale) < 0.01f));
            }

            var picked = settingsMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(settingsItems));
            if (picked >= 0)
            {
                configuration.LinkpearlPopoutTextScale = TextScaleChoices[picked];
                configuration.Save();
            }

            return;
        }

        if (!settingsMenu.IsOpenFor("linkpearl.settings.history"))
        {
            return;
        }

        settingsItems.Clear();
        var current = (HistoryPolicy)Math.Clamp(configuration.LinkpearlHistory, 0, (int)HistoryPolicy.Forever);
        for (var index = 0; index < HistoryChoices.Length; index++)
        {
            settingsItems.Add(new DropdownMenu.Item(Loc.T(HistoryLabelFor(HistoryChoices[index])), string.Empty, false,
                current == HistoryChoices[index]));
        }

        var choice = settingsMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(settingsItems));
        if (choice < 0)
        {
            return;
        }

        configuration.LinkpearlHistory = (int)HistoryChoices[choice];
        configuration.Save();
    }

    private void AskClearAllHistory() =>
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Linkpearl.ClearAllHistory),
            Message = Loc.T(L.Linkpearl.ClearAllHistoryConfirm),
            ConfirmLabel = Loc.T(L.Linkpearl.ClearHistory),
            CancelLabel = Loc.T(L.Messages.DeleteHistoryCancel),
            Confirm = () =>
            {
                popouts.CloseAll();
                archive.DeleteAll();
                chatLog.Clear();
                inbox.Invalidate();
                threadKey = string.Empty;
            },
        });

    private static string PercentLabel(float value) =>
        string.Concat(MathF.Round(value * 100f).ToString(Loc.Culture), "%");
}
