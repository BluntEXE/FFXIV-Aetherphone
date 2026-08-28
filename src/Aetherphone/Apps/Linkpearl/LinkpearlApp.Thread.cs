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
    private const float ThreadHeaderHeight = 56f;
    private const float ThreadBackReserve = 40f;
    private const float ThreadAvatarRadius = 16f;

    private string threadKey = string.Empty;

    private void DrawConversation(Rect area, string key)
    {
        inbox.Sync();
        var row = inbox.Find(key);
        if (row is null)
        {
            router.Reset();
            return;
        }

        inbox.Viewing = key;
        var scale = UiScale.Current;
        var header = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + ThreadHeaderHeight * scale));
        DrawThreadHeader(header, row, scale);
        OpenThread(row);
        chatThread.Draw(new Rect(new Vector2(area.Min.X, header.Max.Y), area.Max), frameTheme);
        chatMenu.Draw(area, frameTheme);
    }

    private void DrawThreadHeader(Rect header, InboxRow row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerY = header.Center.Y;
        var backHit = new Rect(header.Min, new Vector2(header.Min.X + ThreadBackReserve * scale, header.Max.Y));
        var backHovered = UiInteract.Hover(backHit.Min, backHit.Max);
        if (BackButton.Draw("linkpearl.thread.back", new Vector2(header.Min.X + 13f * scale, centerY), 15f * scale,
                frameTheme.Accent, backHovered, scale))
        {
            backToList();
            return;
        }

        var actions = new HeaderActions(CenteredActionRow(header, scale), scale, 3);
        var avatarRadius = ThreadAvatarRadius * scale;
        var avatarCenter = new Vector2(header.Min.X + ThreadBackReserve * scale + avatarRadius, centerY);
        DrawThreadAvatar(drawList, avatarCenter, avatarRadius, row);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Sm * scale;
        var textWidth = MathF.Max(1f, actions.TitleLimit - textLeft);
        var subtitle = GameChatTargets.Subtitle(row);
        var titleStyle = TextStyles.Headline;
        var titleSize = Typography.Measure(Title(row), titleStyle);
        var titleTop = subtitle.Length > 0
            ? centerY - titleSize.Y - 1f * scale
            : centerY - titleSize.Y * 0.5f;
        var titleHovering = UiInteract.Hover(new Vector2(textLeft, titleTop),
            new Vector2(textLeft + textWidth, titleTop + titleSize.Y));
        Marquee.DrawLeft(drawList, "linkpearl.thread.title." + row.Key, Title(row), textLeft, titleTop, textWidth,
            titleStyle, frameTheme.TextStrong, titleHovering);
        if (subtitle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, centerY + 2f * scale),
                Typography.FitText(subtitle, textWidth, TextStyles.Caption1), frameTheme.TextMuted,
                TextStyles.Caption1);
        }

        if (ui.IconButton(actions.Slot(0), actions.Radius, IconGlyph.Of(FontAwesomeIcon.EllipsisH),
                frameTheme.TextStrong, AppSkin.Transparent, 1f, Loc.T(L.Linkpearl.More), HoverLabelSide.Below))
        {
            OpenConversationSheet(row);
        }

        if (ui.IconButton(actions.Slot(1), actions.Radius, IconGlyph.Of(FontAwesomeIcon.Search),
                chatThread.SearchOpen ? frameTheme.Accent : frameTheme.TextStrong, AppSkin.Transparent, 0.95f,
                Loc.T(L.Common.Search), HoverLabelSide.Below))
        {
            chatThread.ToggleSearch();
        }

        if (ui.IconButton(actions.Slot(2), actions.Radius,
                IconGlyph.Of((row.Muted ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell)),
                row.Muted ? frameTheme.Accent : frameTheme.TextStrong, AppSkin.Transparent, 0.95f,
                Loc.T(row.Muted ? L.Linkpearl.Unmute : L.Linkpearl.Mute), HoverLabelSide.Below))
        {
            inbox.ToggleMuted(row);
        }

        if (popouts.IsOpen(row.Key))
        {
            var mark = actions.Slot(0) + new Vector2(10f * scale, -10f * scale);
            drawList.AddCircleFilled(mark, 3.5f * scale, ImGui.GetColorU32(frameTheme.Accent), 12);
        }
    }

    private void DrawThreadAvatar(ImDrawListPtr drawList, Vector2 center, float radius, InboxRow row)
    {
        if (row.IsTell)
        {
            AvatarView.Draw(drawList, center, radius, frameTheme.Accent, Initials.Of(row.Title), 0.85f,
                lodestone.Avatar(row.Title, row.World, radius * 2f), 28);
            return;
        }

        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        Squircle.Fill(drawList, min, max, radius * 0.62f, ImGui.GetColorU32(Palette.WithAlpha(row.Tint, 0.22f)));
        Typography.DrawCentered(drawList, center, Initials.Of(row.Title), row.Tint, TextStyles.Caption1);
    }

    private void OpenThread(InboxRow row)
    {
        if (string.Equals(threadKey, row.Key, StringComparison.Ordinal))
        {
            return;
        }

        threadKey = row.Key;
        chatThread.Open(GameChatTargets.For(row));
    }
}
