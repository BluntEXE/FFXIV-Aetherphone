using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal static class ChirperIcons
{
    private const float ViewBox = 24f;

    public static readonly VectorIcon Reply = VectorIcon.Parse(
        "M21 11.5a8.4 8.4 0 01-8.5 8.3 8.9 8.9 0 01-3.2-.6L3 21l1.9-5.4a8 8 0 01-1.4-4.6A8.4 8.4 0 0112 2.7a8.4 8.4 0 019 8.8z");

    public static readonly VectorIcon Rechirp = VectorIcon.Parse(
        "M17 2l4 4-4 4M3 11v-1a4 4 0 014-4h14M7 22l-4-4 4-4M21 13v1a4 4 0 01-4 4H3");

    public static readonly VectorIcon Share = VectorIcon.Parse(
        "M12 15V3M7.5 7.5L12 3l4.5 4.5M4 13v6a2 2 0 002 2h12a2 2 0 002-2v-6");

    public static readonly VectorIcon SearchHandle = VectorIcon.Parse("M20 20l-3.6-3.6");

    public static readonly VectorIcon Bell = VectorIcon.Parse(
        "M18 8a6 6 0 10-12 0c0 7-3 9-3 9h18s-3-2-3-9M13.7 21a2 2 0 01-3.4 0");

    public static readonly VectorIcon Sliders = VectorIcon.Parse("M4 6h16M7 12h10M10 18h4");

    public static readonly VectorIcon ChevronLeft = VectorIcon.Parse("M15 5l-7 7 7 7");

    public static readonly VectorIcon ChevronRight = VectorIcon.Parse("M9 5l7 7-7 7");

    public static readonly VectorIcon Close = VectorIcon.Parse("M6 6l12 12M18 6L6 18");

    public static readonly VectorIcon Check = VectorIcon.Parse("M20 6L9 17l-5-5");

    public static readonly VectorIcon Plus = VectorIcon.Parse("M12 5v14M5 12h14");

    public static readonly VectorIcon Heart = VectorIcon.Parse(
        "M12 20.3S3.3 15.2 3.3 9.2a4.9 4.9 0 018.7-3 4.9 4.9 0 018.7 3c0 6-8.7 11.1-8.7 11.1z");

    public static readonly VectorIcon Feather = VectorIcon.Parse(
        "M20.2 3.8c-4.8.4-8.6 2-11 4.7-2 2.3-2.8 5-2.4 7.9l-2.6 3.8 1.4 1 2.5-3.6c2.9.3 5.5-.6 7.6-2.7 2.5-2.5 4-6.3 4.5-11.1zM6.8 16.4c2.6-4 5.9-6.8 9.9-8.6");

    public static readonly VectorIcon ImageDiagonal = VectorIcon.Parse("M21 15.5l-4.8-4.8L5.5 21");

    public static readonly VectorIcon EyeSlash = VectorIcon.Parse(
        "M2 12s3.5-6.5 10-6.5S22 12 22 12s-3.5 6.5-10 6.5S2 12 2 12zM4 4l16 16");

    public static readonly VectorIcon Camera = VectorIcon.Parse(
        "M23 19a2 2 0 01-2 2H3a2 2 0 01-2-2V8a2 2 0 012-2h4l2-3h6l2 3h4a2 2 0 012 2z");

    public static readonly VectorIcon QuoteArrow = VectorIcon.Parse("M17 6H7a4 4 0 00-4 4v8M14 3l3 3-3 3");

    public static readonly VectorIcon SmileMouth = VectorIcon.Parse("M8.2 14.2a4.8 4.8 0 007.6 0");

    public static readonly VectorIcon SmilePlus = VectorIcon.Parse("M16.5 3.2v4M14.5 5.2h4");

    public static readonly VectorIcon HomeOutline = VectorIcon.Parse(
        "M3 10.5L12 3l9 7.5V20a1 1 0 01-1 1h-5v-7H9v7H4a1 1 0 01-1-1z");

    public static readonly VectorIcon Refresh = VectorIcon.Parse("M21 12a9 9 0 11-2.64-6.36M21 3v6h-6");

    public static readonly VectorIcon Shoulders = VectorIcon.Parse("M6.2 18.6a6 6 0 0111.6 0");

    public static readonly VectorIcon PinOutline = VectorIcon.Parse("M12 21.5s-7-5.6-7-11.3a7 7 0 0114 0c0 5.7-7 11.3-7 11.3z");

    public static readonly VectorIcon ClockHands = VectorIcon.Parse("M12 7v5l3.2 2");

    public static void Pin(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, float stroke = 1.9f)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        PinOutline.Stroke(drawList, center, size, packed, stroke);
        drawList.AddCircle(origin + new Vector2(12f, 10.2f) * unit, 2.5f * unit, packed, 14, MathF.Max(1f, stroke * unit));
    }

    public static void Clock(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, float stroke = 1.9f)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        drawList.AddCircle(origin + new Vector2(12f, 12f) * unit, 9f * unit, packed, 28, MathF.Max(1f, stroke * unit));
        ClockHands.Stroke(drawList, center, size, packed, stroke);
    }

    private static readonly Vector4 CutoutInk = new(0.043f, 0.078f, 0.125f, 1f);

    public static void Home(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, bool filled)
    {
        var packed = ImGui.GetColorU32(color);
        if (!filled)
        {
            HomeOutline.Stroke(drawList, center, size, packed, 2f);
            return;
        }

        var unit = size / ViewBox;
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        drawList.AddTriangleFilled(origin + new Vector2(2.2f, 11f) * unit, origin + new Vector2(12f, 2.6f) * unit,
            origin + new Vector2(21.8f, 11f) * unit, packed);
        drawList.AddRectFilled(origin + new Vector2(4f, 9.5f) * unit, origin + new Vector2(20f, 21f) * unit, packed,
            1.5f * unit, ImDrawFlags.RoundCornersBottom);
        drawList.AddRectFilled(origin + new Vector2(9.5f, 14f) * unit, origin + new Vector2(14.5f, 21f) * unit,
            ImGui.GetColorU32(CutoutInk), 1f * unit, ImDrawFlags.RoundCornersTop);
    }

    public static void Person(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, bool filled)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        var ring = origin + new Vector2(12f, 12f) * unit;
        var head = origin + new Vector2(12f, 9.6f) * unit;
        if (!filled)
        {
            drawList.AddCircle(ring, 9.5f * unit, packed, 32, MathF.Max(1f, 2f * unit));
            drawList.AddCircle(head, 3.3f * unit, packed, 16, MathF.Max(1f, 2f * unit));
            Shoulders.Stroke(drawList, center, size, packed, 2f);
            return;
        }

        var cutout = ImGui.GetColorU32(CutoutInk);
        drawList.AddCircleFilled(ring, 9.5f * unit, packed, 32);
        drawList.AddCircleFilled(head, 3.4f * unit, cutout, 16);
        drawList.PathArcTo(origin + new Vector2(12f, 20.4f) * unit, 6f * unit, MathF.PI, MathF.PI * 2f, 16);
        drawList.PathFillConvex(cutout);
    }

    public static void Search(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, float stroke = 2.1f)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        drawList.AddCircle(origin + new Vector2(11f, 11f) * unit, 7f * unit, packed, 24, MathF.Max(1f, stroke * unit));
        SearchHandle.Stroke(drawList, center, size, packed, stroke);
    }

    public static void React(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, bool withPlus,
        float stroke = 1.9f)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        var face = origin + new Vector2(12f, 12f) * unit;
        drawList.AddCircle(face, 9.2f * unit, packed, 28, MathF.Max(1f, stroke * unit));
        SmileMouth.Stroke(drawList, center, size, packed, stroke);
        drawList.AddCircleFilled(origin + new Vector2(9f, 9.6f) * unit, MathF.Max(0.9f, 1.1f * unit), packed, 8);
        drawList.AddCircleFilled(origin + new Vector2(15f, 9.6f) * unit, MathF.Max(0.9f, 1.1f * unit), packed, 8);
        if (withPlus)
        {
            SmilePlus.Stroke(drawList, center, size, packed, 1.7f);
        }
    }

    public static void Dots(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        var radius = 1.9f * unit;
        drawList.AddCircleFilled(origin + new Vector2(5f, 12f) * unit, radius, packed, 12);
        drawList.AddCircleFilled(origin + new Vector2(12f, 12f) * unit, radius, packed, 12);
        drawList.AddCircleFilled(origin + new Vector2(19f, 12f) * unit, radius, packed, 12);
    }

    public static void Image(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, float stroke = 1.9f)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        drawList.AddRect(origin + new Vector2(3f, 3f) * unit, origin + new Vector2(21f, 21f) * unit, packed, 4f * unit,
            ImDrawFlags.RoundCornersAll, MathF.Max(1f, stroke * unit));
        drawList.AddCircle(origin + new Vector2(9f, 9f) * unit, 1.7f * unit, packed, 12, MathF.Max(1f, stroke * unit));
        ImageDiagonal.Stroke(drawList, center, size, packed, stroke);
    }

    public static void Sensitive(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, float stroke = 2f)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        EyeSlash.Stroke(drawList, center, size, packed, stroke);
        drawList.AddCircle(origin + new Vector2(12f, 12f) * unit, 2.6f * unit, packed, 16, MathF.Max(1f, stroke * unit));
    }

    public static void CameraBadge(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, float stroke = 2f)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        Camera.Stroke(drawList, center, size, packed, stroke);
        drawList.AddCircle(origin + new Vector2(12f, 13f) * unit, 3.5f * unit, packed, 16, MathF.Max(1f, stroke * unit));
    }

    public static void Quote(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, float stroke = 1.9f)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        QuoteArrow.Stroke(drawList, center, size, packed, stroke);
        drawList.AddRect(origin + new Vector2(9f, 13f) * unit, origin + new Vector2(21f, 21f) * unit, packed,
            2.5f * unit, ImDrawFlags.RoundCornersAll, MathF.Max(1f, stroke * unit));
    }

    public static void HeartFilled(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color)
    {
        var unit = size / ViewBox;
        var packed = ImGui.GetColorU32(color);
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        var lobeRadius = 4.9f * unit;
        drawList.AddCircleFilled(origin + new Vector2(7.65f, 7.9f) * unit, lobeRadius, packed, 20);
        drawList.AddCircleFilled(origin + new Vector2(16.35f, 7.9f) * unit, lobeRadius, packed, 20);
        drawList.AddTriangleFilled(origin + new Vector2(3.05f, 9.6f) * unit, origin + new Vector2(20.95f, 9.6f) * unit,
            origin + new Vector2(12f, 20.3f) * unit, packed);
        Heart.Stroke(drawList, center, size, packed, 2f);
    }
}
