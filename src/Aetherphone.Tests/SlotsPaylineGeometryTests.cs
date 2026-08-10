using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SlotsPaylineGeometryTests
{
    private static readonly int[][] BackendPaylines =
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

    private const long BackendSingleWinCeiling = 500000;

    private static readonly long[,] BackendLinePays =
    {
        { 2, 10, 60 },
        { 2, 5, 30 },
        { 1, 4, 15 },
        { 1, 3, 10 },
        { 1, 2, 5 },
        { 1, 2, 4 },
        { 0, 1, 4 },
        { 0, 1, 3 },
    };

    [Fact]
    public void TheTenPaylinesMatchTheBackendLayoutTable()
    {
        Assert.Equal(BackendPaylines.Length, SlotsRules.Paylines.Length);
        Assert.Equal(SlotsRules.PaylineCount, SlotsRules.Paylines.Length);
        for (var lineIndex = 0; lineIndex < BackendPaylines.Length; lineIndex++)
        {
            Assert.Equal(BackendPaylines[lineIndex], SlotsRules.Paylines[lineIndex]);
        }
    }

    [Fact]
    public void EveryPaylineVisitsOneRowPerReel()
    {
        for (var lineIndex = 0; lineIndex < SlotsRules.Paylines.Length; lineIndex++)
        {
            var rows = SlotsRules.Paylines[lineIndex];
            Assert.Equal(SlotsRules.ReelCount, rows.Length);
            for (var reel = 0; reel < rows.Length; reel++)
            {
                Assert.InRange(rows[reel], 0, SlotsRules.RowCount - 1);
            }
        }
    }

    [Fact]
    public void ThePaytableMatchesTheBackendEngine()
    {
        Assert.Equal(BackendLinePays.GetLength(0), SlotsRules.LinePays.GetLength(0));
        Assert.Equal(BackendLinePays.GetLength(1), SlotsRules.LinePays.GetLength(1));
        for (var symbol = 0; symbol < BackendLinePays.GetLength(0); symbol++)
        {
            for (var column = 0; column < BackendLinePays.GetLength(1); column++)
            {
                Assert.Equal(BackendLinePays[symbol, column], SlotsRules.LinePays[symbol, column]);
            }
        }

        Assert.Equal(new long[] { 0, 0, 0, 1, 5, 25 }, SlotsRules.ScatterPays);
        Assert.Equal(new[] { 0, 0, 0, 8, 12, 20 }, SlotsRules.FreeSpinAwards);
    }

    [Fact]
    public void TheLayoutConstantsMatchTheBackendEngine()
    {
        Assert.Equal(5, SlotsRules.ReelCount);
        Assert.Equal(3, SlotsRules.RowCount);
        Assert.Equal(15, SlotsRules.CellCount);
        Assert.Equal(8, SlotsRules.SymbolCount);
        Assert.Equal(8, SlotsRules.WildSymbol);
        Assert.Equal(9, SlotsRules.ScatterSymbol);
        Assert.Equal(200, SlotsRules.PayoutCapMultiple);
        Assert.Equal(40, SlotsRules.FreeSpinCap);
        Assert.Equal(5, SlotsRules.RetriggerSpins);
    }

    [Fact]
    public void TheStakeBandTopsOutWhereTheCapMeetsTheSingleWinCeiling()
    {
        Assert.Equal(50, SlotsRules.MinStake);
        Assert.Equal(BackendSingleWinCeiling, SlotsRules.MaxStake * SlotsRules.PayoutCapMultiple);
        Assert.True(SlotsRules.IsStakeInRange(SlotsRules.DefaultStake));
        Assert.Equal(0, SlotsRules.DefaultStake % SlotsRules.StakeStep);
    }

    [Fact]
    public void AnyWholeAmountInsideTheBandIsAStake()
    {
        Assert.True(SlotsRules.IsStakeInRange(SlotsRules.MinStake));
        Assert.True(SlotsRules.IsStakeInRange(137));
        Assert.True(SlotsRules.IsStakeInRange(SlotsRules.MaxStake));
        Assert.False(SlotsRules.IsStakeInRange(SlotsRules.MinStake - 1));
        Assert.False(SlotsRules.IsStakeInRange(SlotsRules.MaxStake + 1));
        Assert.False(SlotsRules.IsStakeInRange(0));
        Assert.False(SlotsRules.IsStakeInRange(-1));
    }
}
