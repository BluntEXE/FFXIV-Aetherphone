namespace Aetherphone.Core.Casino;

internal readonly record struct ScratchPrizeRow(long Chips, int CountPerMillion);

internal static class ScratchRules
{
    public const int TierCount = 3;

    public const int CellCount = 9;

    public const int GridSide = 3;

    public const int PrizeSymbolCount = 5;

    public const int SymbolCount = 7;

    public const int TableScale = 1_000_000;

    public const int MatchesToWin = 3;

    public static readonly long[] Prices = { 10, 25, 50 };

    public static readonly ScratchPrizeRow[][] PrizeTables =
    {
        new ScratchPrizeRow[]
        {
            new(10, 200_000),
            new(20, 100_000),
            new(50, 40_000),
            new(100, 12_000),
            new(200, 8_000),
        },
        new ScratchPrizeRow[]
        {
            new(25, 210_000),
            new(50, 110_000),
            new(125, 40_000),
            new(250, 15_000),
            new(500, 6_000),
        },
        new ScratchPrizeRow[]
        {
            new(50, 220_000),
            new(100, 115_000),
            new(250, 44_000),
            new(500, 17_000),
            new(1_000, 4_000),
        },
    };

    public static bool IsValidTier(int tier)
    {
        return tier >= 0 && tier < TierCount;
    }

    public static int TierForPrice(long price)
    {
        for (var tier = 0; tier < TierCount; tier++)
        {
            if (Prices[tier] == price)
            {
                return tier;
            }
        }

        return -1;
    }

    public static long WinCountPerMillion(int tier)
    {
        var table = PrizeTables[tier];
        var total = 0L;
        for (var prizeIndex = 0; prizeIndex < table.Length; prizeIndex++)
        {
            total += table[prizeIndex].CountPerMillion;
        }

        return total;
    }

    public static bool AreValidCells(ReadOnlySpan<int> cells)
    {
        if (cells.Length != CellCount)
        {
            return false;
        }

        for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            if (cells[cellIndex] < 0 || cells[cellIndex] >= SymbolCount)
            {
                return false;
            }
        }

        return true;
    }

    public static int WinningSymbol(ReadOnlySpan<int> cells)
    {
        Span<int> counts = stackalloc int[SymbolCount];
        for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            counts[cells[cellIndex]]++;
        }

        for (var symbol = 0; symbol < SymbolCount; symbol++)
        {
            if (counts[symbol] >= MatchesToWin)
            {
                return symbol;
            }
        }

        return -1;
    }
}
