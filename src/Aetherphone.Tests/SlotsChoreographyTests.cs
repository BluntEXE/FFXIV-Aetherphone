using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SlotsChoreographyTests
{
    [Fact]
    public void ReelStopsAnchorAtTheRatifiedMarks()
    {
        var choreography = new SlotsChoreography();
        choreography.Begin(false);
        Assert.Equal(0.9f, choreography.StopSeconds(0), 3);
        Assert.Equal(1.4f, choreography.StopSeconds(2), 3);
        Assert.Equal(1.9f, choreography.StopSeconds(4), 3);
    }

    [Fact]
    public void AnticipationHoldsOnlyTheLastReel()
    {
        var choreography = new SlotsChoreography();
        choreography.Begin(true);
        Assert.Equal(0.9f, choreography.StopSeconds(0), 3);
        Assert.Equal(1.65f, choreography.StopSeconds(3), 3);
        Assert.Equal(2.5f, choreography.StopSeconds(4), 3);
    }

    [Fact]
    public void ReelsStopLeftToRightAndThenSettle()
    {
        var choreography = new SlotsChoreography();
        choreography.Begin(false);
        choreography.Update(1.0f);
        Assert.True(choreography.ReelStopped(0));
        Assert.False(choreography.ReelStopped(1));
        Assert.False(choreography.Settled);
        choreography.Update(0.5f);
        Assert.True(choreography.ReelStopped(2));
        Assert.False(choreography.ReelStopped(3));
        choreography.Update(0.5f);
        Assert.True(choreography.ReelStopped(4));
        Assert.True(choreography.Settled);
        Assert.False(choreography.Running);
    }

    [Fact]
    public void AnticipationGatesTheSettleUntilTheHoldElapses()
    {
        var choreography = new SlotsChoreography();
        choreography.Begin(true);
        choreography.Update(2.0f);
        Assert.True(choreography.ReelStopped(3));
        Assert.False(choreography.ReelStopped(4));
        Assert.True(choreography.Anticipating);
        Assert.False(choreography.Settled);
        choreography.Update(0.6f);
        Assert.True(choreography.Settled);
        Assert.False(choreography.Anticipating);
    }

    [Fact]
    public void WithoutAnticipationTheHoldNeverEngages()
    {
        var choreography = new SlotsChoreography();
        choreography.Begin(false);
        choreography.Update(1.8f);
        Assert.False(choreography.Anticipating);
        choreography.Update(0.2f);
        Assert.True(choreography.Settled);
    }

    [Fact]
    public void LandingProgressRampsInsideTheLandingWindow()
    {
        var choreography = new SlotsChoreography();
        choreography.Begin(false);
        choreography.Update(0.5f);
        Assert.Equal(0f, choreography.LandingProgress(0));
        choreography.Update(0.25f);
        Assert.InRange(choreography.LandingProgress(0), 0.4f, 0.6f);
        choreography.Update(0.2f);
        Assert.Equal(1f, choreography.LandingProgress(0));
    }

    [Fact]
    public void FinishSnapsEveryReelToItsStop()
    {
        var choreography = new SlotsChoreography();
        choreography.Begin(true);
        choreography.Update(0.3f);
        choreography.Finish();
        Assert.True(choreography.Settled);
        for (var reel = 0; reel < 5; reel++)
        {
            Assert.True(choreography.ReelStopped(reel));
        }
    }

    [Fact]
    public void PlaybackRunsSpinPresentSettleForABaseOnlyRound()
    {
        var playback = new SlotsRoundPlayback();
        Assert.True(playback.Begin(Round(stake: 2, baseWin: 0, scatterCount: 0)));
        Assert.Equal(SlotsPlaybackPhase.Spinning, playback.Phase);
        Assert.False(playback.Choreography.HoldsLastReel);
        playback.Update(2.0f);
        Assert.Equal(SlotsPlaybackPhase.Presenting, playback.Phase);
        playback.Update(0.5f);
        Assert.Equal(SlotsPlaybackPhase.Finished, playback.Phase);
        Assert.Equal(0, playback.CommittedWin);
    }

    [Fact]
    public void BigWinPendingHoldsTheLastReel()
    {
        var playback = new SlotsRoundPlayback();
        Assert.True(playback.Begin(Round(stake: 2, baseWin: 20, scatterCount: 0)));
        Assert.True(playback.Choreography.HoldsLastReel);
        playback.Update(2.0f);
        Assert.Equal(SlotsPlaybackPhase.Spinning, playback.Phase);
        playback.Update(0.6f);
        Assert.Equal(SlotsPlaybackPhase.Presenting, playback.Phase);
        Assert.Equal(20, playback.CommittedWin);
    }

    [Fact]
    public void SmallWinsSpinWithoutTheHold()
    {
        var playback = new SlotsRoundPlayback();
        Assert.True(playback.Begin(Round(stake: 2, baseWin: 4, scatterCount: 0)));
        Assert.False(playback.Choreography.HoldsLastReel);
    }

    [Fact]
    public void BonusPlaybackWalksEveryFreeSpinAtTheFastCadence()
    {
        var playback = new SlotsRoundPlayback();
        Assert.True(playback.Begin(Round(stake: 1, baseWin: 1, scatterCount: 3, freeSpinWins: new long[] { 2, 0 })));
        Assert.True(playback.Choreography.HoldsLastReel);
        Assert.Equal(2, playback.FreeSpinCount);
        Assert.Equal(8, playback.BonusAwarded);

        playback.Update(2.6f);
        Assert.Equal(SlotsPlaybackPhase.Presenting, playback.Phase);
        Assert.Equal(1, playback.CommittedWin);
        playback.Update(1.6f);
        Assert.Equal(SlotsPlaybackPhase.BonusIntro, playback.Phase);
        Assert.False(playback.InBonus);

        playback.Update(1.4f);
        Assert.Equal(SlotsPlaybackPhase.Spinning, playback.Phase);
        Assert.Equal(1, playback.SpinIndex);
        Assert.True(playback.InBonus);
        Assert.True(playback.SkipAvailable);
        Assert.Equal(0.88f, playback.Choreography.StopSeconds(4), 3);

        playback.Update(1.0f);
        Assert.Equal(SlotsPlaybackPhase.Presenting, playback.Phase);
        Assert.Equal(3, playback.CommittedWin);
        playback.Update(1.6f);
        Assert.Equal(SlotsPlaybackPhase.Spinning, playback.Phase);
        Assert.Equal(2, playback.SpinIndex);
        playback.Update(1.0f);
        Assert.Equal(SlotsPlaybackPhase.Presenting, playback.Phase);
        playback.Update(0.5f);
        Assert.Equal(SlotsPlaybackPhase.Finished, playback.Phase);
        Assert.Equal(3, playback.CommittedWin);
    }

    [Fact]
    public void SkipJumpsStraightToTheServerTotal()
    {
        var playback = new SlotsRoundPlayback();
        Assert.True(playback.Begin(Round(stake: 1, baseWin: 1, scatterCount: 3, freeSpinWins: new long[] { 2, 4 })));
        playback.Update(2.6f);
        playback.Update(1.6f);
        Assert.Equal(SlotsPlaybackPhase.BonusIntro, playback.Phase);
        Assert.True(playback.SkipAvailable);
        playback.Skip();
        Assert.Equal(SlotsPlaybackPhase.Finished, playback.Phase);
        Assert.Equal(2, playback.SpinIndex);
        Assert.Equal(7, playback.CommittedWin);
        Assert.True(playback.Choreography.Settled);
    }

    [Fact]
    public void TheCommittedTallyNeverPassesTheCappedTotal()
    {
        var round = Round(stake: 1, baseWin: 150, scatterCount: 3, freeSpinWins: new long[] { 80, 60 });
        round = round with { TotalWin = 200, CapApplied = true };
        var playback = new SlotsRoundPlayback();
        Assert.True(playback.Begin(round));
        playback.Update(3.0f);
        Assert.Equal(150, playback.CommittedWin);
        playback.Update(1.6f);
        playback.Update(1.4f);
        playback.Update(2.0f);
        Assert.Equal(200, playback.CommittedWin);
        playback.Skip();
        Assert.Equal(200, playback.CommittedWin);
        Assert.True(playback.CapApplied);
    }

    [Fact]
    public void RefusalsAndMalformedGridsNeverStartAPlayback()
    {
        var playback = new SlotsRoundPlayback();
        Assert.False(playback.Begin(new CasinoSlotsSpinDto(Granted: false, Reason: "cooldown")));
        Assert.False(playback.Begin(new CasinoSlotsSpinDto(Granted: true,
            BaseSpin: new CasinoSlotsSpinResultDto(Grid: new int[3]))));
        Assert.Equal(SlotsPlaybackPhase.Idle, playback.Phase);
    }

    private static CasinoSlotsSpinDto Round(long stake, long baseWin, int scatterCount,
        long[]? freeSpinWins = null)
    {
        var baseSpin = new CasinoSlotsSpinResultDto(
            Grid: UniformGrid(0),
            LineWins: Array.Empty<CasinoSlotsLineWinDto>(),
            ScatterCount: scatterCount,
            ScatterPay: 0,
            Win: baseWin,
            SpinsAdded: scatterCount >= 3 ? 8 : 0);
        var freeSpins = Array.Empty<CasinoSlotsSpinResultDto>();
        var total = baseWin;
        if (freeSpinWins is not null)
        {
            freeSpins = new CasinoSlotsSpinResultDto[freeSpinWins.Length];
            for (var spinIndex = 0; spinIndex < freeSpinWins.Length; spinIndex++)
            {
                freeSpins[spinIndex] = new CasinoSlotsSpinResultDto(
                    Grid: UniformGrid(1),
                    LineWins: Array.Empty<CasinoSlotsLineWinDto>(),
                    ScatterCount: 0,
                    ScatterPay: 0,
                    Win: freeSpinWins[spinIndex],
                    SpinsAdded: 0);
                total += freeSpinWins[spinIndex];
            }
        }

        return new CasinoSlotsSpinDto(
            Granted: true,
            Reason: string.Empty,
            RoundId: "r1",
            Stake: stake,
            BaseSpin: baseSpin,
            FreeSpins: freeSpins,
            TotalWin: total,
            CapApplied: false,
            NextSeedHash: "next",
            Stack: 100);
    }

    private static int[] UniformGrid(int symbol)
    {
        var grid = new int[15];
        for (var cellIndex = 0; cellIndex < grid.Length; cellIndex++)
        {
            grid[cellIndex] = symbol;
        }

        return grid;
    }
}
