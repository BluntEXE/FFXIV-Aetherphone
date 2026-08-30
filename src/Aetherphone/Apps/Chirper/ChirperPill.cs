using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal static class ChirperPill
{
    private static readonly Vector4 ShadowInk = Palette.Darken(AppPalettes.Chirper.Accent, 0.78f);

    public static void PaintAccent(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, bool hovered) =>
        PaintAccent(drawList, min, max, rounding, hovered, 1f);

    public static void PaintAccent(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, bool hovered,
        float opacity) =>
        AccentPill.Paint(drawList, min, max, rounding, hovered, ChirperInk.Accent, ChirperInk.AccentDeep, ShadowInk,
            opacity);
}
