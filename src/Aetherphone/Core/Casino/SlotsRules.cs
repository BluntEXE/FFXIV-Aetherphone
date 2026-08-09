namespace Aetherphone.Core.Casino;

internal static class SlotsRules
{
    public const int ReelCount = 5;

    public const int RowCount = 3;

    public const int CellCount = 15;

    public const int StopsPerReel = 40;

    public const int SymbolCount = 8;

    public const int WildSymbol = 8;

    public const int ScatterSymbol = 9;

    public const int PaylineCount = 10;

    public const long PayoutCapMultiple = 200;

    public const int FreeSpinCap = 40;

    public const int RetriggerSpins = 5;

    public static readonly long[] StakeTiers = { 1, 2, 5 };

    public static readonly int[][] Paylines =
    {
        new[] { 1, 1, 1, 1, 1 },
        new[] { 0, 0, 0, 0, 0 },
        new[] { 2, 2, 2, 2, 2 },
        new[] { 0, 1, 2, 1, 0 },
        new[] { 2, 1, 0, 1, 2 },
        new[] { 0, 0, 1, 2, 2 },
        new[] { 2, 2, 1, 0, 0 },
        new[] { 1, 0, 1, 2, 1 },
        new[] { 1, 2, 1, 0, 1 },
        new[] { 2, 1, 1, 1, 0 },
    };

    public static readonly long[,] LinePays =
    {
        { 2, 8, 40 },
        { 2, 5, 25 },
        { 1, 4, 15 },
        { 1, 3, 10 },
        { 1, 2, 5 },
        { 1, 2, 4 },
        { 0, 1, 4 },
        { 0, 1, 3 },
    };

    public static readonly long[] ScatterPays = { 0, 0, 0, 1, 5, 25 };

    public static readonly int[] FreeSpinAwards = { 0, 0, 0, 8, 12, 20 };

    public static bool IsStakeTier(long stake)
    {
        for (var tierIndex = 0; tierIndex < StakeTiers.Length; tierIndex++)
        {
            if (StakeTiers[tierIndex] == stake)
            {
                return true;
            }
        }

        return false;
    }
}
