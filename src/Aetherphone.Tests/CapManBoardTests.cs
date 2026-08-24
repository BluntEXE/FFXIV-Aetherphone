using System.Numerics;
using Aetherphone.Apps.Games.CapMan;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CapManBoardTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void TheLayoutHoldsOneHundredAndTwentySevenCollectables()
    {
        var board = new CapManBoard();
        board.StartGame();
        Assert.Equal(127, board.DotsLeft);
        Assert.Equal(1, board.Level);
        Assert.Equal(new Vector2(7f, 13f), board.PlayerPosition);
    }

    [Fact]
    public void ThePenDoorOpensOnlyForGhostsLeavingOrReturning()
    {
        var board = new CapManBoard();
        board.StartGame();
        Assert.False(board.Walkable(7, 8, false));
        Assert.False(board.Walkable(7, 8, true));
        Assert.True(board.Walkable(7, 8, true, true));
    }

    [Fact]
    public void TheTunnelWrapsOnRowSevenOnly()
    {
        var board = new CapManBoard();
        board.StartGame();
        Assert.True(board.Walkable(-1, 7, false));
        Assert.True(board.Walkable(CapManBoard.Columns, 7, false));
        Assert.False(board.Walkable(-1, 3, false));
    }

    [Fact]
    public void AQueuedTurnMovesThePlayerAndEatsDotsAfterTheReadyBeat()
    {
        var board = new CapManBoard();
        board.StartGame();
        Assert.True(board.Ready);
        Advance(board, CapManBoard.ReadySeconds + 0.05f);
        Assert.False(board.Ready);
        board.Turn(CapManBoard.Left);
        Advance(board, 0.5f);
        Assert.True(board.PlayerPosition.X < 7f);
        Assert.True(board.Score >= CapManBoard.DotPoints);
        Assert.True(board.DotsLeft < 127);
    }

    [Theory]
    [InlineData(1, 4.8f)]
    [InlineData(7, 6.0f)]
    [InlineData(8, 6.076f)]
    public void GhostSpeedRampsAndCapsBelowThePlayer(int level, float expected)
    {
        Assert.Equal(expected, CapManBoard.NormalGhostSpeed(level), 3);
    }

    [Theory]
    [InlineData(1, 6.5f)]
    [InlineData(10, 2f)]
    [InlineData(20, 2f)]
    public void FrightDurationShrinksToAFloor(int level, float expected)
    {
        Assert.Equal(expected, CapManBoard.FrightDuration(level), 3);
    }

    [Fact]
    public void GhostChainDoublesToSixteenHundred()
    {
        Assert.Equal(200, CapManBoard.ChainPoints(1));
        Assert.Equal(400, CapManBoard.ChainPoints(2));
        Assert.Equal(800, CapManBoard.ChainPoints(3));
        Assert.Equal(1600, CapManBoard.ChainPoints(4));
    }

    private static void Advance(CapManBoard board, float seconds)
    {
        var elapsed = 0f;
        while (elapsed < seconds)
        {
            board.Tick(Step);
            elapsed += Step;
        }
    }
}
