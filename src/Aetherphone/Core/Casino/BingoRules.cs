namespace Aetherphone.Core.Casino;

// The client half of the bingo contract, mirrored from the backend engine so the hall can paint a
// card, daub it, and quote the ladder without asking the server what any of it means.
//
// Two rules in here carry the money and both are disclosed on screen. The stage rates are tenths
// of a chip per card in play (2.8, 3.7 and 8.0 for a line, two lines and a full house), and
// PrizeCardCap is where that scaling stops: eight times 125 is exactly the single win ceiling, so
// the top prize reaches the house maximum precisely at the cap and never crosses it. Past the cap
// the stakes keep growing while the prizes stand still, which changes what a card is worth, so the
// cabinet says the cap out loud rather than letting a full hall imply a bigger pot than it pays.
//
// Daubing here is presentation only. A mark is derived from the balls the room has called and the
// numbers printed on the card, never from what a finger touched, so the settlement the server
// sends and the board the player reads cannot disagree.
internal static class BingoRules
{
    public const int Balls = 75;

    public const int Columns = 5;

    public const int Cells = 25;

    public const int CardNumbers = 24;

    public const int FreeCell = 12;

    public const int NumbersPerColumn = 15;

    public const long CardPrice = 20;

    public const int MaxCards = 4;

    public const int PrizeCardCap = 125;

    public const int StageLine = 0;

    public const int StageTwoLines = 1;

    public const int StageFullHouse = 2;

    public const int StageCount = 3;

    public const int BallIntervalSeconds = 3;

    public const long MaxSingleWin = 1000;

    public const int FreeMask = 1 << FreeCell;

    public const int FullMask = (1 << Cells) - 1;

    public static readonly int[] StageRateTenths = { 28, 37, 80 };

    public static readonly int[] CardCells =
    {
        0, 1, 2, 3, 4,
        5, 6, 7, 8, 9,
        10, 11, 13, 14,
        15, 16, 17, 18, 19,
        20, 21, 22, 23, 24,
    };

    public static readonly int[] LineMasks = BuildLineMasks();

    public static bool IsBall(int ball)
    {
        return ball >= 1 && ball <= Balls;
    }

    public static bool IsCell(int cell)
    {
        return cell >= 0 && cell < Cells;
    }

    public static bool IsStage(int stage)
    {
        return stage >= 0 && stage < StageCount;
    }

    public static bool IsValidCardCount(int cardCount)
    {
        return cardCount >= 1 && cardCount <= MaxCards;
    }

    public static long StakeFor(int cardCount)
    {
        return CardPrice * cardCount;
    }

    public static int CellForSlot(int slot)
    {
        return slot >= 0 && slot < CardNumbers ? CardCells[slot] : -1;
    }

    // The free centre owns no slot, so it answers -1 rather than borrowing its neighbour's number.
    public static int SlotForCell(int cell)
    {
        if (!IsCell(cell) || cell == FreeCell)
        {
            return -1;
        }

        return cell < FreeCell ? cell : cell - 1;
    }

    public static int ColumnFloorFor(int column)
    {
        return column * NumbersPerColumn + 1;
    }

    public static int ColumnOfBall(int ball)
    {
        return IsBall(ball) ? (ball - 1) / NumbersPerColumn : -1;
    }

    public static int ColumnOfCell(int cell)
    {
        return IsCell(cell) ? cell / Columns : -1;
    }

    public static int RowOfCell(int cell)
    {
        return IsCell(cell) ? cell % Columns : -1;
    }

    public static long PrizeFor(int stage, int cardsInPlay)
    {
        if (!IsStage(stage) || cardsInPlay <= 0)
        {
            return 0;
        }

        var scaled = cardsInPlay < PrizeCardCap ? cardsInPlay : PrizeCardCap;
        var prize = (StageRateTenths[stage] * (long)scaled + 9) / 10;
        return prize > MaxSingleWin ? MaxSingleWin : prize;
    }

    public static void PrizeLadder(int cardsInPlay, Span<long> ladder)
    {
        for (var stage = 0; stage < StageCount && stage < ladder.Length; stage++)
        {
            ladder[stage] = PrizeFor(stage, cardsInPlay);
        }
    }

    public static bool PrizesFrozen(int cardsInPlay)
    {
        return cardsInPlay >= PrizeCardCap;
    }

    public static void MarkCalled(int[]? balls, Span<bool> called)
    {
        called.Clear();
        if (balls is null)
        {
            return;
        }

        for (var ballIndex = 0; ballIndex < balls.Length; ballIndex++)
        {
            var ball = balls[ballIndex];
            if (IsBall(ball) && ball < called.Length)
            {
                called[ball] = true;
            }
        }
    }

    // The settlement mask: the free centre plus every printed number the room has called. Nothing
    // the player did on the glass reaches this, which is the whole reason hand daubing is safe to
    // offer at all.
    public static int AutoMask(int[]? card, ReadOnlySpan<bool> called)
    {
        var mask = FreeMask;
        if (card is null)
        {
            return mask;
        }

        var slots = card.Length < CardNumbers ? card.Length : CardNumbers;
        for (var slot = 0; slot < slots; slot++)
        {
            var number = card[slot];
            if (number >= 0 && number < called.Length && called[number])
            {
                mask |= 1 << CardCells[slot];
            }
        }

        return mask;
    }

    public static int AutoMask(int[]? card, int[]? balls)
    {
        Span<bool> called = stackalloc bool[Balls + 1];
        MarkCalled(balls, called);
        return AutoMask(card, called);
    }

    public static bool IsMarked(int mask, int cell)
    {
        return IsCell(cell) && (mask & (1 << cell)) != 0;
    }

    public static int LinesOn(int mask)
    {
        var lines = 0;
        for (var index = 0; index < LineMasks.Length; index++)
        {
            if ((mask & LineMasks[index]) == LineMasks[index])
            {
                lines++;
            }
        }

        return lines;
    }

    public static int CellsRemaining(int mask)
    {
        return CountBits(FullMask & ~mask);
    }

    // How close the nearest unfinished line is, so the board can whisper "one away" without ever
    // claiming a line the room has not actually completed. A card that already holds every line
    // answers zero.
    public static int ClosestLineGap(int mask)
    {
        var closest = Columns;
        for (var index = 0; index < LineMasks.Length; index++)
        {
            var missing = CountBits(LineMasks[index] & ~mask);
            if (missing < closest)
            {
                closest = missing;
            }
        }

        return closest;
    }

    public static int StageReached(int mask)
    {
        if (CellsRemaining(mask) == 0)
        {
            return StageFullHouse;
        }

        var lines = LinesOn(mask);
        if (lines >= 2)
        {
            return StageTwoLines;
        }

        return lines >= 1 ? StageLine : -1;
    }

    internal static int CountBits(int mask)
    {
        var count = 0;
        var remaining = mask;
        while (remaining != 0)
        {
            remaining &= remaining - 1;
            count++;
        }

        return count;
    }

    private static int[] BuildLineMasks()
    {
        var masks = new int[12];
        var index = 0;
        for (var row = 0; row < Columns; row++)
        {
            var mask = 0;
            for (var column = 0; column < Columns; column++)
            {
                mask |= 1 << (column * Columns + row);
            }

            masks[index] = mask;
            index++;
        }

        for (var column = 0; column < Columns; column++)
        {
            var mask = 0;
            for (var row = 0; row < Columns; row++)
            {
                mask |= 1 << (column * Columns + row);
            }

            masks[index] = mask;
            index++;
        }

        var falling = 0;
        var rising = 0;
        for (var step = 0; step < Columns; step++)
        {
            falling |= 1 << (step * Columns + step);
            rising |= 1 << (step * Columns + (Columns - 1 - step));
        }

        masks[index] = falling;
        masks[index + 1] = rising;
        return masks;
    }
}
