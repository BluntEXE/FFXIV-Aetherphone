using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal static class ChirperPill
{
    private const int ShadowLayers = 5;
    private const float ShadowAlpha = 0.13f;
    private const float ShadowSpread = 1.1f;
    private const float ShadowDrop = 1.4f;

    private static readonly Vector4 ShadowInk = Palette.Darken(AppPalettes.Chirper.Accent, 0.78f);

    public static void PaintAccent(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, bool hovered)
    {
        var scale = UiScale.Current;
        for (var layer = ShadowLayers - 1; layer >= 0; layer--)
        {
            var grow = layer * ShadowSpread * scale;
            var drop = (1f + layer * ShadowDrop) * scale;
            var falloff = 1f - layer / (float)ShadowLayers;
            var alpha = ShadowAlpha * falloff * falloff;
            Squircle.Fill(drawList, new Vector2(min.X - grow, min.Y - grow + drop),
                new Vector2(max.X + grow, max.Y + grow + drop), rounding + grow,
                ImGui.GetColorU32(Palette.WithAlpha(ShadowInk, alpha)));
        }

        var topColor = hovered ? Palette.Mix(ChirperInk.Accent, ChirperInk.White, 0.10f) : ChirperInk.Accent;
        var bottomColor = hovered ? Palette.Mix(ChirperInk.AccentDeep, ChirperInk.White, 0.06f) : ChirperInk.AccentDeep;
        Squircle.FillVerticalGradient(drawList, min, max, rounding, ImGui.GetColorU32(topColor),
            ImGui.GetColorU32(bottomColor));
    }
}
