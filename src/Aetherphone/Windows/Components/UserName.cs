using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class UserName
{
    private const float GlyphFraction = 0.72f;
    private const float GapFraction = 0.34f;

    public static float Reserve(int badges, in TextStyle style, int maxBadges = 1)
    {
        var shown = Math.Min(RoleBadges.Count(badges), maxBadges);
        if (shown == 0)
        {
            return 0f;
        }

        var lineHeight = LineHeight(style);
        return shown * lineHeight * (GlyphFraction + GapFraction);
    }

    public static float Draw(string id, string name, int badges, float boxLeft, float y, float maxWidth,
        in TextStyle style, Vector4 nameInk, bool hovering, bool light, int maxBadges = 1) =>
        Draw(ImGui.GetWindowDrawList(), id, name, badges, boxLeft, y, maxWidth, style, nameInk, hovering, light, maxBadges);

    public static float Draw(string id, string name, int badges, float boxLeft, float y, float maxWidth,
        in TextStyle style, Vector4 nameInk, bool hovering, PhoneTheme theme, int maxBadges = 1) =>
        Draw(ImGui.GetWindowDrawList(), id, name, badges, boxLeft, y, maxWidth, style, nameInk, hovering,
            RoleInk.IsLight(theme), maxBadges);

    public static float Draw(ImDrawListPtr drawList, string id, string name, int badges, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, bool hovering, bool light, int maxBadges = 1)
    {
        var shown = Math.Min(RoleBadges.Count(badges), maxBadges);
        var ink = nameInk;
        if (shown > 0)
        {
            var top = RoleBadges.Top(badges);
            if (top.HasValue)
            {
                ink = RoleInk.For(top.Value.Kind, light);
            }
        }

        var reserve = Reserve(badges, style, maxBadges);
        var textWidth = MathF.Max(1f, maxWidth - reserve);
        var drawn = Marquee.DrawLeft(drawList, id, name, boxLeft, y, textWidth, style, ink, hovering);
        if (shown == 0)
        {
            return drawn;
        }

        var lineHeight = LineHeight(style);
        var glyphHeight = lineHeight * GlyphFraction;
        var gap = lineHeight * GapFraction;
        var centerY = y + lineHeight * 0.5f;
        var cursor = boxLeft + drawn + gap;

        for (var index = 0; index < shown; index++)
        {
            var badge = RoleBadges.At(badges, index);
            var center = new Vector2(cursor + glyphHeight * 0.5f, centerY);
            ProgressRing.CenterIcon(drawList, center, badge.Glyph, RoleInk.For(badge.Kind, light), glyphHeight);

            var half = glyphHeight * 0.5f;
            HoverTooltip.Show(id + ".badge." + index,
                new Rect(new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half)),
                Loc.T(badge.Tooltip));

            cursor += glyphHeight + gap;
        }

        return drawn + reserve;
    }

    public static float Draw(ImDrawListPtr drawList, string id, string name, int badges, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, bool hovering, PhoneTheme theme, int maxBadges = 1) =>
        Draw(drawList, id, name, badges, boxLeft, y, maxWidth, style, nameInk, hovering, RoleInk.IsLight(theme), maxBadges);

    public static float DrawAuto(ImDrawListPtr drawList, string id, string name, int badges, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, bool light, int maxBadges = 1)
    {
        var size = Typography.Measure(name, style);
        var hovering = UiInteract.Hover(new Vector2(boxLeft, y),
            new Vector2(boxLeft + MathF.Min(size.X, maxWidth), y + size.Y));
        return Draw(drawList, id, name, badges, boxLeft, y, maxWidth, style, nameInk, hovering, light, maxBadges);
    }

    public static float DrawAuto(ImDrawListPtr drawList, string id, string name, int badges, float boxLeft, float y,
        float maxWidth, in TextStyle style, Vector4 nameInk, PhoneTheme theme, int maxBadges = 1) =>
        DrawAuto(drawList, id, name, badges, boxLeft, y, maxWidth, style, nameInk, RoleInk.IsLight(theme), maxBadges);

    private static float LineHeight(in TextStyle style)
    {
        return Typography.Measure("A", style).Y;
    }
}
