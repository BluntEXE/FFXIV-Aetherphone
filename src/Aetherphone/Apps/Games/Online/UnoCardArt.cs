using Aetherphone.Core;
using Aetherphone.Core.GameRooms;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

// One card, drawn from primitives: a colored squircle, a tilted white pool, and a rank glyph. The
// same function paints the discard, the hand fan and the opponents' backs, so every card on the
// table agrees on what a card looks like.
internal static class UnoCardArt
{
    private static readonly Vector4[] Colors =
    {
        new(0.85f, 0.22f, 0.20f, 1f),
        new(0.95f, 0.76f, 0.10f, 1f),
        new(0.16f, 0.66f, 0.32f, 1f),
        new(0.12f, 0.45f, 0.85f, 1f),
    };

    private static readonly Vector4 WildFace = new(0.13f, 0.12f, 0.16f, 1f);
    private static readonly Vector4 BackFace = new(0.16f, 0.13f, 0.24f, 1f);
    private static readonly Vector4 Ink = new(0.98f, 0.98f, 0.99f, 1f);

    public static Vector4 ColorFor(int colorIndex)
    {
        return colorIndex is >= 0 and <= 3 ? Colors[colorIndex] : WildFace;
    }

    public static string Label(int card)
    {
        if (card == GameRoomWire.WildCard)
        {
            return "W";
        }

        if (card == GameRoomWire.WildDrawFourCard)
        {
            return "+4";
        }

        var rank = GameRoomWire.RankOf(card);
        return rank switch
        {
            GameRoomWire.RankSkip => "X",
            GameRoomWire.RankReverse => "R",
            GameRoomWire.RankDrawTwo => "+2",
            _ => rank.ToString(),
        };
    }

    public static void DrawFace(ImDrawListPtr drawList, Rect rect, int card, float scale,
        bool highlight, float dimAlpha = 1f)
    {
        var rounding = rect.Width * 0.18f;
        var color = GameRoomWire.IsWild(card) ? WildFace : ColorFor(GameRoomWire.ColorOf(card));
        var top = ImGui.GetColorU32(Lighten(color, 0.14f) with { W = dimAlpha });
        var bottom = ImGui.GetColorU32(Darken(color, 0.18f) with { W = dimAlpha });
        Elevation.Card(drawList, rect.Min, rect.Max, rounding, scale, dimAlpha);
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding, top, bottom);
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (highlight ? 0.9f : 0.35f) * dimAlpha)),
            (highlight ? 2f : 1f) * scale);

        drawList.PushClipRect(rect.Min, rect.Max, true);
        drawList.AddCircleFilled(rect.Center, rect.Width * 0.42f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f * dimAlpha)), 32);
        if (GameRoomWire.IsWild(card))
        {
            DrawWildQuadrants(drawList, rect, dimAlpha);
        }

        drawList.PopClipRect();
        var style = rect.Height >= 64f ? TextStyles.Title2 : TextStyles.SubheadlineEmphasized;
        Typography.DrawCentered(drawList, rect.Center + new Vector2(0f, 1f), Label(card),
            new Vector4(0f, 0f, 0f, 0.35f * dimAlpha), style);
        Typography.DrawCentered(drawList, rect.Center, Label(card), Ink with { W = dimAlpha }, style);
    }

    public static void DrawBack(ImDrawListPtr drawList, Rect rect, float scale, float dimAlpha = 1f)
    {
        var rounding = rect.Width * 0.18f;
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(Lighten(BackFace, 0.10f) with { W = dimAlpha }),
            ImGui.GetColorU32(Darken(BackFace, 0.16f) with { W = dimAlpha }));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f * dimAlpha)), 1f * scale);
        drawList.PushClipRect(rect.Min, rect.Max, true);
        drawList.AddCircleFilled(rect.Center, rect.Width * 0.34f,
            ImGui.GetColorU32(new Vector4(0.92f, 0.35f, 0.30f, 0.8f * dimAlpha)), 32);
        drawList.AddCircle(rect.Center, rect.Width * 0.34f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f * dimAlpha)), 32, 1.5f * scale);
        drawList.PopClipRect();
    }

    private static void DrawWildQuadrants(ImDrawListPtr drawList, Rect rect, float dimAlpha)
    {
        var radius = rect.Width * 0.30f;
        var center = rect.Center;
        for (var quadrant = 0; quadrant < 4; quadrant++)
        {
            var from = quadrant * MathF.PI * 0.5f - MathF.PI * 0.5f;
            drawList.PathClear();
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, from, from + MathF.PI * 0.5f, 12);
            drawList.PathFillConvex(ImGui.GetColorU32(Colors[quadrant] with { W = dimAlpha }));
        }
    }

    private static Vector4 Lighten(Vector4 color, float amount)
    {
        return new Vector4(
            MathF.Min(1f, color.X + amount),
            MathF.Min(1f, color.Y + amount),
            MathF.Min(1f, color.Z + amount),
            color.W);
    }

    private static Vector4 Darken(Vector4 color, float amount)
    {
        return new Vector4(
            MathF.Max(0f, color.X - amount),
            MathF.Max(0f, color.Y - amount),
            MathF.Max(0f, color.Z - amount),
            color.W);
    }
}
