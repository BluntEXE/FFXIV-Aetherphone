using Aetherphone.Core;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal static class ShortcutArt
{
    private static readonly Vector4 GlyphInk = new(1f, 1f, 1f, 1f);

    public static void DrawSurface(ImDrawListPtr drawList, Vector2 center, float size, ShortcutEntry shortcut,
        IDalamudTextureWrap? icon, float scale)
    {
        var half = size * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        var radius = size * 0.26f;
        var surface = IconTile.Surface(ShortcutTint.Resolve(shortcut.Tint));
        Elevation.IconRest(drawList, min, max, radius, scale);
        IconTile.FillShaded(drawList, min, max, radius, surface);
        Material.EdgeSquircle(drawList, min, max, radius, scale);
        DrawContent(drawList, center, size, shortcut, icon);
    }

    public static void DrawContent(ImDrawListPtr drawList, Vector2 center, float size, ShortcutEntry shortcut,
        IDalamudTextureWrap? icon)
    {
        if (icon is not null)
        {
            var inset = size * 0.21f;
            var half = size * 0.5f - inset;
            drawList.AddImageRounded(icon.Handle, new Vector2(center.X - half, center.Y - half),
                new Vector2(center.X + half, center.Y + half), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, half * 0.42f,
                ImDrawFlags.RoundCornersAll);
            return;
        }

        if (shortcut.Glyph != 0)
        {
            ProgressRing.CenterIcon(drawList, center, (FontAwesomeIcon)shortcut.Glyph, GlyphInk, size * 0.42f);
            return;
        }

        var monogram = ShortcutStore.Monogram(shortcut.Name);
        var measured = Typography.Measure(monogram, TextStyles.Title1);
        var glyphScale = measured.Y > 0f ? size * 0.44f / measured.Y : 1f;
        Typography.DrawCentered(drawList, center, monogram, GlyphInk, TextStyles.Title1.Scale * glyphScale,
            FontWeight.SemiBold);
    }
}
