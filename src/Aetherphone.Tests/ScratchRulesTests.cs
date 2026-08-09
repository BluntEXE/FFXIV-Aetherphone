using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ScratchRulesTests
{
    [Fact]
    public void ConstantsMatchTheBackendEngine()
    {
        Assert.Equal(3, ScratchRules.TierCount);
        Assert.Equal(9, ScratchRules.CellCount);
        Assert.Equal(3, ScratchRules.GridSide);
        Assert.Equal(5, ScratchRules.PrizeSymbolCount);
        Assert.Equal(7, ScratchRules.SymbolCount);
        Assert.Equal(1_000_000, ScratchRules.TableScale);
        Assert.Equal(3, ScratchRules.MatchesToWin);
        Assert.Equal(new long[] { 10, 25, 50 }, ScratchRules.Prices);
    }

    [Fact]
    public void PrizeTablesMatchTheBackendLiterals()
    {
        var expected = new (long Chips, int CountPerMillion)[][]
        {
            new[] { (10L, 200_000), (20L, 100_000), (50L, 40_000), (100L, 12_000), (200L, 8_000) },
            new[] { (25L, 210_000), (50L, 110_000), (125L, 40_000), (250L, 15_000), (500L, 6_000) },
            new[] { (50L, 220_000), (100L, 115_000), (250L, 44_000), (500L, 17_000), (1_000L, 4_000) },
        };
        for (var tier = 0; tier < ScratchRules.TierCount; tier++)
        {
            Assert.Equal(expected[tier].Length, ScratchRules.PrizeTables[tier].Length);
            for (var prizeIndex = 0; prizeIndex < expected[tier].Length; prizeIndex++)
            {
                Assert.Equal(expected[tier][prizeIndex].Chips, ScratchRules.PrizeTables[tier][prizeIndex].Chips);
                Assert.Equal(expected[tier][prizeIndex].CountPerMillion,
                    ScratchRules.PrizeTables[tier][prizeIndex].CountPerMillion);
            }
        }
    }

    [Fact]
    public void WinCountsSumTheTierTables()
    {
        Assert.Equal(360_000, ScratchRules.WinCountPerMillion(0));
        Assert.Equal(381_000, ScratchRules.WinCountPerMillion(1));
        Assert.Equal(400_000, ScratchRules.WinCountPerMillion(2));
    }

    [Fact]
    public void TierForPriceRoundTripsAndRejectsUnknownPrices()
    {
        Assert.Equal(0, ScratchRules.TierForPrice(10));
        Assert.Equal(1, ScratchRules.TierForPrice(25));
        Assert.Equal(2, ScratchRules.TierForPrice(50));
        Assert.Equal(-1, ScratchRules.TierForPrice(20));
        Assert.True(ScratchRules.IsValidTier(0));
        Assert.True(ScratchRules.IsValidTier(2));
        Assert.False(ScratchRules.IsValidTier(-1));
        Assert.False(ScratchRules.IsValidTier(3));
    }

    [Fact]
    public void WinningSymbolFindsTheTripleAndOnlyTheTriple()
    {
        Assert.Equal(2, ScratchRules.WinningSymbol(new[] { 2, 0, 0, 1, 1, 3, 3, 2, 2 }));
        Assert.Equal(-1, ScratchRules.WinningSymbol(new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4 }));
    }

    [Fact]
    public void CellValidationRejectsMalformedGrids()
    {
        Assert.True(ScratchRules.AreValidCells(new[] { 0, 1, 2, 3, 4, 5, 6, 0, 1 }));
        Assert.False(ScratchRules.AreValidCells(new[] { 0, 1, 2 }));
        Assert.False(ScratchRules.AreValidCells(new[] { 0, 1, 2, 3, 4, 5, 7, 0, 1 }));
        Assert.False(ScratchRules.AreValidCells(new[] { 0, 1, 2, 3, 4, 5, -1, 0, 1 }));
    }
}
