using System.Numerics;
using Aetherphone.Apps.Games.Skyfall;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SkyfallBoardTests
{
    private const float Step = 1f / 120f;

    [Fact]
    public void ABlastGrowsHoldsAndShrinksInUnderASecond()
    {
        var board = new SkyfallBoard();
        board.StartGame();
        Assert.True(board.Fire(new Vector2(SkyfallBoard.BatteryX, 60f)));
        var elapsed = 0f;
        while (board.BlastCount == 0)
        {
            board.Update(Step);
            elapsed += Step;
            Assert.True(elapsed < 1f);
        }

        var peak = 0f;
        var lifetime = 0f;
        while (board.BlastCount > 0)
        {
            peak = MathF.Max(peak, board.GetBlast(0).Radius);
            board.Update(Step);
            lifetime += Step;
        }

        Assert.Equal(SkyfallBoard.BlastMaxRadius, peak, 2);
        Assert.InRange(lifetime, 0.8f, 0.95f);
    }

    [Fact]
    public void FireRefusesTargetsBelowTheBarrelAndCountsAmmo()
    {
        var board = new SkyfallBoard();
        board.StartGame();
        Assert.False(board.Fire(new Vector2(20f, SkyfallBoard.GroundY)));
        Assert.Equal(SkyfallBoard.AmmoPerWave, board.Ammo);
        Assert.True(board.Fire(new Vector2(20f, 50f)));
        Assert.Equal(SkyfallBoard.AmmoPerWave - 1, board.Ammo);
    }

    [Fact]
    public void ASingleImpactCanNeverTakeTwoCities()
    {
        for (var first = 0; first < SkyfallBoard.CityCount; first++)
        {
            for (var second = first + 1; second < SkyfallBoard.CityCount; second++)
            {
                var spacing = MathF.Abs(SkyfallBoard.CityX[first] - SkyfallBoard.CityX[second]);
                Assert.True(spacing > SkyfallBoard.CityHalfWidth * 2f);
            }
        }
    }

    [Theory]
    [InlineData(1, 9)]
    [InlineData(5, 17)]
    [InlineData(10, 26)]
    [InlineData(30, 26)]
    public void MeteorsPerWaveRampAndCap(int wave, int expected)
    {
        Assert.Equal(expected, SkyfallBoard.MeteorsForWave(wave));
    }

    [Fact]
    public void WaveBonusPaysCitiesAndSpareAmmo()
    {
        Assert.Equal(740, SkyfallBoard.WaveBonus(6, 28));
        Assert.Equal(0, SkyfallBoard.WaveBonus(0, 0));
        Assert.Equal(315, SkyfallBoard.WaveBonus(3, 3));
    }

    [Fact]
    public void AnUnopposedWaveEndsTheRunOnlyWhenEveryCityFalls()
    {
        var board = new SkyfallBoard();
        board.StartGame();
        var elapsed = 0f;
        while (!board.GameOver && elapsed < 600f)
        {
            board.Update(Step);
            elapsed += Step;
        }

        Assert.True(board.GameOver);
        Assert.Equal(0, board.CitiesLeft);
    }
}
