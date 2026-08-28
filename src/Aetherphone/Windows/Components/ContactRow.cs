using Aetherphone.Core;
using Aetherphone.Core.Contacts;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ContactRow
{
    public const float Height = 58f;

    public static bool Draw(in FeedCellScope cell, FriendEntry friend, PhoneTheme theme,
        LodestoneService lodestone)
    {
        var scale = UiScale.Current;
        var dl = ImGui.GetWindowDrawList();
        var row = cell.Bounds;
        var inset = FeedCell.PadX * scale;
        var avatarRadius = 17f * scale;
        var avatarCenter = new Vector2(row.Min.X + inset + avatarRadius, row.Center.Y);
        var baseColor = friend.Online ? theme.Accent : theme.SurfaceMuted;
        AvatarView.Draw(dl, avatarCenter, avatarRadius, baseColor, Initials.Of(friend.Name), 0.95f,
            lodestone.Avatar(friend.Name, friend.WorldName, avatarRadius * 2f), 32);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var nameColor = friend.Online ? theme.TextStrong : Palette.WithAlpha(theme.TextStrong, 0.5f);
        var subtitle = Subtitle(friend);
        var subtitleRight = row.Max.X - inset - (friend.Online ? 16f * scale : 0f);
        var textMaxWidth = subtitleRight - textLeft;
        var nameY = row.Min.Y + 9f * scale;
        var nameSize = Typography.Measure(friend.Name, TextStyles.Headline);
        var nameHovering = UiInteract.Hover(new Vector2(textLeft, nameY),
            new Vector2(textLeft + textMaxWidth, nameY + nameSize.Y));
        Marquee.DrawLeft(new MarqueeId("contactrow.name.", friend.Name), friend.Name, textLeft, nameY,
            textMaxWidth, TextStyles.Headline, nameColor, nameHovering);
        var subtitleY = row.Min.Y + 30f * scale;
        var subtitleSize = Typography.Measure(subtitle, TextStyles.Subheadline);
        var subtitleHovering = UiInteract.Hover(new Vector2(textLeft, subtitleY),
            new Vector2(textLeft + textMaxWidth, subtitleY + subtitleSize.Y));
        Marquee.DrawLeft(new MarqueeId("contactrow.subtitle.", friend.Name), subtitle, textLeft, subtitleY,
            textMaxWidth, TextStyles.Subheadline, theme.TextMuted, subtitleHovering);
        if (friend.Online)
        {
            var dotCenter = new Vector2(row.Max.X - inset - 7f * scale, row.Center.Y);
            dl.AddCircleFilled(dotCenter, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.ToggleOn, 0.22f)), 20);
            dl.AddCircleFilled(dotCenter, 5f * scale, ImGui.GetColorU32(theme.ToggleOn), 16);
            dl.AddCircleFilled(dotCenter - new Vector2(1.4f * scale, 1.4f * scale), 1.6f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f)), 8);
        }

        return cell.Tapped;
    }

    private static string Subtitle(FriendEntry friend)
    {
        if (!friend.Online)
        {
            return friend.WorldName;
        }

        return friend.Location.Length > 0 ? $"{friend.WorldName} · {friend.Location}" : friend.WorldName;
    }
}
