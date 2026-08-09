using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class SpinRingArt
{
    private static readonly Vector4 SmallFill = new(0.129f, 0.298f, 0.271f, 1f);
    private static readonly Vector4 MidFill = new(0.208f, 0.451f, 0.400f, 1f);
    private static readonly Vector4 TopFill = new(0.925f, 0.745f, 0.318f, 1f);
    private static readonly Vector4 FeltDark = new(0.035f, 0.070f, 0.059f, 1f);
    private static readonly Vector4 HubFill = new(0.062f, 0.105f, 0.094f, 1f);
    private static readonly Vector4 Brass = new(0.85f, 0.72f, 0.42f, 1f);
    private static readonly Vector4 LightInk = new(0.96f, 0.97f, 0.98f, 1f);

    private const int WedgeSegments = 12;
    private const float LabelRadiusFactor = 0.74f;
    private const float LabelPadding = 3f;

    public static float SegmentSpan => WheelChoreography.SpanFor(DailySpinRules.SegmentCount);

    public static Vector4 FillFor(int segment)
    {
        var award = DailySpinRules.AwardOf(segment);
        if (award >= DailySpinRules.TopAward)
        {
            return TopFill;
        }

        return award > 5 ? MidFill : SmallFill;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, float rotation,
        int highlightSegment, float highlightGlow, float scale)
    {
        drawList.AddCircleFilled(center, radius + 7f * scale, ImGui.GetColorU32(FeltDark), 64);
        drawList.AddCircle(center, radius + 7f * scale, ImGui.GetColorU32(Palette.WithAlpha(Brass, 0.55f)), 64,
            1.5f * scale);

        var span = SegmentSpan;
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            var fill = FillFor(segment);
            if (segment == highlightSegment && highlightGlow > 0f)
            {
                fill = Vector4.Lerp(fill, LightInk, 0.42f * highlightGlow);
            }

            var centreAngle = rotation + segment * span;
            var from = centreAngle - span * 0.5f - MathF.PI * 0.5f;
            var to = centreAngle + span * 0.5f - MathF.PI * 0.5f;
            drawList.PathClear();
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, from, to, WedgeSegments);
            drawList.PathFillConvex(ImGui.GetColorU32(fill));
        }

        DrawLabels(drawList, center, radius * LabelRadiusFactor, rotation, highlightSegment, highlightGlow, scale);
        drawList.AddCircleFilled(center, radius * 0.32f, ImGui.GetColorU32(HubFill), 48);
        drawList.AddCircle(center, radius * 0.32f, ImGui.GetColorU32(Palette.WithAlpha(Brass, 0.45f)), 48,
            1.2f * scale);
    }

    private static void DrawLabels(ImDrawListPtr drawList, Vector2 center, float labelRadius, float rotation,
        int highlightSegment, float highlightGlow, float scale)
    {
        var chord = 2f * labelRadius * MathF.Sin(SegmentSpan * 0.5f);
        var span = SegmentSpan;
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            var label = GameNumber.Label((int)DailySpinRules.AwardOf(segment));
            if (Typography.Measure(label, TextStyles.Caption2).X + LabelPadding * 2f * scale > chord)
            {
                continue;
            }

            var fill = FillFor(segment);
            if (segment == highlightSegment && highlightGlow > 0f)
            {
                fill = Vector4.Lerp(fill, LightInk, 0.42f * highlightGlow);
            }

            var at = center + WheelRingArt.Direction(rotation + segment * span) * labelRadius;
            Typography.DrawCentered(drawList, at, label, WheelRingArt.InkOn(fill), TextStyles.Caption2);
        }
    }
}
