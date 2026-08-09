using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

// Fifty wedges on a phone rim leave about ten pixels of arc apiece, which is not a number, so the
// wheel is read by colour first: one hue per bet spot, and the spot cards under it carry the same
// swatches as the legend. A multiplier is only ever painted onto a wedge when the measured glyph
// actually fits inside that wedge's chord, so the labels appear as the phone is zoomed and never
// crowd into each other at the size where they would not fit.
internal static class WheelRingArt
{
    public static readonly Vector4[] SpotColors =
    {
        new(0.169f, 0.298f, 0.337f, 1f),
        new(0.212f, 0.392f, 0.639f, 1f),
        new(0.451f, 0.310f, 0.663f, 1f),
        new(0.808f, 0.451f, 0.208f, 1f),
        new(0.925f, 0.745f, 0.318f, 1f),
    };

    private static readonly Vector4 FeltDark = new(0.035f, 0.070f, 0.059f, 1f);
    private static readonly Vector4 HubFill = new(0.062f, 0.105f, 0.094f, 1f);
    private static readonly Vector4 LightInk = new(0.96f, 0.97f, 0.98f, 1f);
    private static readonly Vector4 DarkInk = new(0.09f, 0.10f, 0.08f, 1f);
    private static readonly Vector4 Brass = new(0.85f, 0.72f, 0.42f, 1f);

    private const int WedgeSegments = 6;
    private const float LabelRadiusFactor = 0.80f;
    private const float LabelPadding = 3f;

    public static Vector4 InkOn(Vector4 fill)
    {
        var luminance = fill.X * 0.2126f + fill.Y * 0.7152f + fill.Z * 0.0722f;
        return luminance > 0.55f ? DarkInk : LightInk;
    }

    public static Vector2 Direction(float angle)
    {
        return new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
    }

    private static float ImGuiAngle(float angle)
    {
        return angle - MathF.PI * 0.5f;
    }

    public static bool LabelFits(string label, float labelRadius, float scale)
    {
        var chord = 2f * labelRadius * MathF.Sin(WheelChoreography.SegmentSpan * 0.5f);
        return Typography.Measure(label, TextStyles.Caption2).X + LabelPadding * 2f * scale <= chord;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, float rotation,
        int highlightSegment, float highlightGlow, float scale)
    {
        drawList.AddCircleFilled(center, radius + 7f * scale, ImGui.GetColorU32(FeltDark), 64);
        drawList.AddCircle(center, radius + 7f * scale, ImGui.GetColorU32(Palette.WithAlpha(Brass, 0.55f)), 64,
            1.5f * scale);

        var labelRadius = radius * LabelRadiusFactor;
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            var spot = WheelRules.Segments[segment];
            var fill = SpotColors[spot];
            if (segment == highlightSegment && highlightGlow > 0f)
            {
                fill = Vector4.Lerp(fill, LightInk, 0.42f * highlightGlow);
            }

            var centreAngle = rotation + segment * WheelChoreography.SegmentSpan;
            var from = ImGuiAngle(centreAngle - WheelChoreography.SegmentSpan * 0.5f);
            var to = ImGuiAngle(centreAngle + WheelChoreography.SegmentSpan * 0.5f);
            drawList.PathClear();
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, from, to, WedgeSegments);
            drawList.PathFillConvex(ImGui.GetColorU32(fill));
        }

        DrawTicks(drawList, center, radius, rotation, scale);
        DrawLabels(drawList, center, labelRadius, rotation, highlightSegment, highlightGlow, scale);
        drawList.AddCircleFilled(center, radius * 0.34f, ImGui.GetColorU32(HubFill), 48);
        drawList.AddCircle(center, radius * 0.34f, ImGui.GetColorU32(Palette.WithAlpha(Brass, 0.45f)), 48,
            1.2f * scale);
    }

    private static void DrawTicks(ImDrawListPtr drawList, Vector2 center, float radius, float rotation, float scale)
    {
        var tint = ImGui.GetColorU32(Palette.WithAlpha(FeltDark, 0.75f));
        var inner = radius * 0.36f;
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            var boundary = rotation + (segment - 0.5f) * WheelChoreography.SegmentSpan;
            var direction = Direction(boundary);
            drawList.AddLine(center + direction * inner, center + direction * radius, tint, 1f * scale);
        }
    }

    private static void DrawLabels(ImDrawListPtr drawList, Vector2 center, float labelRadius, float rotation,
        int highlightSegment, float highlightGlow, float scale)
    {
        Span<bool> fits = stackalloc bool[WheelRules.SpotCount];
        var anyFits = false;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            fits[spot] = LabelFits(GameNumber.Label(WheelRules.Multipliers[spot]), labelRadius, scale);
            anyFits |= fits[spot];
        }

        if (!anyFits)
        {
            return;
        }

        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            var spot = WheelRules.Segments[segment];
            if (!fits[spot])
            {
                continue;
            }

            var label = GameNumber.Label(WheelRules.Multipliers[spot]);
            var fill = SpotColors[spot];
            if (segment == highlightSegment && highlightGlow > 0f)
            {
                fill = Vector4.Lerp(fill, LightInk, 0.42f * highlightGlow);
            }

            var at = center + Direction(rotation + segment * WheelChoreography.SegmentSpan) * labelRadius;
            Typography.DrawCentered(drawList, at, label, InkOn(fill), TextStyles.Caption2);
        }
    }

    public static void DrawPointer(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        var tip = new Vector2(center.X, center.Y - radius + 5f * scale);
        var width = 7f * scale;
        var back = new Vector2(center.X, center.Y - radius - 13f * scale);
        drawList.AddTriangleFilled(tip, new Vector2(back.X - width, back.Y), new Vector2(back.X + width, back.Y),
            ImGui.GetColorU32(Brass));
        drawList.AddCircleFilled(new Vector2(back.X, back.Y + 2f * scale), 4f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(LightInk, 0.85f)), 16);
    }

    public static void DrawCountdown(ImDrawListPtr drawList, Vector2 center, float radius, float fraction,
        Vector4 color, float scale)
    {
        var track = ImGui.GetColorU32(Palette.WithAlpha(color, 0.16f));
        drawList.AddCircle(center, radius, track, 96, 3f * scale);
        if (fraction <= 0f)
        {
            return;
        }

        if (fraction > 1f)
        {
            fraction = 1f;
        }

        var steps = Math.Max(4, (int)MathF.Ceiling(fraction * 96f));
        var tint = ImGui.GetColorU32(color);
        var previous = center + Direction(0f) * radius;
        for (var step = 1; step <= steps; step++)
        {
            var angle = fraction * WheelChoreography.Tau * (step / (float)steps);
            var next = center + Direction(angle) * radius;
            drawList.AddLine(previous, next, tint, 3f * scale);
            previous = next;
        }
    }
}
