using Aetherphone.Apps.Games.Hop;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HopBoardTests
{
    [Theory]
    [InlineData(12.5f, 0.6f, 0f, 1, true)]
    [InlineData(0.2f, 0.6f, 12.5f, 1, true)]
    [InlineData(3f, 0.6f, 5f, 2, false)]
    [InlineData(4.5f, 0.6f, 3f, 2, true)]
    public void SpansOverlapAcrossTheWrapSeam(float aStart, float aLength, float bStart, int bLength, bool expected)
    {
        Assert.Equal(expected, HopBoard.SpansOverlap(aStart, aLength, bStart, bLength));
    }

    [Theory]
    [InlineData(4f, 4f, 3, true)]
    [InlineData(6.7f, 4f, 3, true)]
    [InlineData(7.9f, 4f, 3, false)]
    [InlineData(3.6f, 4f, 3, true)]
    [InlineData(3.1f, 4f, 3, false)]
    [InlineData(12.6f, 0f, 3, true)]
    public void PadSupportHonoursGripSlackAndTheWrapSeam(float hopperX, float padX, int length, bool expected)
    {
        Assert.Equal(expected, HopBoard.PadSupports(hopperX, padX, length));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 0)]
    [InlineData(1.5f, -1)]
    [InlineData(2.1f, 1)]
    [InlineData(12f, 4)]
    public void DensCatchAFullCellEitherSide(float x, int expected)
    {
        Assert.Equal(expected, HopBoard.BayAt(x));
    }

    [Fact]
    public void BankPointsPayTheHopTheDenAndACappedTimeBonus()
    {
        Assert.Equal(300, HopBoard.BankPoints(45f));
        Assert.Equal(210, HopBoard.BankPoints(0.9f));
        Assert.Equal(230, HopBoard.BankPoints(10.4f));
    }

    [Fact]
    public void ALevelIsWorthAtMostTwoThousandFiveHundredAndFifty()
    {
        Assert.Equal(2550, HopBoard.PerLevelMaximum());
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(1, 1, 3)]
    [InlineData(0, 3, 3)]
    [InlineData(1, 9, 3)]
    public void RoadDensityRampsEveryTwoLevelsAndCapsAtThree(int lane, int level, int expected)
    {
        Assert.Equal(expected, HopBoard.RoadCountForLevel(lane, level));
    }

    [Fact]
    public void HoppingUpPaysOnlyForNewRowsAndTheFirstHopLeavesTheStartRow()
    {
        var board = new HopBoard();
        board.StartGame();
        Assert.Equal(HopBoard.StartRow, board.Row);
        board.Hop(0, 1);
        Assert.Equal(1, board.Row);
        Assert.Equal(HopBoard.HopPoints, board.Score);
        board.Hop(0, -1);
        board.Hop(0, 1);
        Assert.Equal(HopBoard.HopPoints, board.Score);
    }

    [Fact]
    public void TheTimerRunsOutIntoADeathAndRefillsOnRespawn()
    {
        var board = new HopBoard();
        board.StartGame();
        var elapsed = 0f;
        while (!board.Dying && elapsed < HopBoard.LifeTimerSeconds + 1f)
        {
            board.Tick(1f / 60f);
            elapsed += 1f / 60f;
        }

        Assert.True(board.Dying);
        Assert.Equal(HopBoard.StartLives - 1, board.Lives);
        while (board.Dying)
        {
            board.Tick(1f / 60f);
        }

        Assert.Equal(HopBoard.LifeTimerSeconds, board.TimerRemaining, 1);
        Assert.Equal(HopBoard.StartRow, board.Row);
    }
}
