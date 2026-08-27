using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal enum InboxRowAction : byte
{
    None,
    Open,
    Menu,
    TogglePin,
    ToggleMute,
}

internal static class InboxRowView
{
    public const float Height = 66f;

    private const float AvatarRadius = 22f;
    private const float QuickRadius = 15f;
    private const float QuickPitch = 34f;
    private const float RevealSmoothTime = 0.10f;
    private const float MaxFrameSeconds = 0.1f;
    private const float MarkGlyphScale = 0.58f;
    private const float MarkGap = 16f;

    private static readonly Dictionary<string, Spring> Reveals = new(StringComparer.Ordinal);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static InboxRowAction Draw(in FeedCellScope cell, InboxRow row, PhoneTheme theme,
        LodestoneService lodestone, bool quickActions)
    {
        var scale = UiScale.Current;
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var hovered = cell.Hovered;
        var drawList = ImGui.GetWindowDrawList();
        var reveal = StepReveal(row.Key, hovered && quickActions);
        var avatarCenter = new Vector2(min.X + 14f * scale + AvatarRadius * scale, min.Y + Height * scale * 0.5f);
        DrawAvatar(drawList, avatarCenter, row, theme, lodestone, scale);
        var textLeft = avatarCenter.X + AvatarRadius * scale + Metrics.Space.Md * scale;
        var textRight = max.X - 14f * scale;
        var restAlpha = 1f - reveal;
        var action = InboxRowAction.None;
        var overActions = false;
        if (reveal > 0.01f)
        {
            action = DrawQuickActions(drawList, row, theme, max, min.Y + Height * scale * 0.5f, reveal, scale,
                out overActions);
        }

        var titleRight = textRight;
        if (restAlpha > 0.01f)
        {
            titleRight = DrawTrailing(drawList, row, theme, textRight, min.Y, restAlpha, scale);
        }
        else
        {
            titleRight = max.X - (QuickPitch * 3f + 8f) * scale;
        }

        var titleWidth = MathF.Max(1f, titleRight - Metrics.Space.Sm * scale - textLeft);
        var titleY = min.Y + 11f * scale;
        var titleHovering = UiInteract.Hover(new Vector2(textLeft, titleY),
            new Vector2(textLeft + titleWidth, titleY + Typography.LineHeight(TextStyles.Headline)));
        var titleInk = row.Muted ? Palette.WithAlpha(theme.TextStrong, 0.72f) : theme.TextStrong;
        Marquee.DrawLeft("linkpearl.row." + row.Key, row.Title, textLeft, titleY, titleWidth, TextStyles.Headline,
            titleInk, titleHovering);
        var previewRight = textRight;
        if (row.HasBadge && restAlpha > 0.01f)
        {
            previewRight = DrawBadge(drawList, row.Unread, textRight, min.Y + 42f * scale, theme, restAlpha, scale);
        }
        else if (row.Muted && row.Unread > 0 && restAlpha > 0.01f)
        {
            previewRight = DrawDot(drawList, textRight, min.Y + 42f * scale, theme, restAlpha, scale);
        }
        else if (reveal > 0.5f)
        {
            previewRight = titleRight;
        }

        DrawPreview(drawList, row, theme, textLeft, min.Y + 34f * scale, previewRight - textLeft, scale);
        if (action != InboxRowAction.None)
        {
            return action;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            return InboxRowAction.Menu;
        }

        return cell.Tapped && !overActions ? InboxRowAction.Open : InboxRowAction.None;
    }

    private static float StepReveal(string key, bool target)
    {
        if (!Reveals.TryGetValue(key, out var spring))
        {
            spring = default;
        }

        spring.Step(target ? 1f : 0f, RevealSmoothTime, MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds));
        Reveals[key] = spring;
        return Math.Clamp(spring.Value, 0f, 1f);
    }

    private static InboxRowAction DrawQuickActions(ImDrawListPtr drawList, InboxRow row, PhoneTheme theme,
        Vector2 max, float centerY, float reveal, float scale, out bool overActions)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        var interactive = reveal > 0.5f;
        var radius = QuickRadius * scale;
        var right = max.X - 12f * scale - radius;
        var moreCenter = new Vector2(right, centerY);
        var bellCenter = new Vector2(right - QuickPitch * scale, centerY);
        var pinCenter = new Vector2(right - QuickPitch * 2f * scale, centerY);
        var bandMin = new Vector2(pinCenter.X - radius, centerY - radius);
        var bandMax = new Vector2(moreCenter.X + radius, centerY + radius);
        overActions = interactive && UiInteract.Hover(bandMin, bandMax);
        var action = InboxRowAction.None;
        if (HoverButton.Circle(drawList, "inbox.pin." + row.Key, pinCenter, radius, FontAwesomeIcon.Thumbtack,
                AppSkin.Transparent, row.Pinned ? theme.Accent : theme.TextMuted, delta, reveal, interactive,
                Loc.T(row.Pinned ? L.Common.Unpin : L.Common.Pin), HoverLabelSide.Above))
        {
            action = InboxRowAction.TogglePin;
        }

        if (HoverButton.Circle(drawList, "inbox.mute." + row.Key, bellCenter, radius,
                row.Muted ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell, AppSkin.Transparent,
                row.Muted ? theme.Accent : theme.TextMuted, delta, reveal, interactive,
                Loc.T(row.Muted ? L.Linkpearl.Unmute : L.Linkpearl.Mute), HoverLabelSide.Above))
        {
            action = InboxRowAction.ToggleMute;
        }

        if (HoverButton.Circle(drawList, "inbox.more." + row.Key, moreCenter, radius, FontAwesomeIcon.EllipsisH,
                AppSkin.Transparent, theme.TextMuted, delta, reveal, interactive, Loc.T(L.Linkpearl.More),
                HoverLabelSide.Above))
        {
            action = InboxRowAction.Menu;
        }

        return action;
    }

    private static float DrawTrailing(ImDrawListPtr drawList, InboxRow row, PhoneTheme theme, float right,
        float top, float alpha, float scale)
    {
        var cursor = right;
        var time = row.LastActivity == default ? string.Empty : TimeText.Short(row.LastActivity);
        if (time.Length > 0)
        {
            var timeSize = Typography.Measure(time, TextStyles.Caption1);
            var timeInk = row.HasBadge ? theme.Accent : theme.TextMuted;
            Typography.Draw(drawList, new Vector2(cursor - timeSize.X, top + 13f * scale), time,
                Palette.WithAlpha(timeInk, timeInk.W * alpha), TextStyles.Caption1);
            cursor -= timeSize.X + Metrics.Space.Xs * scale;
        }

        var markY = top + 19f * scale;
        if (row.Muted)
        {
            AppSkin.Icon(drawList, new Vector2(cursor - 6f * scale, markY), FontAwesomeIcon.BellSlash.ToIconString(),
                Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * alpha), MarkGlyphScale);
            cursor -= MarkGap * scale;
        }

        if (row.Pinned)
        {
            AppSkin.Icon(drawList, new Vector2(cursor - 6f * scale, markY), FontAwesomeIcon.Thumbtack.ToIconString(),
                Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * alpha), MarkGlyphScale);
            cursor -= MarkGap * scale;
        }

        return cursor;
    }

    private static void DrawAvatar(ImDrawListPtr drawList, Vector2 center, InboxRow row, PhoneTheme theme,
        LodestoneService lodestone, float scale)
    {
        var radius = AvatarRadius * scale;
        if (row.IsTell)
        {
            AvatarView.Draw(drawList, center, radius, theme.Accent, Initials.Of(row.Title), 1.2f,
                lodestone.Avatar(row.Title, row.World, radius * 2f), 32);
            return;
        }

        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        Squircle.Fill(drawList, min, max, radius * 0.62f, ImGui.GetColorU32(Palette.WithAlpha(row.Tint, 0.22f)));
        Squircle.Stroke(drawList, min, max, radius * 0.62f, ImGui.GetColorU32(Palette.WithAlpha(row.Tint, 0.35f)),
            Metrics.Stroke.Hairline);
        Typography.DrawCentered(drawList, center, Initials.Of(row.Title), row.Tint, TextStyles.SubheadlineEmphasized);
    }

    private static float DrawBadge(ImDrawListPtr drawList, int unread, float right, float centerY, PhoneTheme theme,
        float alpha, float scale)
    {
        var label = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var height = 18f * scale;
        var badgeWidth = MathF.Max(labelSize.X + 12f * scale, height);
        var min = new Vector2(right - badgeWidth, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, alpha)));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, Palette.WithAlpha(White, alpha),
            TextStyles.Caption1);
        return min.X - Metrics.Space.Sm * scale;
    }

    private static float DrawDot(ImDrawListPtr drawList, float right, float centerY, PhoneTheme theme, float alpha,
        float scale)
    {
        var radius = 4f * scale;
        var center = new Vector2(right - radius, centerY);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.7f * alpha)), 12);
        return center.X - radius - Metrics.Space.Sm * scale;
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

        var preview = row.PreviewText.Length > 0 ? row.PreviewText : Loc.T(L.Linkpearl.NoMessagesYetPreview);
        Typography.Draw(drawList, new Vector2(cursor, top), Typography.FitText(preview, remaining,
            TextStyles.Caption1), theme.TextMuted, TextStyles.Caption1);
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }
}
