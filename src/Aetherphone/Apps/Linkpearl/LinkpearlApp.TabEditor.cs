using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const int TabNameMaxLength = 24;
    private const float EditorRowHeight = 46f;

    private static readonly ChannelCategory[] PickerOrder =
    {
        ChannelCategory.Community,
        ChannelCategory.Group,
        ChannelCategory.Linkshell,
        ChannelCategory.CrossWorld,
        ChannelCategory.Local,
        ChannelCategory.System,
    };

    private static readonly HistoryPolicy[] HistoryChoices =
    {
        HistoryPolicy.Off,
        HistoryPolicy.Session,
        HistoryPolicy.Days30,
        HistoryPolicy.Forever,
    };

    private readonly DropdownMenu editorMenu = new();
    private readonly List<DropdownMenu.Item> editorItems = new(24);
    private readonly List<string> editorKeys = new(24);
    private string editorName = string.Empty;
    private string editorTabId = string.Empty;

    private void OpenTabEditor(ChatTab tab)
    {
        editorTabId = tab.Id;
        editorName = tab.Name;
        router.Push(LinkpearlRoute.TabEditor(tab.Id));
    }

    private void CreateTab()
    {
        if (!tabs.CanAdd)
        {
            return;
        }

        OpenTabEditor(tabs.Create(Loc.T(L.Linkpearl.NewTab), Array.Empty<string>()));
        inbox.Invalidate();
    }

    private void DrawTabEditor(Rect area, string tabId)
    {
        var tab = tabs.Find(tabId);
        if (tab is null)
        {
            router.Reset();
            return;
        }

        var scale = UiScale.Current;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, Loc.T(L.Linkpearl.EditTab), leaveTabEditor);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawNameField(tab, width, scale);
            DrawTintSwatches(tab, width, scale);
            SettingsSection.Header(Loc.T(L.Linkpearl.Channels), frameTheme,
                tab.Channels.Count == 0 ? Loc.T(L.Linkpearl.NoChannels) : null);
            for (var index = 0; index < PickerOrder.Length; index++)
            {
                DrawCategory(tab, PickerOrder[index], width, scale);
            }

            SettingsSection.Header(Loc.T(L.Linkpearl.TabSettings), frameTheme);
            DrawTabSettings(tab, width, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            if (SettingsRow.Action(NextRow(width, scale), Loc.T(L.Linkpearl.DeleteTab), frameTheme.Danger, frameTheme))
            {
                AskDeleteTab(tab);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }

        DrawEditorMenu(area, tab);
    }

    private void DrawNameField(ChatTab tab, float width, float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.TabName), frameTheme);
        var row = NextRow(width, scale);
        var drawList = ImGui.GetWindowDrawList();
        var inset = Metrics.Space.Lg * scale;
        var fieldMin = new Vector2(row.Min.X + inset, row.Min.Y + 4f * scale);
        var fieldMax = new Vector2(row.Max.X - inset, row.Max.Y - 4f * scale);
        Squircle.Fill(drawList, fieldMin, fieldMax, Metrics.Radius.Field * scale,
            ImGui.GetColorU32(frameTheme.GroupedCard));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + Metrics.Space.Md * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldMax.X - fieldMin.X - Metrics.Space.Md * scale * 2f);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.InputText("##linkpearl.tabname", ref editorName, TabNameMaxLength);
        }

        var trimmed = editorName.Trim();
        if (trimmed.Length > 0 && !string.Equals(trimmed, tab.Name, StringComparison.Ordinal))
        {
            tab.Name = trimmed;
            tabs.Update(tab);
            inbox.Invalidate();
        }
    }

    private void DrawTintSwatches(ChatTab tab, float width, float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.TabTint), frameTheme);
        var row = NextRow(width, scale);
        var drawList = ImGui.GetWindowDrawList();
        var palette = ChannelTints.TabPalette;
        var radius = 12f * scale;
        var spacing = (row.Width - Metrics.Space.Lg * scale * 2f) / palette.Length;
        for (var index = 0; index < palette.Length; index++)
        {
            var center = new Vector2(row.Min.X + Metrics.Space.Lg * scale + spacing * (index + 0.5f), row.Center.Y);
            var selected = tab.Tint == index;
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(palette[index]), 24);
            if (selected)
            {
                drawList.AddCircle(center, radius + 4f * scale, ImGui.GetColorU32(frameTheme.TextStrong), 24,
                    1.6f * scale);
            }

            if (!UiInteract.HoverClickCircle(center, radius + 3f * scale) || selected)
            {
                continue;
            }

            tab.Tint = index;
            tabs.Update(tab);
            inbox.Invalidate();
        }
    }

    private void DrawCategory(ChatTab tab, ChannelCategory category, float width, float scale)
    {
        var all = GameChannels.All;
        var drew = false;
        for (var index = 0; index < all.Length; index++)
        {
            var channel = all[index];
            if (channel.Category != category)
            {
                continue;
            }

            if (!drew)
            {
                SettingsSection.Hint(Loc.T(CategoryLabel(category)), frameTheme);
                drew = true;
            }

            DrawChannelRow(tab, channel, NextRow(width, scale), scale);
        }
    }

    private void DrawChannelRow(ChatTab tab, GameChannel channel, Rect row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, new Vector2(row.Min.X + Metrics.Space.Sm * scale, row.Min.Y),
                new Vector2(row.Max.X - Metrics.Space.Sm * scale, row.Max.Y), Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.05f)));
        }

        var left = row.Min.X + Metrics.Space.Lg * scale;
        var dotCenter = new Vector2(left + 4f * scale, row.Center.Y);
        drawList.AddCircleFilled(dotCenter, 4f * scale, ImGui.GetColorU32(channel.Tint), 12);
        var joined = LinkshellNames.Joined(channel);
        var label = LinkshellNames.Label(channel);
        var textLeft = dotCenter.X + 12f * scale;
        var selected = tab.Includes(channel.Key);
        var ink = joined ? frameTheme.TextStrong : frameTheme.TextMuted;
        var labelWidth = row.Max.X - Metrics.Space.Xxl * scale - textLeft;
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelSize.Y * 0.5f),
            Typography.FitText(label, labelWidth, TextStyles.BodyEmphasized), ink, TextStyles.BodyEmphasized);
        if (channel.IsSlotted && !joined)
        {
            var hint = Loc.T(L.Linkpearl.EmptySlot);
            Typography.Draw(drawList, new Vector2(textLeft + labelSize.X + Metrics.Space.Sm * scale,
                row.Center.Y - labelSize.Y * 0.5f + 1f * scale), hint, frameTheme.TextMuted, TextStyles.Caption1);
        }

        if (selected)
        {
            var tip = new Vector2(row.Max.X - Metrics.Space.Xl * scale, row.Center.Y + 4f * scale);
            var color = ImGui.GetColorU32(frameTheme.Accent);
            drawList.AddLine(tip - new Vector2(5f * scale, 5f * scale), tip, color, 2f * scale);
            drawList.AddLine(tip, new Vector2(tip.X + 9f * scale, tip.Y - 11f * scale), color, 2f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(row.Min, row.Max, hovered))
        {
            return;
        }

        if (selected)
        {
            tab.Channels.Remove(channel.Key);
        }
        else
        {
            tab.Channels.Add(channel.Key);
        }

        tabs.Update(tab);
        inbox.Invalidate();
    }

    private void DrawTabSettings(ChatTab tab, float width, float scale)
    {
        if (SettingsRow.Disclosure(NextRow(width, scale), Loc.T(L.Linkpearl.RepliesGoTo), SendChannelLabel(tab),
                frameTheme))
        {
            editorMenu.Toggle("linkpearl.editor.send", LastRow(width, scale));
        }

        if (SettingsRow.Disclosure(NextRow(width, scale), Loc.T(L.Linkpearl.Layout), Loc.T(
                tab.Density == ChatDensity.Bubbles ? L.Linkpearl.LayoutBubbles : L.Linkpearl.LayoutCompact),
                frameTheme))
        {
            editorMenu.Toggle("linkpearl.editor.layout", LastRow(width, scale));
        }

        if (SettingsRow.Disclosure(NextRow(width, scale), Loc.T(L.Linkpearl.KeepHistory), HistoryLabel(tab),
                frameTheme))
        {
            editorMenu.Toggle("linkpearl.editor.history", LastRow(width, scale));
        }

        if (SettingsRow.Disclosure(NextRow(width, scale), Loc.T(L.Linkpearl.Alerts), Loc.T(AlertLabel(tab.Alerts)),
                frameTheme))
        {
            editorMenu.Toggle("linkpearl.editor.alerts", LastRow(width, scale));
        }

        SettingsSection.Hint(Loc.T(L.Linkpearl.StoredOnThisPc), frameTheme);
    }

    private void DrawEditorMenu(Rect area, ChatTab tab)
    {
        if (editorMenu.IsOpenFor("linkpearl.editor.send"))
        {
            editorItems.Clear();
            editorKeys.Clear();
            for (var index = 0; index < tab.Channels.Count; index++)
            {
                if (!GameChannels.TryByKey(tab.Channels[index], out var channel) || !channel.CanSend)
                {
                    continue;
                }

                editorItems.Add(new DropdownMenu.Item(LinkshellNames.Label(channel), string.Empty, false,
                    string.Equals(channel.Key, tab.SendChannel, StringComparison.Ordinal)));
                editorKeys.Add(channel.Key);
            }

            var picked = DrawEditorList(area);
            if (picked >= 0)
            {
                tab.SendChannel = editorKeys[picked];
                tabs.Update(tab);
            }

            return;
        }

        if (editorMenu.IsOpenFor("linkpearl.editor.layout"))
        {
            editorItems.Clear();
            editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.LayoutCompact), string.Empty, false,
                tab.Density == ChatDensity.Compact));
            editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.LayoutBubbles), string.Empty, false,
                tab.Density == ChatDensity.Bubbles));
            var picked = DrawEditorList(area);
            if (picked >= 0)
            {
                tab.Density = picked == 1 ? ChatDensity.Bubbles : ChatDensity.Compact;
                tabs.Update(tab);
                threadKey = string.Empty;
            }

            return;
        }

        if (editorMenu.IsOpenFor("linkpearl.editor.history"))
        {
            editorItems.Clear();
            var current = TabHistory(tab);
            for (var index = 0; index < HistoryChoices.Length; index++)
            {
                editorItems.Add(new DropdownMenu.Item(Loc.T(HistoryLabelFor(HistoryChoices[index])), string.Empty,
                    false, current == HistoryChoices[index]));
            }

            var picked = DrawEditorList(area);
            if (picked >= 0)
            {
                ApplyHistory(tab, HistoryChoices[picked]);
            }

            return;
        }

        if (!editorMenu.IsOpenFor("linkpearl.editor.alerts"))
        {
            return;
        }

        editorItems.Clear();
        editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.AlertsAll), string.Empty, false,
            tab.Alerts == AlertPolicy.All));
        editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.AlertsMentions), string.Empty, false,
            tab.Alerts == AlertPolicy.Mentions));
        editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.AlertsOff), string.Empty, false,
            tab.Alerts == AlertPolicy.Off));
        var choice = DrawEditorList(area);
        if (choice < 0)
        {
            return;
        }

        tab.Alerts = choice switch
        {
            0 => AlertPolicy.All,
            1 => AlertPolicy.Mentions,
            _ => AlertPolicy.Off,
        };
        tabs.Update(tab);
        inbox.Invalidate();
    }

    private int DrawEditorList(Rect area) =>
        editorItems.Count == 0 ? -1 : editorMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(editorItems));

    private void LeaveTabEditor()
    {
        editorMenu.Close();
        var tab = tabs.Find(editorTabId);
        if (tab is { Channels.Count: 0 })
        {
            tabs.Delete(tab);
            inbox.Invalidate();
        }

        editorTabId = string.Empty;
        router.Pop();
    }

    private string SendChannelLabel(ChatTab tab) =>
        GameChannels.TryByKey(tab.SendChannel, out var channel)
            ? LinkshellNames.Label(channel)
            : Loc.T(L.Linkpearl.ChannelReadOnly);

    private string HistoryLabel(ChatTab tab)
    {
        var policy = TabHistory(tab);
        return policy is null ? Loc.T(L.Linkpearl.HistoryMixed) : Loc.T(HistoryLabelFor(policy.Value));
    }

    private HistoryPolicy? TabHistory(ChatTab tab)
    {
        HistoryPolicy? shared = null;
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            var policy = archive.PolicyFor(tab.Channels[index]);
            if (shared is null)
            {
                shared = policy;
                continue;
            }

            if (shared != policy)
            {
                return null;
            }
        }

        return shared ?? HistoryPolicy.Days30;
    }

    private void ApplyHistory(ChatTab tab, HistoryPolicy policy)
    {
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            archive.SetPolicy(tab.Channels[index], policy);
        }
    }

    private static LocString HistoryLabelFor(HistoryPolicy policy) => policy switch
    {
        HistoryPolicy.Off => L.Linkpearl.HistoryOff,
        HistoryPolicy.Session => L.Linkpearl.HistorySession,
        HistoryPolicy.Forever => L.Linkpearl.HistoryForever,
        _ => L.Linkpearl.HistoryDays30,
    };

    private static LocString AlertLabel(AlertPolicy policy) => policy switch
    {
        AlertPolicy.All => L.Linkpearl.AlertsAll,
        AlertPolicy.Mentions => L.Linkpearl.AlertsMentions,
        _ => L.Linkpearl.AlertsOff,
    };

    private static LocString CategoryLabel(ChannelCategory category) => category switch
    {
        ChannelCategory.Local => L.Linkpearl.CategoryLocal,
        ChannelCategory.Group => L.Linkpearl.CategoryGroup,
        ChannelCategory.Community => L.Linkpearl.CategoryCommunity,
        ChannelCategory.Linkshell => L.Linkpearl.CategoryLinkshell,
        ChannelCategory.CrossWorld => L.Linkpearl.CategoryCrossWorld,
        _ => L.Linkpearl.CategorySystem,
    };

    private static Rect NextRow(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + EditorRowHeight * scale));
        ImGui.Dummy(new Vector2(width, EditorRowHeight * scale));
        return row;
    }

    private static Rect LastRow(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        return new Rect(new Vector2(origin.X, origin.Y - EditorRowHeight * scale),
            new Vector2(origin.X + width, origin.Y));
    }
}
