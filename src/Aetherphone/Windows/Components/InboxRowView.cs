using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum InboxRowAction : byte
{
    None,
    Open,
    Menu,
}

internal static class InboxRowView
{
    private const float Height = 64f;
    private const float AvatarRadius = 21f;

    public static InboxRowAction Draw(InboxRow row, PhoneTheme theme, LodestoneService lodestone)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + Height * scale);
        var hovered = UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var drawList = ImGui.GetWindowDrawList();
        if (hovered)
        {
            Squircle.Fill(drawList, new Vector2(min.X + Metrics.Space.Xs * scale, min.Y + 3f * scale),
                new Vector2(max.X - Metrics.Space.Xs * scale, max.Y - 3f * scale), Metrics.Radius.Md * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, pressed ? 0.10f : 0.05f)));
        }

        var avatarCenter = new Vector2(min.X + 14f * scale + AvatarRadius * scale, min.Y + Height * scale * 0.5f);
        DrawAvatar(drawList, avatarCenter, row, theme, lodestone, scale);
        var textLeft = avatarCenter.X + AvatarRadius * scale + Metrics.Space.Md * scale;
        var textRight = max.X - 14f * scale;
        var hasUnread = row.Unread > 0;
        var time = row.LastActivity == default ? string.Empty : TimeText.Short(row.LastActivity);
        var timeSize = time.Length > 0 ? Typography.Measure(time, TextStyles.Caption1) : Vector2.Zero;
        if (time.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textRight - timeSize.X, min.Y + 13f * scale), time,
                hasUnread ? theme.Accent : theme.TextMuted, TextStyles.Caption1);
        }

        var titleWidth = textRight - timeSize.X - Metrics.Space.Sm * scale - textLeft;
        var titleY = min.Y + 11f * scale;
        var titleHovering = UiInteract.Hover(new Vector2(textLeft, titleY),
            new Vector2(textLeft + titleWidth, titleY + Typography.LineHeight(TextStyles.Headline)));
        Marquee.DrawLeft("linkpearl.row." + row.Key, row.Title, textLeft, titleY, titleWidth, TextStyles.Headline,
            theme.TextStrong, titleHovering);
        var previewRight = textRight;
        if (hasUnread)
        {
            previewRight = DrawBadge(drawList, row.Unread, textRight, min.Y + 42f * scale, theme, scale);
        }

        DrawPreview(drawList, row, theme, textLeft, min.Y + 34f * scale, previewRight - textLeft, scale);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            return InboxRowAction.Menu;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, max.Y));
        return UiInteract.Click(min, max, hovered) ? InboxRowAction.Open : InboxRowAction.None;
    }

    private static void DrawAvatar(ImDrawListPtr drawList, Vector2 center, InboxRow row, PhoneTheme theme,
        LodestoneService lodestone, float scale)
    {
        var radius = AvatarRadius * scale;
        if (row.IsTell)
        {
            AvatarView.Draw(drawList, center, radius, theme.Accent, Initials.Of(row.Title), 1.2f,
                lodestone.Avatar(row.Title, row.World), 32);
            return;
        }

        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        Squircle.Fill(drawList, min, max, radius * 0.62f, ImGui.GetColorU32(Palette.WithAlpha(row.Tint, 0.22f)));
        Typography.DrawCentered(drawList, center, Initials.Of(row.Title), row.Tint, TextStyles.SubheadlineEmphasized);
    }

    private static float DrawBadge(ImDrawListPtr drawList, int unread, float right, float centerY, PhoneTheme theme,
        float scale)
    {
        var label = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var height = 18f * scale;
        var badgeWidth = MathF.Max(labelSize.X + 12f * scale, height);
        var min = new Vector2(right - badgeWidth, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(theme.Accent));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, new Vector4(1f, 1f, 1f, 1f),
            TextStyles.Caption1);
        return min.X - Metrics.Space.Sm * scale;
    }

    private static void DrawPreview(ImDrawListPtr drawList, InboxRow row, PhoneTheme theme, float left, float top,
        float width, float scale)
    {
        if (width <= 0f)
        {
            return;
        }

        var cursor = left;
        if (row.PreviewChannel.Length > 0 && GameChannels.TryByKey(row.PreviewChannel, out var channel))
        {
            var tag = LinkshellNames.Label(channel);
            var tagLabel = Typography.FitText(tag, width * 0.42f, TextStyles.Caption2);
            var tagSize = Typography.Measure(tagLabel, TextStyles.Caption2);
            var tagMin = new Vector2(cursor, top + 1f * scale);
            var tagMax = tagMin + tagSize + new Vector2(8f * scale, 3f * scale);
            Squircle.Fill(drawList, tagMin, tagMax, (tagMax.Y - tagMin.Y) * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(channel.Tint, 0.18f)));
            Typography.DrawCentered(drawList, (tagMin + tagMax) * 0.5f, tagLabel, channel.Tint, TextStyles.Caption2);
            cursor = tagMax.X + Metrics.Space.Xs * scale;
        }

        var remaining = left + width - cursor;
        if (remaining <= 0f)
        {
            return;
        }

        if (row.PreviewSender.Length > 0)
        {
            var sender = Typography.FitText(string.Concat(FirstName(row.PreviewSender), ": "), remaining * 0.5f,
                TextStyles.Caption1);
            var senderSize = Typography.Measure(sender, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(cursor, top), sender, theme.TextStrong, TextStyles.Caption1);
            cursor += senderSize.X;
            remaining = left + width - cursor;
        }

        if (remaining <= 0f)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(cursor, top), Typography.FitText(row.PreviewText, remaining,
            TextStyles.Caption1), theme.TextMuted, TextStyles.Caption1);
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }
}
