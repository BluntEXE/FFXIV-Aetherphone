using System.Numerics;
using Aetherphone.Apps.Games.Invaders;
using Xunit;

namespace Aetherphone.Tests;

public sealed class InvadersBoardTests
{
    private const float Step = 1f / 120f;

    [Fact]
    public void TheFormationStartsAtTheTopLeftAndDropsWithEachWaveUpToACap()
    {
        var board = new InvadersBoard();
        board.StartGame();
        Assert.Equal(new Vector2(6f, 14f), board.InvaderPosition(0, 0));
        Assert.Equal(InvadersBoard.InvaderCount, board.AliveCount);
        Assert.Equal(1, board.Wave);
        board.FormationBounds(out var minX, out var maxX);
        Assert.Equal(6f, minX);
        Assert.Equal(6f + 6 * InvadersBoard.ColumnPitch + InvadersBoard.InvaderWidth, maxX);
    }

    [Theory]
    [InlineData(1, 35, 0.59f)]
    [InlineData(1, 1, 0.055f)]
    [InlineData(19, 35, 0.055f)]
    public void TheStepIntervalShrinksWithTheRankAndFloors(int wave, int alive, float expected)
    {
        Assert.Equal(expected, InvadersBoard.StepInterval(wave, alive), 3);
    }

    [Fact]
    public void APerfectWavePaysEightHundredAndThirty()
    {
        Assert.Equal(830, InvadersBoard.PerfectWavePoints());
    }

    [Fact]
    public void OnlyOneBulletMayBeInTheAir()
    {
        var board = new InvadersBoard();
        board.StartGame();
        Assert.True(board.Fire());
        Assert.False(board.Fire());
    }

    [Fact]
    public void ABulletUnderABunkerChipsExactlyOneCellAndStopsThere()
    {
        var board = new InvadersBoard();
        board.StartGame();
        var target = InvadersBoard.ShieldX[1];
        var direction = target < board.PlayerX ? -1f : 1f;
        while (MathF.Abs(board.PlayerX - target) > 0.2f)
        {
            board.Move(direction, Step);
        }

        Assert.True(board.Fire());
        var elapsed = 0f;
        while (board.HasBullet && elapsed < 1f)
        {
            board.Update(Step);
            elapsed += Step;
        }

        Assert.False(board.HasBullet);
        Assert.Equal(InvadersBoard.ShieldCellCount - 1, CountShieldCells(board));
        Assert.Equal(InvadersBoard.InvaderCount, board.AliveCount);
        Assert.Equal(0, board.Score);
    }

    [Fact]
    public void ShieldIndexIsUniquePerCell()
    {
        var seen = new bool[InvadersBoard.ShieldCellCount];
        for (var shield = 0; shield < InvadersBoard.ShieldCount; shield++)
        {
            for (var column = 0; column < InvadersBoard.ShieldColumns; column++)
            {
                for (var row = 0; row < InvadersBoard.ShieldRows; row++)
                {
                    var index = InvadersBoard.ShieldIndex(shield, column, row);
                    Assert.False(seen[index]);
                    seen[index] = true;
                }
            }
        }
    }

    private static int CountShieldCells(InvadersBoard board)
    {
        var count = 0;
        for (var shield = 0; shield < InvadersBoard.ShieldCount; shield++)
        {
            for (var column = 0; column < InvadersBoard.ShieldColumns; column++)
            {
                for (var row = 0; row < InvadersBoard.ShieldRows; row++)
                {
                    if (board.ShieldCellAlive(shield, column, row))
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }
}
