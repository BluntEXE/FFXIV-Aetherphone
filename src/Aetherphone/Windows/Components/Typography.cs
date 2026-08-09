using System.Text;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class Typography
{
    private const string Ellipsis = "…";

    private static readonly Vector2[] HaloOffsets =
    {
        new(1f, 0f),
        new(-1f, 0f),
        new(0f, 1f),
        new(0f, -1f),
        new(0.7071f, 0.7071f),
        new(-0.7071f, 0.7071f),
        new(0.7071f, -0.7071f),
        new(-0.7071f, -0.7071f),
    };

    private static readonly Dictionary<(string Text, float Width, float FontSize), string> FitCache = new();

    private readonly struct WrapEntry
    {
        public readonly float Width;
        public readonly float FontSize;
        public readonly string[] Lines;

        public WrapEntry(float width, float fontSize, string[] lines)
        {
            Width = width;
            FontSize = fontSize;
            Lines = lines;
        }
    }

    private static readonly Dictionary<string, WrapEntry> WrapCache = new();

    private static int cacheGeneration;

    private static void InvalidateCachesOnFontChange()
    {
        var current = Plugin.Fonts.Generation;
        if (current == cacheGeneration)
        {
            return;
        }

        cacheGeneration = current;
        FitCache.Clear();
        WrapCache.Clear();
        FitScaleCache.Clear();
    }

    public static void Plain(string text)
    {
        Plugin.Fonts.NoticeText(text);
        ImGui.TextUnformatted(text);
    }

    public static void Wrapped(string text)
    {
        Plugin.Fonts.NoticeText(text);
        ImGui.TextWrapped(text);
    }

    public static WrapScope WrapAt(float screenRight) => new(screenRight);

    public readonly struct WrapScope : IDisposable
    {
        public WrapScope(float screenRight)
        {
            ImGui.PushTextWrapPos(screenRight - ImGui.GetWindowPos().X);
        }

        public void Dispose()
        {
            ImGui.PopTextWrapPos();
        }
    }

    public static Vector2 Measure(string text, float scale = 1f) => Measure(text, scale, FontWeight.Regular);
    public static Vector2 Measure(string text, in TextStyle style) => Measure(text, style.Scale, style.Weight);

    public static Vector2 Measure(string text, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            return ImGui.CalcTextSize(text);
        }
    }

    public static float MeasureWrapped(string text, float wrapWidth, float fontScale,
        FontWeight weight = FontWeight.Regular)
    {
        using (Plugin.Fonts.Push(fontScale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            return ImGui.CalcTextSize(text, false, wrapWidth).Y;
        }
    }

    public static float LineHeight(in TextStyle style)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            return ImGui.GetTextLineHeightWithSpacing();
        }
    }

    public static Vector2 MeasureWrappedBlock(string text, in TextStyle style, float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var lines = WrapLines(text, maxWidth);
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var width = 0f;
            for (var index = 0; index < lines.Length; index++)
            {
                width = MathF.Max(width, ImGui.CalcTextSize(lines[index]).X);
            }

            return new Vector2(width, lines.Length * lineHeight);
        }
    }

    public static void Draw(Vector2 position, string text, Vector4 color, float scale = 1f) =>
        Draw(position, text, color, scale, FontWeight.Regular);

    public static void Draw(Vector2 position, string text, Vector4 color, in TextStyle style) =>
        Draw(position, text, color, style.Scale, style.Weight);

    public static void Draw(Vector2 position, string text, Vector4 color, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            ImGui.SetCursorScreenPos(position);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }
        }
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float scale = 1f) =>
        Draw(drawList, position, text, color, scale, FontWeight.Regular);

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color,
        in TextStyle style) =>
        Draw(drawList, position, text, color, style.Scale, style.Weight);

    public static void DrawCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        in TextStyle style) =>
        DrawCentered(drawList, center, text, color, style.Scale, style.Weight);

    public static string FitText(string text, float maxWidth, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            return Fit(text, maxWidth);
        }
    }

    public static string FitText(string text, float maxWidth, in TextStyle style) =>
        FitText(text, maxWidth, style.Scale, style.Weight);

    private static readonly Dictionary<(string Text, float MaxWidth, float MaxScale, float MinScale, FontWeight Weight,
        float FontSize), float> FitScaleCache = new();

    public static float FitScale(string text, float maxWidth, float maxScale, float minScale, FontWeight weight)
    {
        if (maxWidth <= 0f)
        {
            return minScale;
        }

        InvalidateCachesOnFontChange();
        var fontSize = ImGui.GetFontSize();
        var key = (text, maxWidth, maxScale, minScale, weight, fontSize);
        if (FitScaleCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = minScale;
        for (var scale = maxScale; scale > minScale; scale -= 0.05f)
        {
            using (Plugin.Fonts.Push(scale, weight))
            {
                Plugin.Fonts.NoticeText(text);
                if (ImGui.CalcTextSize(text).X <= maxWidth)
                {
                    result = scale;
                    break;
                }
            }
        }

        FitScaleCache[key] = result;
        return result;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float scale,
        FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), position, ImGui.GetColorU32(color), text);
        }
    }

    private const float SweepBandFraction = 0.34f;
    private const float GlintBandFraction = 0.16f;
    private const float GlintActiveFraction = 0.30f;
    private const float FrostBandFraction = 0.52f;
    private const float RippleCrests = 2.5f;
    private const float WaveSpan = 1.0f;
    private const int GradientSlices = 12;
    private const float EmberSlowCrests = 1.7f;
    private const float EmberFastCrests = 4.3f;
    private const float EmberFastWeight = 0.40f;
    private const float EmberFastRate = 1.6f;
    private const float AuroraCounterCrests = 0.6f;
    private const float AuroraCounterRate = 0.7f;
    private const float PrismSpan = 2.0f;
    private const float GlitchActiveFraction = 0.18f;
    private const int GlitchSteps = 8;
    private const int GlitchBands = 3;
    private const float GlitchShift = 2.4f;
    private const float GlitchAlpha = 0.85f;
    private const int EclipseHaloPoints = 8;
    private const float EclipseHaloRadius = 1.6f;
    private const float EclipseHaloAlpha = 0.20f;
    private const float StarfallRiseFraction = 0.28f;
    private const float StarfallStagger = 0.19f;
    private const float StarfallRadiusScale = 0.065f;
    private const float RimAlpha = 0.26f;
    private const float RimOffset = 1f;
    private const float ThumpWidth = 0.12f;
    private const float ThumpEchoAt = 0.18f;
    private const float ThumpEchoWeight = 0.6f;

    private readonly record struct SweepTier(float WidthScale, float AlphaScale);

    private static readonly SweepTier[] SweepTiers =
    {
        new(1.00f, 0.28f),
        new(0.58f, 0.58f),
        new(0.26f, 1.00f),
    };

    private static readonly float[] SparkOffsetsX = { 0.12f, 0.34f, 0.58f, 0.76f, 0.91f };

    private static readonly float[] SparkOffsetsY = { 0.18f, 0.66f, 0.10f, 0.74f, 0.36f };

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, in TextStyle style,
        in TextEffect effect)
    {
        if (effect.Kind == NameEffectKind.None)
        {
            Draw(drawList, position, text, color, style.Scale, style.Weight);
            return;
        }

        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var size = ImGui.CalcTextSize(text);
            if (effect.Kind == NameEffectKind.Breath || effect.Kind == NameEffectKind.Heartbeat)
            {
                var beat = effect.Kind == NameEffectKind.Heartbeat ? Thump(effect.Phase) : Wave(effect.Phase);
                var lit = Vector4.Lerp(color, effect.Crest, beat);
                drawList.AddText(font, fontSize, position, ImGui.GetColorU32(lit), text);
                return;
            }

            if (effect.Kind == NameEffectKind.Eclipse)
            {
                DrawHalo(drawList, font, fontSize, position, text, effect);
                drawList.AddText(font, fontSize, position, ImGui.GetColorU32(color), text);
                return;
            }

            drawList.AddText(font, fontSize, position, ImGui.GetColorU32(color), text);
            var top = position.Y - size.Y * 0.5f;
            var bottom = position.Y + size.Y * 1.5f;
            if (effect.Kind == NameEffectKind.Frost)
            {
                DrawRim(drawList, font, fontSize, position, text, effect);
                DrawCrest(drawList, font, fontSize, position, text, size.X, effect, top, bottom);
                return;
            }

            if (effect.Kind == NameEffectKind.Sweep || effect.Kind == NameEffectKind.Glint)
            {
                DrawCrest(drawList, font, fontSize, position, text, size.X, effect, top, bottom);
                return;
            }

            if (effect.Kind == NameEffectKind.Glitch)
            {
                DrawTear(drawList, font, fontSize, position, text, size, effect);
                return;
            }

            if (effect.Kind == NameEffectKind.Starfall)
            {
                DrawSparks(drawList, position, size, fontSize, effect);
                return;
            }

            DrawSlices(drawList, font, fontSize, position, text, size.X, color, effect, top, bottom);
        }
    }

    private static void DrawCrest(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 position,
        string text, float width, in TextEffect effect, float top, float bottom)
    {
        var glint = effect.Kind == NameEffectKind.Glint;
        var travel = effect.Phase;
        if (glint)
        {
            if (effect.Phase >= GlintActiveFraction)
            {
                return;
            }

            travel = effect.Phase / GlintActiveFraction;
        }

        var fraction = effect.Kind switch
        {
            NameEffectKind.Glint => GlintBandFraction,
            NameEffectKind.Frost => FrostBandFraction,
            _ => SweepBandFraction,
        };

        var band = MathF.Max(1f, width * fraction);
        var center = position.X - band * 0.5f + (width + band) * travel;
        for (var tierIndex = 0; tierIndex < SweepTiers.Length; tierIndex++)
        {
            var tier = SweepTiers[tierIndex];
            var halfWidth = band * tier.WidthScale * 0.5f;
            var crest = new Vector4(effect.Crest.X, effect.Crest.Y, effect.Crest.Z,
                effect.Crest.W * tier.AlphaScale);
            drawList.PushClipRect(new Vector2(center - halfWidth, top), new Vector2(center + halfWidth, bottom), true);
            drawList.AddText(font, fontSize, position, ImGui.GetColorU32(crest), text);
            drawList.PopClipRect();
        }
    }

    private static void DrawRim(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 position,
        string text, in TextEffect effect)
    {
        var rim = new Vector4(effect.Crest.X, effect.Crest.Y, effect.Crest.Z, effect.Crest.W * RimAlpha);
        var packed = ImGui.GetColorU32(rim);
        drawList.AddText(font, fontSize, position + new Vector2(RimOffset, 0f), packed, text);
        drawList.AddText(font, fontSize, position + new Vector2(-RimOffset, 0f), packed, text);
        drawList.AddText(font, fontSize, position + new Vector2(0f, RimOffset), packed, text);
        drawList.AddText(font, fontSize, position + new Vector2(0f, -RimOffset), packed, text);
    }

    private static void DrawHalo(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 position,
        string text, in TextEffect effect)
    {
        var breathe = 0.65f + 0.35f * Wave(effect.Phase);
        var radius = EclipseHaloRadius * breathe;
        var glow = new Vector4(effect.Crest.X, effect.Crest.Y, effect.Crest.Z,
            effect.Crest.W * EclipseHaloAlpha * breathe);
        var packed = ImGui.GetColorU32(glow);
        for (var pointIndex = 0; pointIndex < EclipseHaloPoints; pointIndex++)
        {
            var angle = pointIndex / (float)EclipseHaloPoints * MathF.Tau;
            var offset = new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            drawList.AddText(font, fontSize, position + offset, packed, text);
        }
    }

    private static void DrawTear(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 position,
        string text, Vector2 size, in TextEffect effect)
    {
        if (effect.Phase < 1f - GlitchActiveFraction)
        {
            return;
        }

        var step = (int)(effect.Phase * GlitchSteps);
        var bandHeight = size.Y / GlitchBands;
        var leading = new Vector4(effect.Ramp.Start.X, effect.Ramp.Start.Y, effect.Ramp.Start.Z, GlitchAlpha);
        var trailing = new Vector4(effect.Ramp.Quarter.X, effect.Ramp.Quarter.Y, effect.Ramp.Quarter.Z, GlitchAlpha);
        var leadingPacked = ImGui.GetColorU32(leading);
        var trailingPacked = ImGui.GetColorU32(trailing);
        for (var bandIndex = 0; bandIndex < GlitchBands; bandIndex++)
        {
            var shift = Jitter(step, bandIndex) * GlitchShift;
            var bandTop = position.Y + bandHeight * bandIndex;
            drawList.PushClipRect(new Vector2(position.X - GlitchShift, bandTop),
                new Vector2(position.X + size.X + GlitchShift, bandTop + bandHeight), true);
            drawList.AddText(font, fontSize, position + new Vector2(shift, 0f), leadingPacked, text);
            drawList.AddText(font, fontSize, position + new Vector2(-shift, 0f), trailingPacked, text);
            drawList.PopClipRect();
        }
    }

    private static void DrawSparks(ImDrawListPtr drawList, Vector2 position, Vector2 size, float fontSize,
        in TextEffect effect)
    {
        var radius = fontSize * StarfallRadiusScale;
        for (var sparkIndex = 0; sparkIndex < SparkOffsetsX.Length; sparkIndex++)
        {
            var local = Fraction(effect.Phase + sparkIndex * StarfallStagger);
            var start = 1f - StarfallRiseFraction;
            if (local < start)
            {
                continue;
            }

            var glow = MathF.Sin((local - start) / StarfallRiseFraction * MathF.PI);
            if (glow <= 0.01f)
            {
                continue;
            }

            var center = new Vector2(
                position.X + size.X * SparkOffsetsX[sparkIndex],
                position.Y + size.Y * SparkOffsetsY[sparkIndex]);
            var tint = new Vector4(effect.Crest.X, effect.Crest.Y, effect.Crest.Z, effect.Crest.W * glow);
            drawList.AddCircleFilled(center, radius * (0.6f + 0.6f * glow), ImGui.GetColorU32(tint));
        }
    }

    private static void DrawSlices(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 position,
        string text, float width, Vector4 color, in TextEffect effect, float top, float bottom)
    {
        for (var sliceIndex = 0; sliceIndex < GradientSlices; sliceIndex++)
        {
            var left = position.X + width * sliceIndex / GradientSlices;
            var right = position.X + width * (sliceIndex + 1) / GradientSlices;
            var center = (sliceIndex + 0.5f) / GradientSlices;
            var tint = effect.Kind switch
            {
                NameEffectKind.Wave => effect.Ramp.Sample(center * WaveSpan - effect.Phase),
                NameEffectKind.Prism => effect.Ramp.Sample(center * PrismSpan - effect.Phase * PrismSpan),
                NameEffectKind.Aurora => Vector4.Lerp(
                    effect.Ramp.Sample(center - effect.Phase),
                    effect.Ramp.Sample(center * AuroraCounterCrests + effect.Phase * AuroraCounterRate),
                    0.5f),
                _ => Vector4.Lerp(color, effect.Crest, SliceFactor(effect.Kind, center, effect.Phase)),
            };

            drawList.PushClipRect(new Vector2(left, top), new Vector2(right, bottom), true);
            drawList.AddText(font, fontSize, position, ImGui.GetColorU32(tint), text);
            drawList.PopClipRect();
        }
    }

    private static float SliceFactor(NameEffectKind kind, float center, float phase)
    {
        return kind switch
        {
            NameEffectKind.Flow => Triangle(center + phase),
            NameEffectKind.Ripple => Wave(center * RippleCrests + phase),
            NameEffectKind.Ember => Ember(center, phase),
            _ => center,
        };
    }

    private static float Ember(float center, float phase)
    {
        var slow = Wave(center * EmberSlowCrests + phase);
        var fast = Wave(center * EmberFastCrests - phase * EmberFastRate);
        return slow * (1f - EmberFastWeight) + fast * EmberFastWeight;
    }

    private static float Thump(float phase)
    {
        var wrapped = Fraction(phase);
        return MathF.Min(1f, Spike(wrapped, 0f) + Spike(wrapped, ThumpEchoAt) * ThumpEchoWeight);
    }

    private static float Spike(float phase, float at)
    {
        var delta = phase - at;
        if (delta < 0f || delta > ThumpWidth)
        {
            return 0f;
        }

        return MathF.Sin(delta / ThumpWidth * MathF.PI);
    }

    private static float Jitter(int step, int bandIndex)
    {
        var hash = (uint)((step * 73856093) ^ ((bandIndex + 1) * 19349663));
        hash ^= hash >> 13;
        hash *= 2654435761u;
        hash ^= hash >> 16;
        return ((hash % 2000u) / 1000f) - 1f;
    }

    private static float Wave(float phase) => (MathF.Sin(phase * MathF.Tau) + 1f) * 0.5f;

    private static float Fraction(float phase) => phase - MathF.Floor(phase);

    private static float Triangle(float phase) => 1f - MathF.Abs(Fraction(phase) * 2f - 1f);

    public static void DrawCentered(Vector2 center, string text, Vector4 color, float scale = 1f) =>
        DrawCentered(center, text, color, scale, FontWeight.Regular);

    public static void DrawCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        float scale = 1f) =>
        DrawCentered(drawList, center, text, color, scale, FontWeight.Regular);

    public static void DrawCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color, float scale,
        FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            var size = ImGui.CalcTextSize(text);
            var wrapWidth = AutoWrapWidth(center.X);
            if (wrapWidth > 0f && size.X > wrapWidth)
            {
                DrawWrappedBlock(drawList, center, text, color, wrapWidth);
                return;
            }

            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), center - size * 0.5f, ImGui.GetColorU32(color),
                text);
        }
    }

    private static float AutoWrapWidth(float centerX)
    {
        var windowLeft = ImGui.GetWindowPos().X;
        var margin = 8f * UiScale.Current;
        var left = windowLeft + ImGui.GetWindowContentRegionMin().X + margin;
        var right = windowLeft + ImGui.GetWindowContentRegionMax().X - margin;
        var half = MathF.Min(centerX - left, right - centerX);
        return half * 2f;
    }

    private static void DrawWrappedBlock(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        float wrapWidth)
    {
        var lines = WrapLines(text, wrapWidth);
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();
        var packed = ImGui.GetColorU32(color);
        var top = center.Y - lines.Length * lineHeight * 0.5f;
        for (var index = 0; index < lines.Length; index++)
        {
            var size = ImGui.CalcTextSize(lines[index]);
            drawList.AddText(font, fontSize, new Vector2(center.X - size.X * 0.5f, top + index * lineHeight), packed,
                lines[index]);
        }
    }

    public static float DrawWrappedCentered(ImDrawListPtr drawList, string text, in TextStyle style, Vector4 color,
        Vector2 topCenter, float maxWidth, float lineSpacing = 1.25f)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(color);
            var lineHeight = ImGui.CalcTextSize("Ay").Y * lineSpacing;
            var buffer = new List<string>();
            var y = topCenter.Y;
            var segments = text.Split('\n');
            for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                var segment = segments[segmentIndex].TrimEnd('\r');
                if (segment.Length == 0)
                {
                    y += lineHeight;
                    continue;
                }

                buffer.Clear();
                WrapSegment(segment, maxWidth, buffer);
                for (var lineIndex = 0; lineIndex < buffer.Count; lineIndex++)
                {
                    var line = buffer[lineIndex];
                    var size = ImGui.CalcTextSize(line);
                    drawList.AddText(font, fontSize,
                        new Vector2(topCenter.X - size.X * 0.5f, y + (lineHeight - size.Y) * 0.5f), packed, line);
                    y += lineHeight;
                }
            }

            return y;
        }
    }

    public static int CountWrappedLines(string text, in TextStyle style, float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var buffer = new List<string>();
            var lines = 0;
            var segments = text.Split('\n');
            for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
            {
                var segment = segments[segmentIndex].TrimEnd('\r');
                if (segment.Length == 0)
                {
                    lines++;
                    continue;
                }

                buffer.Clear();
                WrapSegment(segment, maxWidth, buffer);
                lines += buffer.Count;
            }

            return lines;
        }
    }

    public static void DrawCenteredHalo(Vector2 center, string text, Vector4 color, Vector4 halo, float haloRadius,
        float maxWidth, in TextStyle style)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var display = Fit(text, maxWidth);
            var drawList = ImGui.GetWindowDrawList();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var origin = center - ImGui.CalcTextSize(display) * 0.5f;
            var haloColor = ImGui.GetColorU32(halo);
            for (var offsetIndex = 0; offsetIndex < HaloOffsets.Length; offsetIndex++)
            {
                drawList.AddText(font, fontSize, origin + HaloOffsets[offsetIndex] * haloRadius, haloColor, display);
            }

            drawList.AddText(font, fontSize, origin, ImGui.GetColorU32(color), display);
        }
    }

    public static float DrawWrappedCentered(Vector2 topCenter, string text, Vector4 color, in TextStyle style,
        float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var lines = WrapLines(text, maxWidth);
            var drawList = ImGui.GetWindowDrawList();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var packed = ImGui.GetColorU32(color);
            for (var index = 0; index < lines.Length; index++)
            {
                var size = ImGui.CalcTextSize(lines[index]);
                drawList.AddText(font, fontSize, new Vector2(topCenter.X - size.X * 0.5f, topCenter.Y + index * lineHeight),
                    packed, lines[index]);
            }

            return lines.Length * lineHeight;
        }
    }

    public static void DrawWrappedCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        in TextStyle style, float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            DrawWrappedBlock(drawList, center, text, color, maxWidth);
        }
    }

    public static float DrawWrappedLeft(Vector2 topLeft, string text, Vector4 color, in TextStyle style,
        float maxWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var lines = WrapLines(text, maxWidth);
            var drawList = ImGui.GetWindowDrawList();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var packed = ImGui.GetColorU32(color);
            for (var index = 0; index < lines.Length; index++)
            {
                drawList.AddText(font, fontSize, new Vector2(topLeft.X, topLeft.Y + index * lineHeight), packed,
                    lines[index]);
            }

            return lines.Length * lineHeight;
        }
    }

    public static string[] WrapText(string text, in TextStyle style, float maxWidth) =>
        WrapText(text, style.Scale, style.Weight, maxWidth);

    public static string[] WrapText(string text, float scale, FontWeight weight, float maxWidth)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            return WrapLines(text, maxWidth);
        }
    }

    public static string[] WrapCurrent(string text, float maxWidth) => WrapLines(text, maxWidth);

    private static string[] WrapLines(string text, float maxWidth)
    {
        InvalidateCachesOnFontChange();
        var fontSize = ImGui.GetFontSize();
        if (WrapCache.TryGetValue(text, out var cached) && cached.Width == maxWidth && cached.FontSize == fontSize)
        {
            return cached.Lines;
        }

        var lines = Wrap(text, maxWidth);
        WrapCache[text] = new WrapEntry(maxWidth, fontSize, lines);
        return lines;
    }

    private static string[] Wrap(string text, float maxWidth)
    {
        if (maxWidth <= 0f)
        {
            return new[] { text };
        }

        if (text.IndexOf('\n') < 0 && ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return new[] { text };
        }

        var lines = new List<string>();
        var segments = text.Split('\n');
        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            WrapSegment(segments[segmentIndex].TrimEnd('\r'), maxWidth, lines);
        }

        return lines.ToArray();
    }

    private static bool IsCjk(char value)
    {
        return value >= '⺀' && value <= '鿿'
            || value >= '豈' && value <= '﫿'
            || value >= '＀' && value <= '￯';
    }

    private static void WrapSegment(string segment, float maxWidth, List<string> lines)
    {
        if (segment.Length == 0)
        {
            lines.Add(string.Empty);
            return;
        }

        var builder = new StringBuilder();
        var length = segment.Length;
        var index = 0;
        var pendingSpace = false;
        while (index < length)
        {
            if (segment[index] == ' ')
            {
                pendingSpace = true;
                index++;
                continue;
            }

            string token;
            if (IsCjk(segment[index]))
            {
                token = segment[index].ToString();
                index++;
            }
            else
            {
                var start = index;
                while (index < length && segment[index] != ' ' && !IsCjk(segment[index]))
                {
                    if (segment[index] == '-')
                    {
                        index++;
                        break;
                    }

                    index++;
                }

                token = segment.Substring(start, index - start);
            }

            if (builder.Length == 0)
            {
                AppendToken(token, maxWidth, lines, builder);
                pendingSpace = false;
                continue;
            }

            var separatorLength = pendingSpace ? 1 : 0;
            if (pendingSpace)
            {
                builder.Append(' ');
            }

            builder.Append(token);
            pendingSpace = false;
            if (ImGui.CalcTextSize(builder.ToString()).X > maxWidth)
            {
                builder.Length -= token.Length + separatorLength;
                lines.Add(builder.ToString());
                builder.Clear();
                AppendToken(token, maxWidth, lines, builder);
            }
        }

        if (builder.Length > 0)
        {
            lines.Add(builder.ToString());
        }
    }

    private static void AppendToken(string token, float maxWidth, List<string> lines, StringBuilder builder)
    {
        if (ImGui.CalcTextSize(token).X <= maxWidth)
        {
            builder.Append(token);
            return;
        }

        var start = 0;
        while (start < token.Length)
        {
            var count = CharactersThatFit(token, start, maxWidth);
            if (start + count >= token.Length)
            {
                builder.Append(token, start, token.Length - start);
                return;
            }

            lines.Add(token.Substring(start, count));
            start += count;
        }
    }

    private static int CharactersThatFit(string token, int start, float maxWidth)
    {
        var count = NextBoundary(token, start, 0);
        while (start + count < token.Length)
        {
            var next = NextBoundary(token, start, count);
            if (ImGui.CalcTextSize(token.Substring(start, next)).X > maxWidth)
            {
                break;
            }

            count = next;
        }

        return count;
    }

    private static int NextBoundary(string token, int start, int count)
    {
        var advance = count + 1;
        if (start + advance < token.Length && char.IsHighSurrogate(token[start + count]))
        {
            advance++;
        }

        return advance;
    }

    private static string Fit(string text, float maxWidth)
    {
        if (maxWidth <= 0f)
        {
            return text;
        }

        InvalidateCachesOnFontChange();
        var fontSize = ImGui.GetFontSize();
        var key = (text, maxWidth, fontSize);
        if (FitCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = Shorten(text, maxWidth);
        FitCache[key] = result;
        return result;
    }

    private static string Shorten(string text, float maxWidth)
    {
        if (ImGui.CalcTextSize(text).X <= maxWidth)
        {
            return text;
        }

        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = string.Concat(text.AsSpan(0, length).TrimEnd(), Ellipsis.AsSpan());
            if (ImGui.CalcTextSize(candidate).X <= maxWidth)
            {
                return candidate;
            }
        }

        return Ellipsis;
    }

    public static void DrawCentered(Vector2 center, string text, Vector4 color, in TextStyle style) =>
        DrawCentered(center, text, color, style.Scale, style.Weight);

    public static void DrawCentered(Vector2 center, string text, Vector4 color, float scale, FontWeight weight)
    {
        using (Plugin.Fonts.Push(scale, weight))
        {
            Plugin.Fonts.NoticeText(text);
            var size = ImGui.CalcTextSize(text);
            var wrapWidth = AutoWrapWidth(center.X);
            if (wrapWidth > 0f && size.X > wrapWidth)
            {
                DrawWrappedBlock(ImGui.GetWindowDrawList(), center, text, color, wrapWidth);
                return;
            }

            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }
        }
    }
}
