using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private readonly record struct QuickTab(TabPreset Preset, FontAwesomeIcon Icon, Vector4 Tint, string ProbeChannel,
        LocString Hint);

    private const float NewChatSheetHeight = 0.64f;
    private const float QuickTileHeight = 76f;
    private const float QuickTileGap = 10f;
    private const float QuickIconRadius = 17f;
    private const float SheetRowHeight = 50f;
    private const float SheetLabelHeight = 24f;

    private static readonly QuickTab[] QuickTabs =
    {
        new(TabPreset.FreeCompany, FontAwesomeIcon.ShieldAlt, ChannelTints.FreeCompany, GameChannels.FreeCompanyKey,
            L.Linkpearl.PresetFreeCompanyHint),
        new(TabPreset.Linkshells, FontAwesomeIcon.Link, ChannelTints.Linkshell, "ls1", L.Linkpearl.PresetLinkshellsHint),
        new(TabPreset.Party, FontAwesomeIcon.UserFriends, ChannelTints.Party, GameChannels.PartyKey,
            L.Linkpearl.PresetPartyHint),
        new(TabPreset.Local, FontAwesomeIcon.MapMarkerAlt, ChannelTints.Say, GameChannels.SayKey,
            L.Linkpearl.PresetLocalHint),
    };

    private readonly SheetSurface newChatSheet = new("linkpearl.newChat");
    private readonly Action<Rect> drawNewChatSheet;
    private bool peopleFocusPending;

    private void OpenNewChat()
    {
        newChatSheet.Open();
    }

    private void DrawNewChatSheet(Rect content)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var cursorY = content.Min.Y;
        DrawSheetLabel(drawList, Loc.T(L.Linkpearl.QuickTabs), content.Min.X, cursorY, scale);
        cursorY += SheetLabelHeight * scale;
        var gap = QuickTileGap * scale;
        var tileWidth = (content.Width - gap) * 0.5f;
        var tileHeight = QuickTileHeight * scale;
        for (var index = 0; index < QuickTabs.Length; index++)
        {
            var column = index % 2;
            var rowIndex = index / 2;
            var min = new Vector2(content.Min.X + column * (tileWidth + gap), cursorY + rowIndex * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            DrawQuickTile(drawList, new Rect(min, max), QuickTabs[index], scale);
        }

        cursorY += 2f * tileHeight + gap + Metrics.Space.Lg * scale;
        DrawSheetLabel(drawList, Loc.T(L.Linkpearl.OrStartFresh), content.Min.X, cursorY, scale);
        cursorY += SheetLabelHeight * scale;
        var rowHeight = SheetRowHeight * scale;
        var cardMin = new Vector2(content.Min.X, cursorY);
        var cardMax = new Vector2(content.Max.X, cursorY + rowHeight * 2f);
        Squircle.Fill(drawList, cardMin, cardMax, Metrics.Radius.Md * scale, ImGui.GetColorU32(frameTheme.GroupedCard));
        Material.EdgeSquircle(drawList, cardMin, cardMax, Metrics.Radius.Md * scale, scale);
        var customRow = new Rect(cardMin, new Vector2(cardMax.X, cardMin.Y + rowHeight));
        var tellRow = new Rect(new Vector2(cardMin.X, customRow.Max.Y), cardMax);
        drawList.AddLine(new Vector2(cardMin.X + Metrics.Space.Lg * scale, customRow.Max.Y),
            new Vector2(cardMax.X, customRow.Max.Y), ImGui.GetColorU32(frameTheme.Separator), Metrics.Stroke.Hairline);
        if (DrawSheetRow(drawList, customRow, FontAwesomeIcon.SlidersH, frameTheme.Accent,
                Loc.T(L.Linkpearl.CustomTab), Loc.T(L.Linkpearl.CustomTabHint), scale))
        {
            newChatSheet.Close();
            CreateTab();
        }

        if (DrawSheetRow(drawList, tellRow, FontAwesomeIcon.PenAlt, ChannelTints.Tell, Loc.T(L.Linkpearl.SendTell),
                Loc.T(L.Linkpearl.SendTellHint), scale))
        {
            newChatSheet.Close();
            SelectTab(MessagesTab.People);
            peopleFocusPending = true;
        }
    }

    private void DrawSheetLabel(ImDrawListPtr drawList, string text, float left, float top, float scale)
    {
        var label = Loc.Culture.TextInfo.ToUpper(text);
        Typography.Draw(drawList, new Vector2(left + Metrics.Space.Xs * scale, top + 2f * scale), label,
            frameTheme.TextMuted, TextStyles.Caption2);
    }

    private void DrawQuickTile(ImDrawListPtr drawList, Rect tile, in QuickTab quick, float scale)
    {
        var existing = tabs.FirstTabWith(quick.ProbeChannel);
        var added = existing is not null;
        var hovered = UiInteract.Hover(tile.Min, tile.Max);
        var rounding = Metrics.Radius.Card * scale;
        var fill = Palette.WithAlpha(quick.Tint, hovered ? 0.22f : 0.14f);
        Squircle.Fill(drawList, tile.Min, tile.Max, rounding, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, tile.Min, tile.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(quick.Tint, hovered ? 0.5f : 0.28f)), Metrics.Stroke.Hairline);
        var iconRadius = QuickIconRadius * scale;
        var iconCenter = new Vector2(tile.Min.X + Metrics.Space.Md * scale + iconRadius, tile.Min.Y + Metrics.Space.Md * scale + iconRadius);
        drawList.AddCircleFilled(iconCenter, iconRadius, ImGui.GetColorU32(Palette.WithAlpha(quick.Tint, 0.28f)), 28);
        AppSkin.Icon(drawList, iconCenter, quick.Icon.ToIconString(), quick.Tint, 0.95f);
        var textLeft = tile.Min.X + Metrics.Space.Md * scale;
        var textWidth = tile.Width - Metrics.Space.Md * scale * 2f;
        var name = Loc.T(TabStore.PresetLabel(quick.Preset));
        var nameTop = iconCenter.Y + iconRadius + Metrics.Space.Xs * scale;
        Typography.Draw(drawList, new Vector2(textLeft, nameTop), Typography.FitText(name, textWidth, TextStyles.BodyEmphasized),
            frameTheme.TextStrong, TextStyles.BodyEmphasized);
        var hint = added ? Loc.T(L.Linkpearl.PresetAdded) : Loc.T(quick.Hint);
        Typography.Draw(drawList, new Vector2(textLeft, nameTop + Typography.LineHeight(TextStyles.BodyEmphasized)),
            Typography.FitText(hint, textWidth, TextStyles.Caption1), added ? quick.Tint : frameTheme.TextMuted,
            TextStyles.Caption1);
        if (added)
        {
            AppSkin.Icon(drawList, new Vector2(tile.Max.X - Metrics.Space.Md * scale - 6f * scale, iconCenter.Y),
                FontAwesomeIcon.Check.ToIconString(), quick.Tint, 0.7f);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(tile.Min, tile.Max, hovered))
        {
            return;
        }

        newChatSheet.Close();
        if (existing is not null)
        {
            inbox.Sync();
            OpenConversation(ChatInbox.KeyForTab(existing));
            return;
        }

        var created = tabs.AddPreset(quick.Preset);
        inbox.Invalidate();
        inbox.Sync();
        OpenConversation(ChatInbox.KeyForTab(created));
    }

    private bool DrawSheetRow(ImDrawListPtr drawList, Rect row, FontAwesomeIcon icon, Vector4 tint, string title,
        string hint, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            SettingsRow.DrawRowHighlight(new Rect(new Vector2(row.Min.X + Metrics.Space.Sm * scale, row.Min.Y + 2f * scale),
                new Vector2(row.Max.X - Metrics.Space.Sm * scale, row.Max.Y - 2f * scale)), frameTheme);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconRadius = 14f * scale;
        var iconCenter = new Vector2(row.Min.X + Metrics.Space.Lg * scale + iconRadius, row.Center.Y);
        drawList.AddCircleFilled(iconCenter, iconRadius, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.2f)), 24);
        AppSkin.Icon(drawList, iconCenter, icon.ToIconString(), tint, 0.78f);
        var textLeft = iconCenter.X + iconRadius + Metrics.Space.Md * scale;
        var textWidth = row.Max.X - Metrics.Space.Xl * scale - textLeft;
        var titleSize = Typography.Measure(title, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - titleSize.Y - 1f * scale),
            Typography.FitText(title, textWidth, TextStyles.BodyEmphasized), frameTheme.TextStrong,
            TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y + 1f * scale),
            Typography.FitText(hint, textWidth, TextStyles.Caption1), frameTheme.TextMuted, TextStyles.Caption1);
        var chevronTip = new Vector2(row.Max.X - Metrics.Space.Lg * scale, row.Center.Y);
        var chevron = ImGui.GetColorU32(frameTheme.TextMuted);
        drawList.AddLine(new Vector2(chevronTip.X - 6f * scale, chevronTip.Y - 6f * scale), chevronTip, chevron, 2f * scale);
        drawList.AddLine(chevronTip, new Vector2(chevronTip.X - 6f * scale, chevronTip.Y + 6f * scale), chevron, 2f * scale);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }
}
