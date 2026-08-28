using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Skyfall;

internal sealed class SkyfallRenderer
{
    public static readonly Vector4 MeteorColor = new(1f, 0.62f, 0.30f, 1f);
    public static readonly Vector4 MeteorHead = new(1f, 0.93f, 0.80f, 1f);
    public static readonly Vector4 BlastFill = new(1f, 0.96f, 0.85f, 1f);
    private static readonly Vector4 Window = new(1f, 1f, 1f, 0.55f);
    private static readonly Vector4 Rubble = new(0.35f, 0.33f, 0.36f, 0.9f);
    private const float TowerWidth = 2.4f;
    private const int TowerCount = 3;

    public void Draw(SkyfallBoard board, Rect field, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(field.Min, field.Max, true);
        var factor = field.Width / SkyfallBoard.Width;
        DrawGround(drawList, field, factor, accent);
        DrawCities(drawList, board, field, factor, accent, scale);
        DrawBattery(drawList, field, factor, accent);
        DrawMeteors(drawList, board, field, factor, scale);
        DrawInterceptors(drawList, board, field, factor, accent, scale);
        DrawBlasts(drawList, board, field, factor, accent, scale);
        drawList.PopClipRect();
    }

    private static Vector2 ToScreen(Rect field, float factor, Vector2 world) => field.Min + world * factor;

    private static void DrawGround(ImDrawListPtr drawList, Rect field, float factor, Vector4 accent)
    {
        var groundY = field.Min.Y + SkyfallBoard.GroundY * factor;
        var horizonTop = groundY - 34f * factor;
        var clear = ImGui.GetColorU32(accent with { W = 0f });
        var haze = ImGui.GetColorU32(accent with { W = 0.16f });
        drawList.AddRectFilledMultiColor(new Vector2(field.Min.X, horizonTop), new Vector2(field.Max.X, groundY),
            clear, clear, haze, haze);
        drawList.AddRectFilled(new Vector2(field.Min.X, groundY), field.Max,
            ImGui.GetColorU32(GamePalette.Darken(accent, 0.55f) with { W = 0.9f }));
        drawList.AddLine(new Vector2(field.Min.X, groundY), new Vector2(field.Max.X, groundY),
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f)), MathF.Max(1f, 0.8f * factor));
    }

    private static void DrawCities(ImDrawListPtr drawList, SkyfallBoard board, Rect field, float factor, Vector4 accent,
        float scale)
    {
        var block = TowerWidth * factor;
        for (var cityIndex = 0; cityIndex < SkyfallBoard.CityCount; cityIndex++)
        {
            var center = ToScreen(field, factor, SkyfallBoard.CityCenter(cityIndex));
            if (!board.CityAlive(cityIndex))
            {
                drawList.AddRectFilled(new Vector2(center.X - block * 1.6f, center.Y - block * 0.4f),
                    new Vector2(center.X + block * 1.6f, center.Y), ImGui.GetColorU32(Rubble), block * 0.2f);
                continue;
            }

            var fill = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.28f));
            for (var tower = 0; tower < TowerCount; tower++)
            {
                var height = block * (tower == 1 ? 2.4f : 1.6f);
                var left = center.X + (tower - 1) * block * 1.15f - block * 0.5f;
                var min = new Vector2(left, center.Y - height);
                var max = new Vector2(left + block, center.Y);
                drawList.AddRectFilled(min, max, fill, block * 0.12f);
                var windowY = min.Y + block * 0.4f;
                while (windowY < max.Y - block * 0.4f)
                {
                    drawList.AddRectFilled(new Vector2(left + block * 0.3f, windowY),
                        new Vector2(left + block * 0.7f, windowY + block * 0.22f), ImGui.GetColorU32(Window));
                    windowY += block * 0.5f;
                }
            }

            ProgressRing.Glow(new Vector2(center.X, center.Y - block), block * 1.4f, accent, 0.18f);
        }
    }

    private static void DrawBattery(ImDrawListPtr drawList, Rect field, float factor, Vector4 accent)
    {
        var groundY = field.Min.Y + SkyfallBoard.GroundY * factor;
        var batteryX = field.Min.X + SkyfallBoard.BatteryX * factor;
        var size = 3.2f * factor;
        var apex = new Vector2(batteryX, groundY - size * 1.6f);
        ProgressRing.Glow(new Vector2(batteryX, groundY - size * 0.6f), size * 1.6f, accent, 0.35f);
        drawList.AddTriangleFilled(apex, new Vector2(batteryX + size, groundY), new Vector2(batteryX - size, groundY),
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.15f)));
        drawList.AddCircleFilled(apex, size * 0.22f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)));
    }

    private static void DrawMeteors(ImDrawListPtr drawList, SkyfallBoard board, Rect field, float factor, float scale)
    {
        var trailWide = ImGui.GetColorU32(MeteorColor with { W = 0.16f });
        var trailThin = ImGui.GetColorU32(MeteorColor with { W = 0.55f });
        var head = ImGui.GetColorU32(MeteorHead);
        var headRadius = MathF.Max(1.5f * scale, 1.1f * factor);
        for (var index = 0; index < board.MeteorCount; index++)
        {
            var meteor = board.GetMeteor(index);
            var start = ToScreen(field, factor, meteor.Start);
            var position = ToScreen(field, factor, meteor.Position);
            drawList.AddLine(start, position, trailWide, 3f * scale);
            drawList.AddLine(start, position, trailThin, 1.2f * scale);
            ProgressRing.Glow(position, headRadius * 2.2f, MeteorColor, 0.6f);
            drawList.AddCircleFilled(position, headRadius, head);
        }
    }

    private static void DrawInterceptors(ImDrawListPtr drawList, SkyfallBoard board, Rect field, float factor,
        Vector4 accent, float scale)
    {
        var barrel = ToScreen(field, factor, new Vector2(SkyfallBoard.BatteryX, SkyfallBoard.BarrelY));
        var trail = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f) with { W = 0.55f });
        var marker = ImGui.GetColorU32(accent with { W = 0.75f });
        var headHalf = 1.6f * scale;
        var cross = 2.5f * scale;
        for (var index = 0; index < board.InterceptorCount; index++)
        {
            var shot = board.GetInterceptor(index);
            var position = ToScreen(field, factor, shot.Position);
            var target = ToScreen(field, factor, shot.Target);
            drawList.AddLine(barrel, position, trail, 1.5f * scale);
            drawList.AddRectFilled(position - new Vector2(headHalf, headHalf), position + new Vector2(headHalf, headHalf),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)));
            drawList.AddLine(target - new Vector2(cross, cross), target + new Vector2(cross, cross), marker, 1f * scale);
            drawList.AddLine(target - new Vector2(cross, -cross), target + new Vector2(cross, -cross), marker, 1f * scale);
        }
    }

    private static void DrawBlasts(ImDrawListPtr drawList, SkyfallBoard board, Rect field, float factor, Vector4 accent,
        float scale)
    {
        var fill = ImGui.GetColorU32(BlastFill with { W = 0.85f });
        var ring = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f));
        for (var index = 0; index < board.BlastCount; index++)
        {
            var blast = board.GetBlast(index);
            var center = ToScreen(field, factor, blast.Center);
            var radius = blast.Radius * factor;
            ProgressRing.Glow(center, radius * 1.5f, MeteorColor, 0.8f);
            drawList.AddCircleFilled(center, radius, fill);
            drawList.AddCircle(center, radius, ring, 0, MathF.Max(1f, 1.5f * scale));
        }
    }
}
