using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

// The playing surface every table and cabinet sits on: the games arena with a lamp pool poured over
// its middle and a stitch line run round the inside. The pool is painted with stacked translucent
// discs rather than a texture, because a textured fill inside a path is the one thing this draw
// list cannot do without dragging the whole frame's texture binding along with it.
internal static class FeltPanel
{
    public const float RoundingUnits = 26f;

    private const int PoolLayers = 6;
    private const float PoolRadiusFraction = 0.62f;
    private const float PoolAlpha = 0.05f;
    private const float PoolHeightFraction = 0.38f;
    private const float StitchInset = 10f;

    private static readonly Vector4 Lamp = new(1f, 0.96f, 0.86f, 1f);
    private static readonly Vector4 StitchInk = new(1f, 1f, 1f, 0.09f);

    public static float RoundingFor(float scale)
    {
        return RoundingUnits * scale;
    }

    public static void Draw(ImDrawListPtr drawList, in Rect rect, Vector4 accent, float scale)
    {
        var rounding = RoundingFor(scale);
        GameScene.Arena(drawList, rect, rounding, scale, accent);
        DrawLampPool(drawList, rect);
        DrawStitch(drawList, rect, rounding, scale);
    }

    private static void DrawLampPool(ImDrawListPtr drawList, in Rect rect)
    {
        var center = new Vector2(rect.Center.X, rect.Min.Y + rect.Height * PoolHeightFraction);
        var span = MathF.Min(rect.Width, rect.Height) * PoolRadiusFraction;
        for (var layer = PoolLayers; layer >= 1; layer--)
        {
            var radius = span * (layer / (float)PoolLayers);
            var alpha = PoolAlpha * (PoolLayers - layer + 1) / PoolLayers;
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(Lamp, alpha)), 48);
        }
    }

    private static void DrawStitch(ImDrawListPtr drawList, in Rect rect, float rounding, float scale)
    {
        var inset = new Vector2(StitchInset * scale, StitchInset * scale);
        Squircle.Stroke(drawList, rect.Min + inset, rect.Max - inset, rounding - inset.X,
            ImGui.GetColorU32(StitchInk), Metrics.Stroke.Thin * scale);
    }
}
