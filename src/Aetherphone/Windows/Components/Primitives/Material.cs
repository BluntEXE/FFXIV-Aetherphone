using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Material
{
    private const float BorderAlpha = 0.09f;
    private const float HighlightAlpha = 0.11f;
    private const float SheenFalloff = 0.18f;
    private const uint AlphaChannel = 0xFF000000;
    private static readonly Vector4 FrostedFill = new(0.12f, 0.12f, 0.15f, 0.86f);
    private static readonly Vector4 DockGlassCalm = new(0.90f, 0.92f, 0.98f, 0.26f);
    private static readonly Vector4 DockGlassHarsh = new(0.56f, 0.58f, 0.64f, 0.42f);

    public static void TopGlow(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 accent,
        float coverage, float strength)
    {
        if (strength <= 0f)
        {
            return;
        }

        var scale = UiScale.Current;
        var tint = ImGui.GetColorU32(accent with { W = strength });
        var clear = ImGui.GetColorU32(accent with { W = 0f });
        var capBottom = min.Y + rounding + scale;
        drawList.AddRectFilled(min, new Vector2(max.X, capBottom), tint, rounding, ImDrawFlags.RoundCornersTop);
        var fadeBottom = min.Y + (max.Y - min.Y) * coverage;
        if (fadeBottom > capBottom)
        {
            drawList.AddRectFilledMultiColor(new Vector2(min.X, capBottom), new Vector2(max.X, fadeBottom), tint, tint,
                clear, clear);
        }
    }

    public static void Veil(ImDrawListPtr drawList, Vector2 min, Vector2 max, float dim, float rounding = 0f)
    {
        if (dim <= 0f)
        {
            return;
        }

        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)));
    }

    public static void Glass(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 ink,
        float scale)
    {
        var lightScene = ink.X < 0.5f;
        var fill = lightScene ? new Vector4(0.10f, 0.12f, 0.16f, 0.10f) : new Vector4(1f, 1f, 1f, 0.10f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ink with { W = ink.W * 0.14f }), 1f * scale);
        Sheen(drawList, min, max, rounding,
            ImGui.GetColorU32(ink with { W = ink.W * (lightScene ? 0.05f : 0.18f) }), 1f * scale, 1f * scale);
    }

    public static void Frosted(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(FrostedFill with { W = FrostedFill.W * opacity }));
        EdgeSquircle(drawList, min, max, radius, scale, opacity);
    }

    public static void Dock(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        float brightness, float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        var harsh = Math.Clamp(brightness, 0f, 1f);
        var fill = Vector4.Lerp(DockGlassCalm, DockGlassHarsh, harsh);
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fill with { W = fill.W * opacity }));

        SheenBlock(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * opacity)), 1f * scale,
            0.5f);

        Squircle.Stroke(drawList, min, max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.42f * opacity)), 1.4f * scale);
        var innerOffset = 1.6f * scale;
        Squircle.Stroke(drawList, new Vector2(min.X + innerOffset, min.Y + innerOffset),
            new Vector2(max.X - innerOffset, max.Y - innerOffset), radius - innerOffset,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f * opacity)), 1f * scale);
    }

    public static void Card(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, Vector4 fill, float scale,
        float opacity = 1f)
    {
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill with { W = fill.W * opacity }), rounding);
        Edge(drawList, min, max, rounding, scale, opacity);
    }

    public static void Edge(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale,
        float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, BorderAlpha * opacity)), rounding,
            ImDrawFlags.RoundCornersAll, 1f * scale);
        SheenRounded(drawList, min, max, rounding, HighlightColor(opacity), 1f * scale, 1f * scale);
    }

    public static void EdgeSquircle(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale,
        float opacity = 1f)
    {
        if (opacity <= 0f)
        {
            return;
        }

        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, BorderAlpha * opacity)),
            1f * scale);
        Sheen(drawList, min, max, radius, HighlightColor(opacity), 1f * scale, 1f * scale);
    }

    private static uint HighlightColor(float opacity) =>
        ImGui.GetColorU32(new Vector4(1f, 1f, 1f, HighlightAlpha * opacity));

    public static void Sheen(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float thickness, float depth)
    {
        var box = Squircle.CornerBox(min, max, radius);
        DrawSheen(drawList, min, max, box, Squircle.EdgeInset(box, depth), color, thickness, depth);
    }

    public static void SheenRounded(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float thickness, float depth)
    {
        var box = Squircle.CornerBox(min, max, radius);
        DrawSheen(drawList, min, max, box, RoundedInset(box, depth), color, thickness, depth);
    }

    public static void SheenBlock(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float depth, float coverage)
    {
        if ((color & AlphaChannel) == 0u)
        {
            return;
        }

        var box = Squircle.CornerBox(min, max, radius);
        var inset = Squircle.EdgeInset(box, depth);
        var left = min.X + inset;
        var right = max.X - inset;
        var top = min.Y + depth;
        var bottom = min.Y + (max.Y - min.Y) * coverage;
        if (right - left <= 1f || bottom <= top)
        {
            return;
        }

        var middle = (left + right) * 0.5f;
        var taper = MathF.Max(box - inset, (right - left) * SheenFalloff);
        var solidLeft = MathF.Min(left + taper, middle);
        var solidRight = MathF.Max(right - taper, middle);
        var clear = color & ~AlphaChannel;
        drawList.AddRectFilledMultiColor(new Vector2(left, top), new Vector2(solidLeft, bottom), clear, color, clear,
            clear);
        if (solidRight > solidLeft)
        {
            drawList.AddRectFilledMultiColor(new Vector2(solidLeft, top), new Vector2(solidRight, bottom), color, color,
                clear, clear);
        }

        drawList.AddRectFilledMultiColor(new Vector2(solidRight, top), new Vector2(right, bottom), color, clear, clear,
            clear);
    }

    private static float RoundedInset(float box, float depth)
    {
        if (box <= 0f || depth <= 0f)
        {
            return MathF.Max(box, 0f);
        }

        if (depth >= box)
        {
            return 0f;
        }

        var reach = box - depth;
        return box - MathF.Sqrt(MathF.Max(box * box - reach * reach, 0f));
    }

    private static void DrawSheen(ImDrawListPtr drawList, Vector2 min, Vector2 max, float box, float inset, uint color,
        float thickness, float depth)
    {
        if ((color & AlphaChannel) == 0u)
        {
            return;
        }

        var left = min.X + inset;
        var right = max.X - inset;
        if (right - left <= 1f)
        {
            return;
        }

        var top = min.Y + depth;
        var bottom = top + MathF.Max(thickness, 1f);
        var middle = (left + right) * 0.5f;
        var taper = MathF.Max(box - inset, (right - left) * SheenFalloff);
        var solidLeft = MathF.Min(left + taper, middle);
        var solidRight = MathF.Max(right - taper, middle);
        var clear = color & ~AlphaChannel;
        drawList.AddRectFilledMultiColor(new Vector2(left, top), new Vector2(solidLeft, bottom), clear, color, color,
            clear);
        if (solidRight > solidLeft)
        {
            drawList.AddRectFilled(new Vector2(solidLeft, top), new Vector2(solidRight, bottom), color);
        }

        drawList.AddRectFilledMultiColor(new Vector2(solidRight, top), new Vector2(right, bottom), color, clear, clear,
            color);
    }
}
