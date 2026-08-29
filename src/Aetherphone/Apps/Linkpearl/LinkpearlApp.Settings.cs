using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float SettingsSliderRowHeight = 52f;
    private const float SliderLabelWidth = 0.42f;
    private const float PopoutOpacityMinimum = 0.5f;
    private const float SettingsLinkRowHeight = 62f;
    private const float IdleOpacityMinimum = 0.15f;
    private const float OpacityEpsilon = 0.002f;

    private static readonly float[] TextScaleChoices = { 0.8f, 0.9f, 1f, 1.15f, 1.3f, 1.5f };

    private readonly List<DropdownMenu.Item> settingsItems = new(6);

    private static readonly LinkpearlSettingsSection[] SettingsSections =
    {
        LinkpearlSettingsSection.Popouts, LinkpearlSettingsSection.Behavior, LinkpearlSettingsSection.Composer,
        LinkpearlSettingsSection.Channels, LinkpearlSettingsSection.History,
    };

    private static readonly Vector4[] SettingsSectionTints =
    {
        new(0.36f, 0.55f, 0.95f, 1f),
        new(0.95f, 0.68f, 0.25f, 1f),
        new(0.20f, 0.70f, 0.62f, 1f),
        new(0.62f, 0.45f, 0.92f, 1f),
        new(0.40f, 0.62f, 0.48f, 1f),
    };

    private void DrawSettings(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, Loc.T(L.Linkpearl.ChatSettings), backToList);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var alerts = GroupCard.Begin(frameTheme, 1);
            var paused = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Messages.PauseNotifications),
                notificationGate.Paused, frameTheme, "linkpearl.settings.pause");
            if (paused != notificationGate.Paused)
            {
                notificationGate.SetPaused(paused);
            }

            alerts.End();
            SettingsSection.Hint(Loc.T(L.Linkpearl.PauseHint), frameTheme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            var card = GroupCard.Begin(frameTheme, SettingsSections.Length, SettingsLinkRowHeight);
            for (var index = 0; index < SettingsSections.Length; index++)
            {
                var section = SettingsSections[index];
                if (SettingsRow.Link(card.NextRow(), SectionIcon(section), SettingsSectionTints[index],
                        Loc.T(SectionTitle(section)), string.Empty, frameTheme))
                {
                    router.Push(LinkpearlRoute.SettingsFor(section));
                }
            }

            card.End();
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }
    }

    private void DrawSettingsSection(Rect area, LinkpearlSettingsSection section)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, Loc.T(SectionTitle(section)), backToSettings);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            switch (section)
            {
                case LinkpearlSettingsSection.Popouts:
                    DrawPopoutSettings(scale);
                    break;
                case LinkpearlSettingsSection.Behavior:
                    DrawBehaviorSettings(scale);
                    break;
                case LinkpearlSettingsSection.Composer:
                    DrawComposerSettings(scale);
                    break;
                case LinkpearlSettingsSection.Channels:
                    DrawChannelSettings(scale);
                    break;
                default:
                    DrawHistorySettings(scale);
                    break;
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }

        DrawSettingsMenu(area);
    }

    private static FontAwesomeIcon SectionIcon(LinkpearlSettingsSection section) => section switch
    {
        LinkpearlSettingsSection.Popouts => FontAwesomeIcon.ExternalLinkAlt,
        LinkpearlSettingsSection.Behavior => FontAwesomeIcon.SlidersH,
        LinkpearlSettingsSection.Composer => FontAwesomeIcon.PenAlt,
        LinkpearlSettingsSection.Channels => FontAwesomeIcon.Hashtag,
        _ => FontAwesomeIcon.History,
    };

    private static LocString SectionTitle(LinkpearlSettingsSection section) => section switch
    {
        LinkpearlSettingsSection.Popouts => L.Linkpearl.PopoutSection,
        LinkpearlSettingsSection.Behavior => L.Linkpearl.BehaviorSection,
        LinkpearlSettingsSection.Composer => L.Linkpearl.ComposerSection,
        LinkpearlSettingsSection.Channels => L.Linkpearl.ChannelStyleSection,
        _ => L.Linkpearl.KeepHistory,
    };

    private void DrawPopoutSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.PopoutSection), frameTheme);
        var behaviour = GroupCard.Begin(frameTheme, 4);
        var grouped = SettingsRow.Bool(behaviour.NextRow(), Loc.T(L.Linkpearl.PopoutTabs),
            configuration.LinkpearlPopoutTabs, frameTheme, "linkpearl.settings.popoutTabs");
        if (grouped != configuration.LinkpearlPopoutTabs)
        {
            configuration.LinkpearlPopoutTabs = grouped;
            configuration.Save();
        }

        var popTells = SettingsRow.Bool(behaviour.NextRow(), Loc.T(L.Linkpearl.PopoutTells),
            configuration.LinkpearlPopoutTells, frameTheme, "linkpearl.settings.popTells");
        if (popTells != configuration.LinkpearlPopoutTells)
        {
            configuration.LinkpearlPopoutTells = popTells;
            configuration.Save();
        }

        var outgoing = SettingsRow.Bool(behaviour.NextRow(), Loc.T(L.Linkpearl.PopoutOutgoingTells),
            configuration.LinkpearlPopoutOutgoingTells, frameTheme, "linkpearl.settings.outgoingTells", null,
            !popTells);
        if (outgoing != configuration.LinkpearlPopoutOutgoingTells)
        {
            configuration.LinkpearlPopoutOutgoingTells = outgoing;
            configuration.Save();
        }

        var closeOnLogout = SettingsRow.Bool(behaviour.NextRow(), Loc.T(L.Linkpearl.PopoutCloseOnLogout),
            configuration.LinkpearlPopoutCloseOnLogout, frameTheme, "linkpearl.settings.closeOnLogout");
        if (closeOnLogout != configuration.LinkpearlPopoutCloseOnLogout)
        {
            configuration.LinkpearlPopoutCloseOnLogout = closeOnLogout;
            configuration.Save();
        }

        behaviour.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.PopoutTabsHint), frameTheme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var fade = configuration.LinkpearlPopoutFade;
        var look = GroupCard.Begin(frameTheme, fade ? 4 : 3);
        DrawOpacityRow(look.NextRow(), scale);
        var nextFade = SettingsRow.Bool(look.NextRow(), Loc.T(L.Linkpearl.PopoutFade), fade, frameTheme,
            "linkpearl.settings.popoutFade");
        if (nextFade != fade)
        {
            configuration.LinkpearlPopoutFade = nextFade;
            configuration.Save();
        }

        if (fade)
        {
            DrawIdleOpacityRow(look.NextRow(), scale);
        }

        var textSizeRow = look.NextRow();
        if (SettingsRow.Disclosure(textSizeRow, Loc.T(L.Linkpearl.PopoutTextSize),
                PercentLabel(configuration.LinkpearlPopoutTextScale), frameTheme, "linkpearl.settings.textSize"))
        {
            settingsMenu.Toggle("linkpearl.settings.textSize", textSizeRow);
        }

        look.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.PopoutHint), frameTheme);
        if (popouts.OpenCount == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var closeCard = GroupCard.Begin(frameTheme, 2);
        var anyExpanded = popouts.AnyExpanded;
        if (SettingsRow.Action(closeCard.NextRow(),
                Loc.T(anyExpanded ? L.Linkpearl.CollapseAllPopouts : L.Linkpearl.ExpandAllPopouts, popouts.OpenCount),
                frameTheme.Accent, frameTheme))
        {
            popouts.SetAllCollapsed(anyExpanded);
        }

        if (SettingsRow.Action(closeCard.NextRow(), Loc.T(L.Linkpearl.CloseAllPopouts, popouts.OpenCount),
                frameTheme.Accent, frameTheme))
        {
            popouts.CloseAll();
        }

        closeCard.End();
    }

    private void DrawOpacityRow(Rect row, float scale)
    {
        configuration.LinkpearlPopoutOpacity = DrawOpacitySlider(row, scale, "linkpearl.settings.opacity",
            Loc.T(L.Linkpearl.PopoutOpacity), configuration.LinkpearlPopoutOpacity, PopoutOpacityMinimum,
            out var released);
        if (released)
        {
            configuration.Save();
        }
    }

    private void DrawIdleOpacityRow(Rect row, float scale)
    {
        configuration.LinkpearlPopoutIdleOpacity = DrawOpacitySlider(row, scale, "linkpearl.settings.idleOpacity",
            Loc.T(L.Linkpearl.PopoutIdleOpacity), configuration.LinkpearlPopoutIdleOpacity, IdleOpacityMinimum,
            out var released);
        if (released)
        {
            configuration.Save();
        }
    }

    private float DrawOpacitySlider(Rect row, float scale, string id, string label, float value, float minimum,
        out bool released)
    {
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        var labelWidth = row.Width * SliderLabelWidth;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f),
            Typography.FitText(label, labelWidth, TextStyles.BodyEmphasized), frameTheme.TextStrong,
            TextStyles.BodyEmphasized);
        var span = 1f - minimum;
        var normalized = (Math.Clamp(value, minimum, 1f) - minimum) / span;
        var result = Slider.Draw(id, row, normalized, frameTheme, labelWidth + Metrics.Space.Md * scale,
            Metrics.Space.Xs * scale);
        released = result.Released;
        var next = minimum + result.Value * span;
        return MathF.Abs(next - value) > OpacityEpsilon ? next : value;
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
            Sheet = true,
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
