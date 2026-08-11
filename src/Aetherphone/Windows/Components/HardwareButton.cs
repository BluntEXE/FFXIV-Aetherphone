using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum RailSide
{
    Left,
    Right,
    Top,
    Bottom,
}

internal static class HardwareButton
{
    private const float PressTravel = 1.5f;
    private const float SeatSpread = 2.4f;
    private const float MinBury = 3f;
    private const float MaxBury = 6f;
    private const float ClipBleed = 3f;

    public static void Draw(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, RailSide side, bool hovered,
        float press, float active)
    {
        var scale = UiScale.Current;
        var bury = Math.Clamp(theme.MetalWidth, MinBury, MaxBury) * scale;
        var rounding = Protrusion(bounds, side) * 0.5f;
        Seat(drawList, bounds, theme, side, rounding, bury, scale);

        var travel = press * PressTravel * scale;
        var shift = side switch
        {
            RailSide.Right => new Vector2(-travel, 0f),
            RailSide.Top => new Vector2(0f, travel),
            RailSide.Bottom => new Vector2(0f, -travel),
            _ => new Vector2(travel, 0f),
        };
        var min = bounds.Min + shift;
        var max = bounds.Max + shift;
        var cap = Bury(min, max, side, bury);

        var metal = theme.RailMetal;
        var crown = Palette.Lighten(metal, 0.30f - press * 0.20f);
        var flank = Palette.Darken(metal, 0.28f + press * 0.12f);

        FillCap(drawList, cap, rounding, side, ImGui.GetColorU32(metal));
        Face(drawList, cap.Min, cap.Max, rounding, side, crown, flank);
        CrownSpecular(drawList, min, max, rounding, side, hovered, press, scale);
        RecessSeam(drawList, min, max, rounding, side, press, active, theme, scale);
        StrokeProud(drawList, cap, ProudClip(bounds, side, scale), rounding,
            ImGui.GetColorU32(Palette.Darken(metal, 0.60f)), 1f * scale);
    }

    private static void Seat(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, RailSide side, float rounding,
        float bury, float scale)
    {
        var spread = SeatSpread * scale;
        var horizontal = side is RailSide.Top or RailSide.Bottom;
        var min = horizontal
            ? new Vector2(bounds.Min.X - spread, bounds.Min.Y)
            : new Vector2(bounds.Min.X, bounds.Min.Y - spread);
        var max = horizontal
            ? new Vector2(bounds.Max.X + spread, bounds.Max.Y)
            : new Vector2(bounds.Max.X, bounds.Max.Y + spread);
        var seat = Bury(min, max, side, bury);
        FillCap(drawList, seat, rounding + spread, side, ImGui.GetColorU32(Palette.Lighten(theme.Glass, 0.06f)));
        StrokeProud(drawList, seat, ProudClip(new Rect(min, max), side, scale), rounding + spread,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)), 1f * scale);
    }

    private static float Protrusion(Rect bounds, RailSide side) =>
        side is RailSide.Top or RailSide.Bottom ? bounds.Height : bounds.Width;

    private static Rect Bury(Vector2 min, Vector2 max, RailSide side, float depth) => side switch
    {
        RailSide.Right => new Rect(new Vector2(min.X - depth, min.Y), max),
        RailSide.Top => new Rect(min, new Vector2(max.X, max.Y + depth)),
        RailSide.Bottom => new Rect(new Vector2(min.X, min.Y - depth), max),
        _ => new Rect(min, new Vector2(max.X + depth, max.Y)),
    };

    private static Rect ProudClip(Rect bounds, RailSide side, float scale)
    {
        var bleed = ClipBleed * scale;
        return side switch
        {
            RailSide.Right => new Rect(new Vector2(bounds.Min.X, bounds.Min.Y - bleed),
                new Vector2(bounds.Max.X + bleed, bounds.Max.Y + bleed)),
            RailSide.Top => new Rect(new Vector2(bounds.Min.X - bleed, bounds.Min.Y - bleed),
                new Vector2(bounds.Max.X + bleed, bounds.Max.Y)),
            RailSide.Bottom => new Rect(new Vector2(bounds.Min.X - bleed, bounds.Min.Y),
                new Vector2(bounds.Max.X + bleed, bounds.Max.Y + bleed)),
            _ => new Rect(new Vector2(bounds.Min.X - bleed, bounds.Min.Y - bleed),
                new Vector2(bounds.Max.X, bounds.Max.Y + bleed)),
        };
    }

    private static void FillCap(ImDrawListPtr drawList, Rect rect, float rounding, RailSide side, uint color)
    {
        switch (side)
        {
            case RailSide.Right:
                Squircle.FillSideCap(drawList, rect.Min, rect.Max, rounding, color, false);
                return;
            case RailSide.Top:
                Squircle.FillCap(drawList, rect.Min, rect.Max, rounding, color, true);
                return;
            case RailSide.Bottom:
                Squircle.FillCap(drawList, rect.Min, rect.Max, rounding, color, false);
                return;
            default:
                Squircle.FillSideCap(drawList, rect.Min, rect.Max, rounding, color, true);
                return;
        }
    }

    private static void StrokeProud(ImDrawListPtr drawList, Rect rect, Rect clip, float rounding, uint color,
        float thickness)
    {
        drawList.PushClipRect(clip.Min, clip.Max, true);
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, color, thickness);
        drawList.PopClipRect();
    }

    private static void Face(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        Vector4 crown, Vector4 flank)
    {
        if (side is RailSide.Top or RailSide.Bottom)
        {
            FaceHorizontal(drawList, min, max, rounding, side, crown, flank);
            return;
        }

        var top = min.Y + rounding;
        var bottom = max.Y - rounding;
        if (bottom <= top)
        {
            return;
        }

        var crownTop = ImGui.GetColorU32(Palette.Lighten(crown, 0.10f));
        var crownBottom = ImGui.GetColorU32(Palette.Darken(crown, 0.12f));
        var flankTop = ImGui.GetColorU32(Palette.Lighten(flank, 0.08f));
        var flankBottom = ImGui.GetColorU32(Palette.Darken(flank, 0.10f));
        var faceMin = new Vector2(min.X, top);
        var faceMax = new Vector2(max.X, bottom);
        if (side == RailSide.Right)
        {
            drawList.AddRectFilledMultiColor(faceMin, faceMax, flankTop, crownTop, crownBottom, flankBottom);
            return;
        }

        drawList.AddRectFilledMultiColor(faceMin, faceMax, crownTop, flankTop, flankBottom, crownBottom);
    }

    private static void FaceHorizontal(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        Vector4 crown, Vector4 flank)
    {
        var left = min.X + rounding;
        var right = max.X - rounding;
        if (right <= left)
        {
            return;
        }

        var crownTop = ImGui.GetColorU32(Palette.Lighten(crown, 0.10f));
        var crownBottom = ImGui.GetColorU32(Palette.Darken(crown, 0.12f));
        var flankTop = ImGui.GetColorU32(Palette.Lighten(flank, 0.08f));
        var flankBottom = ImGui.GetColorU32(Palette.Darken(flank, 0.10f));
        var faceMin = new Vector2(left, min.Y);
        var faceMax = new Vector2(right, max.Y);
        if (side == RailSide.Top)
        {
            drawList.AddRectFilledMultiColor(faceMin, faceMax, crownTop, crownTop, flankBottom, flankBottom);
            return;
        }

        drawList.AddRectFilledMultiColor(faceMin, faceMax, flankTop, flankTop, crownBottom, crownBottom);
    }

    private static void CrownSpecular(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        bool hovered, float press, float scale)
    {
        var alpha = (hovered ? 0.55f : 0.40f) * (1f - press * 0.6f);
        if (alpha <= 0.01f)
        {
            return;
        }

        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        if (side is RailSide.Top or RailSide.Bottom)
        {
            var y = side == RailSide.Top ? min.Y + 1.6f * scale : max.Y - 1.6f * scale;
            drawList.AddLine(new Vector2(min.X + rounding, y), new Vector2(max.X - rounding, y), color,
                1.3f * scale);
            return;
        }

        var x = side == RailSide.Right ? max.X - 1.6f * scale : min.X + 1.6f * scale;
        drawList.AddLine(new Vector2(x, min.Y + rounding), new Vector2(x, max.Y - rounding), color, 1.3f * scale);
    }

    private static void RecessSeam(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, RailSide side,
        float press, float active, PhoneTheme theme, float scale)
    {
        if (side is RailSide.Top or RailSide.Bottom)
        {
            RecessSeamHorizontal(drawList, min, max, rounding, side, press, active, theme, scale);
            return;
        }

        var innerX = side == RailSide.Right ? min.X + 1f * scale : max.X - 1f * scale;
        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.42f + press * 0.30f));
        drawList.AddLine(new Vector2(innerX, min.Y + rounding), new Vector2(innerX, max.Y - rounding), shadow,
            1.4f * scale);
        if (active <= 0.01f)
        {
            return;
        }

        var accent = ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, active));
        var tintX = side == RailSide.Right ? min.X + 2.6f * scale : max.X - 2.6f * scale;
        drawList.AddLine(new Vector2(tintX, min.Y + rounding), new Vector2(tintX, max.Y - rounding), accent,
            1.6f * scale);
    }

    private static void RecessSeamHorizontal(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding,
        RailSide side, float press, float active, PhoneTheme theme, float scale)
    {
        var innerY = side == RailSide.Top ? max.Y - 1f * scale : min.Y + 1f * scale;
        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.42f + press * 0.30f));
        drawList.AddLine(new Vector2(min.X + rounding, innerY), new Vector2(max.X - rounding, innerY), shadow,
            1.4f * scale);
        if (active <= 0.01f)
        {
            return;
        }

        var accent = ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, active));
        var tintY = side == RailSide.Top ? max.Y - 2.6f * scale : min.Y + 2.6f * scale;
        drawList.AddLine(new Vector2(min.X + rounding, tintY), new Vector2(max.X - rounding, tintY), accent,
            1.6f * scale);
    }
}
