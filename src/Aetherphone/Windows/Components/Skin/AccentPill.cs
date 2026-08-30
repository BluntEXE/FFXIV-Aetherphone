using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AccentPill
{
    private const int ShadowLayers = 5;
    private const float ShadowAlpha = 0.13f;
    private const float ShadowSpread = 1.1f;
    private const float ShadowDrop = 1.4f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static void Paint(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, bool hovered,
        Vector4 accent, Vector4 accentDeep, Vector4 shadowInk, float opacity = 1f)
    {
        var scale = UiScale.Current;
        for (var layer = ShadowLayers - 1; layer >= 0; layer--)
        {
            var grow = layer * ShadowSpread * scale;
            var drop = (1f + layer * ShadowDrop) * scale;
            var falloff = 1f - layer / (float)ShadowLayers;
            var alpha = ShadowAlpha * falloff * falloff * opacity;
            Squircle.Fill(drawList, new Vector2(min.X - grow, min.Y - grow + drop),
                new Vector2(max.X + grow, max.Y + grow + drop), rounding + grow,
                ImGui.GetColorU32(Palette.WithAlpha(shadowInk, alpha)));
        }

        var topColor = hovered ? Palette.Mix(accent, White, 0.10f) : accent;
        var bottomColor = hovered ? Palette.Mix(accentDeep, White, 0.06f) : accentDeep;
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(topColor, opacity)),
            ImGui.GetColorU32(Palette.WithAlpha(bottomColor, opacity)));
    }
}
