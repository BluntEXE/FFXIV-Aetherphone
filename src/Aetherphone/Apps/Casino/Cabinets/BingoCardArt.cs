using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class BingoCardArt
{
    public const int Columns = BingoRules.Columns;

    private const string FreeMark = "FREE";

    public static readonly Vector4[] ColumnTints =
    {
        new(0.310f, 0.588f, 0.855f, 1f),
        new(0.243f, 0.686f, 0.604f, 1f),
        new(0.898f, 0.690f, 0.298f, 1f),
        new(0.882f, 0.451f, 0.404f, 1f),
        new(0.612f, 0.460f, 0.840f, 1f),
    };

    public static float HeightFor(float width)
    {
        return width;
    }

    public static void Draw(ImDrawListPtr drawList, AppSkin ui, Rect card, int[]? numbers, int autoMask,
        int stampedMask, BingoRoundPlayback playback, int cardIndex, float scale)
    {
        var cell = card.Width / Columns;
        for (var cellIndex = 0; cellIndex < BingoRules.Cells; cellIndex++)
        {
            DrawCell(drawList, ui, card, cell, cellIndex, numbers, autoMask, stampedMask, playback, cardIndex,
                scale);
        }
    }

    private static void DrawCell(ImDrawListPtr drawList, AppSkin ui, Rect card, float cell, int cellIndex,
        int[]? numbers, int autoMask, int stampedMask, BingoRoundPlayback playback, int cardIndex, float scale)
    {
        var column = BingoRules.ColumnOfCell(cellIndex);
        var row = BingoRules.RowOfCell(cellIndex);
        var min = new Vector2(card.Min.X + column * cell, card.Min.Y + row * cell);
        var center = new Vector2(min.X + cell * 0.5f, min.Y + cell * 0.5f);
        var tint = ColumnTints[column];

        if (cellIndex == BingoRules.FreeCell)
        {
            // The middle square is given, so it reads as a mark that was already made rather than
            // as a hole where a number should be.
            drawList.AddCircleFilled(center, cell * 0.40f,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.55f)), 32);
            Typography.DrawCentered(drawList, center, FreeMark, ui.Palette.HeaderInk,
                ScaleForRadius(cell * 0.40f) * 0.72f, FontWeight.Bold);
            return;
        }

        var slot = BingoRules.SlotForCell(cellIndex);
        if (numbers is null || slot < 0 || slot >= numbers.Length || !BingoRules.IsBall(numbers[slot]))
        {
            return;
        }

        var number = numbers[slot];
        var marked = (stampedMask & (1 << cellIndex)) != 0;
        var pending = !marked && (autoMask & (1 << cellIndex)) != 0;

        if (marked)
        {
            var pop = playback.PopOf(cardIndex, cellIndex);
            var progress = pop <= 0f ? 1f : Easing.EaseOutBack(1f - pop / BingoRoundPlayback.PopSeconds);
            var radius = cell * 0.40f * MathF.Max(0.1f, progress);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.85f)), 24);
        }
        else if (pending)
        {
            var breath = 0.35f + 0.45f * Pulse.Wave(Pulse.Fast);
            drawList.AddCircle(center, cell * 0.40f,
                ImGui.GetColorU32(Palette.WithAlpha(tint, breath)), 24, 1.6f * scale);
        }

        var ink = marked ? ui.Palette.HeaderInk : ui.BodyInk;
        Typography.DrawCentered(drawList, center, GameNumber.Label(number), ink, ScaleForRadius(cell * 0.40f),
            marked ? FontWeight.SemiBold : FontWeight.Medium);
    }

    public static void DrawBallChip(ImDrawListPtr drawList, Vector2 center, float radius, int ball, float alpha,
        Vector4 ink)
    {
        var column = BingoRules.ColumnOfBall(ball);
        var tint = column >= 0 ? ColumnTints[column] : ink;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.85f * alpha)), 32);
        Typography.DrawCentered(drawList, center, GameNumber.Label(ball), Palette.WithAlpha(ink, alpha),
            ScaleForRadius(radius), FontWeight.Bold);
    }

    // A number drawn at a fixed size inside a circle that grows is the tell of a chip nobody sized:
    // the ink has to follow the geometry it sits in, so both the felt and the caller read the same
    // whatever the layout hands them.
    private static float ScaleForRadius(float radius)
    {
        var scale = radius / (13f * MathF.Max(0.5f, UiScale.Current));
        return Math.Clamp(scale, 0.66f, 3.00f);
    }
}
