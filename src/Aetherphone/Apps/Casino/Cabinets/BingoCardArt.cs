using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

// A bingo card at phone size has twenty five cells inside about a hundred and seventy points, so
// nothing here can afford a label it does not need. The five columns are tinted instead of
// lettered: a called ball wears its column's tint on the rail and the matching cell answers in the
// same colour, which says which column a number belongs to without a glyph and without a word to
// translate. The mark itself is a filled disc that pops in on EaseOutBack, which is games only
// easing and stays inside the cabinet where it belongs.
internal static class BingoCardArt
{
    public const int Columns = BingoRules.Columns;

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
            drawList.AddCircleFilled(center, cell * 0.34f,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 24);
            return;
        }

        var slot = BingoRules.SlotForCell(cellIndex);
        var number = numbers is not null && slot >= 0 && slot < numbers.Length ? numbers[slot] : 0;
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
        Typography.DrawCentered(drawList, center, GameNumber.Label(number), ink, TextStyles.Caption2);
    }

    public static void DrawBallChip(ImDrawListPtr drawList, Vector2 center, float radius, int ball, float alpha,
        Vector4 ink)
    {
        var column = BingoRules.ColumnOfBall(ball);
        var tint = column >= 0 ? ColumnTints[column] : ink;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.85f * alpha)), 24);
        Typography.DrawCentered(drawList, center, GameNumber.Label(ball), Palette.WithAlpha(ink, alpha),
            TextStyles.Caption1);
    }
}
