using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

// The one thing this machine exists to guarantee: a seat bound to another device never becomes this
// device's seat without somebody pressing the button. Everything else here is the supporting cast
// that keeps that guarantee honest under a snapshot arriving many times a second.
public sealed class CasinoSeatMachineTests
{
    [Fact]
    public void ASeatBoundElsewhereNeverBecomesMineWithoutAPress()
    {
        var stage = CasinoSeatStage.Elsewhere;
        foreach (var signal in AllSignalsExcept(CasinoSeatSignal.TakeOverRequested,
                     CasinoSeatSignal.SeatBoundHere, CasinoSeatSignal.SeatLost, CasinoSeatSignal.Left))
        {
            Assert.Equal(CasinoSeatStage.Elsewhere, CasinoSeatMachine.Next(stage, signal));
        }

        Assert.Equal(CasinoSeatStage.Claiming,
            CasinoSeatMachine.Next(stage, CasinoSeatSignal.TakeOverRequested));
    }

    // A claim grant that nobody asked for cannot arrive, and if one ever did it must not move the
    // screen: the takeover is the gesture, not the message.
    [Fact]
    public void AClaimGrantWithoutAClaimChangesNothing()
    {
        Assert.Equal(CasinoSeatStage.Elsewhere,
            CasinoSeatMachine.Next(CasinoSeatStage.Elsewhere, CasinoSeatSignal.ClaimGranted));
        Assert.Equal(CasinoSeatStage.Watching,
            CasinoSeatMachine.Next(CasinoSeatStage.Watching, CasinoSeatSignal.ClaimGranted));
        Assert.Equal(CasinoSeatStage.Seated,
            CasinoSeatMachine.Next(CasinoSeatStage.Seated, CasinoSeatSignal.ClaimGranted));
    }

    [Fact]
    public void TheClaimRoundTripLandsOnTheSeatAndARefusalPutsItBack()
    {
        var stage = CasinoSeatMachine.Next(CasinoSeatStage.Watching, CasinoSeatSignal.SeatBoundElsewhere);
        Assert.Equal(CasinoSeatStage.Elsewhere, stage);

        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.TakeOverRequested);
        Assert.Equal(CasinoSeatStage.Claiming, stage);
        Assert.True(CasinoSeatMachine.Busy(stage));
        Assert.True(CasinoSeatMachine.ShowsTakeOver(stage));

        Assert.Equal(CasinoSeatStage.Seated, CasinoSeatMachine.Next(stage, CasinoSeatSignal.ClaimGranted));
        Assert.Equal(CasinoSeatStage.Elsewhere, CasinoSeatMachine.Next(stage, CasinoSeatSignal.ClaimRefused));
    }

    [Fact]
    public void TheServerHandingTheBindingBackIsNotATakeover()
    {
        Assert.Equal(CasinoSeatStage.Seated,
            CasinoSeatMachine.Next(CasinoSeatStage.Elsewhere, CasinoSeatSignal.SeatBoundHere));
    }

    [Fact]
    public void ASeatedPlayerLosingTheBindingIsToldRatherThanSilentlyDropped()
    {
        Assert.Equal(CasinoSeatStage.Elsewhere,
            CasinoSeatMachine.Next(CasinoSeatStage.Seated, CasinoSeatSignal.SeatBoundElsewhere));
        Assert.False(CasinoSeatMachine.Holds(CasinoSeatStage.Elsewhere));
    }

    [Fact]
    public void ASitInFlightIsNotCancelledByASnapshotThatHasNotCaughtUp()
    {
        var stage = CasinoSeatMachine.Next(CasinoSeatStage.Watching, CasinoSeatSignal.SitRequested);
        Assert.Equal(CasinoSeatStage.Sitting, stage);
        Assert.Equal(CasinoSeatStage.Sitting, CasinoSeatMachine.Next(stage, CasinoSeatSignal.SeatLost));
        Assert.Equal(CasinoSeatStage.Seated, CasinoSeatMachine.Next(stage, CasinoSeatSignal.SitGranted));
        Assert.Equal(CasinoSeatStage.Watching, CasinoSeatMachine.Next(stage, CasinoSeatSignal.SitRefused));
    }

    [Fact]
    public void AClaimInFlightIsNotCancelledByASnapshotEither()
    {
        Assert.Equal(CasinoSeatStage.Claiming,
            CasinoSeatMachine.Next(CasinoSeatStage.Claiming, CasinoSeatSignal.SeatLost));
    }

    [Fact]
    public void AStandAcceptedMidHandKeepsTheSeatUntilTheHandSettles()
    {
        var stage = CasinoSeatMachine.Next(CasinoSeatStage.Seated, CasinoSeatSignal.StandRequested);
        Assert.Equal(CasinoSeatStage.Standing, stage);
        Assert.True(CasinoSeatMachine.Holds(stage));
        Assert.Equal(CasinoSeatStage.Seated, CasinoSeatMachine.Next(stage, CasinoSeatSignal.StandQueued));
        Assert.Equal(CasinoSeatStage.Watching, CasinoSeatMachine.Next(stage, CasinoSeatSignal.StandGranted));
        Assert.Equal(CasinoSeatStage.Seated, CasinoSeatMachine.Next(stage, CasinoSeatSignal.StandRefused));
    }

    [Fact]
    public void LeavingTheRoomBeatsEveryStage()
    {
        foreach (var stage in AllStages())
        {
            Assert.Equal(CasinoSeatStage.Watching, CasinoSeatMachine.Next(stage, CasinoSeatSignal.Left));
        }
    }

    [Fact]
    public void ASeatThatIsGoneEndsEveryStageThatIsNotWaitingOnAnAnswer()
    {
        Assert.Equal(CasinoSeatStage.Watching,
            CasinoSeatMachine.Next(CasinoSeatStage.Seated, CasinoSeatSignal.SeatLost));
        Assert.Equal(CasinoSeatStage.Watching,
            CasinoSeatMachine.Next(CasinoSeatStage.Elsewhere, CasinoSeatSignal.SeatLost));
        Assert.Equal(CasinoSeatStage.Watching,
            CasinoSeatMachine.Next(CasinoSeatStage.Standing, CasinoSeatSignal.SeatLost));
    }

    [Fact]
    public void TheSnapshotReadsAsExactlyOneOfThreeThings()
    {
        Assert.Equal(CasinoSeatSignal.SeatLost, CasinoSeatMachine.SignalFor(false, false));
        Assert.Equal(CasinoSeatSignal.SeatLost, CasinoSeatMachine.SignalFor(false, true));
        Assert.Equal(CasinoSeatSignal.SeatBoundHere, CasinoSeatMachine.SignalFor(true, false));
        Assert.Equal(CasinoSeatSignal.SeatBoundElsewhere, CasinoSeatMachine.SignalFor(true, true));
    }

    [Fact]
    public void EveryStageIsReachedByOnlyOneOfTheThreeReadings()
    {
        Assert.True(CasinoSeatMachine.Watching(CasinoSeatStage.Watching));
        Assert.True(CasinoSeatMachine.Watching(CasinoSeatStage.Sitting));
        Assert.False(CasinoSeatMachine.Watching(CasinoSeatStage.Seated));
        Assert.False(CasinoSeatMachine.Watching(CasinoSeatStage.Elsewhere));
        Assert.True(CasinoSeatMachine.Busy(CasinoSeatStage.Sitting));
        Assert.True(CasinoSeatMachine.Busy(CasinoSeatStage.Standing));
        Assert.False(CasinoSeatMachine.Busy(CasinoSeatStage.Seated));
    }

    private static CasinoSeatStage[] AllStages()
    {
        return new[]
        {
            CasinoSeatStage.Watching,
            CasinoSeatStage.Sitting,
            CasinoSeatStage.Seated,
            CasinoSeatStage.Elsewhere,
            CasinoSeatStage.Claiming,
            CasinoSeatStage.Standing,
        };
    }

    private static CasinoSeatSignal[] AllSignalsExcept(params CasinoSeatSignal[] excluded)
    {
        var all = (CasinoSeatSignal[])System.Enum.GetValues(typeof(CasinoSeatSignal));
        var kept = new System.Collections.Generic.List<CasinoSeatSignal>(all.Length);
        for (var index = 0; index < all.Length; index++)
        {
            var skip = false;
            for (var excludedIndex = 0; excludedIndex < excluded.Length; excludedIndex++)
            {
                if (all[index] == excluded[excludedIndex])
                {
                    skip = true;
                    break;
                }
            }

            if (!skip)
            {
                kept.Add(all[index]);
            }
        }

        return kept.ToArray();
    }
}
