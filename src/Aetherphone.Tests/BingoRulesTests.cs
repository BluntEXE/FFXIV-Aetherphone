using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BingoRulesTests
{
    // Column major: five numbers from each column's own fifteen, four in the middle because the
    // centre is free. Cells run 0 to 24 down the columns, so slot n maps to cell n below the free
    // centre and cell n + 1 above it.
    private static int[] SequentialCard()
    {
        var card = new int[BingoRules.CardNumbers];
        for (var slot = 0; slot < BingoRules.CardNumbers; slot++)
        {
            var cell = BingoRules.CardCells[slot];
            var column = cell / BingoRules.Columns;
            var row = cell % BingoRules.Columns;
            card[slot] = BingoRules.ColumnFloorFor(column) + row;
        }

        return card;
    }

    [Fact]
    public void TheCardShapeMirrorsTheEngine()
    {
        Assert.Equal(75, BingoRules.Balls);
        Assert.Equal(24, BingoRules.CardNumbers);
        Assert.Equal(12, BingoRules.FreeCell);
        Assert.Equal(20, BingoRules.CardPrice);
        Assert.Equal(4, BingoRules.MaxCards);
        Assert.Equal(125, BingoRules.PrizeCardCap);
        Assert.Equal(3, BingoRules.BallIntervalSeconds);
        Assert.Equal(BingoRules.CardNumbers, BingoRules.CardCells.Length);
        Assert.Equal(12, BingoRules.LineMasks.Length);
        Assert.Equal(1, BingoRules.ColumnFloorFor(0));
        Assert.Equal(61, BingoRules.ColumnFloorFor(4));
    }

    [Fact]
    public void TheFreeCentreOwnsNoSlotAndEveryOtherCellOwnsExactlyOne()
    {
        Assert.Equal(-1, BingoRules.SlotForCell(BingoRules.FreeCell));
        for (var slot = 0; slot < BingoRules.CardNumbers; slot++)
        {
            Assert.Equal(slot, BingoRules.SlotForCell(BingoRules.CellForSlot(slot)));
        }
    }

    [Fact]
    public void AnEmptyBoardStillHoldsTheFreeCentre()
    {
        var mask = BingoRules.AutoMask(SequentialCard(), Array.Empty<int>());
        Assert.Equal(BingoRules.FreeMask, mask);
        Assert.Equal(0, BingoRules.LinesOn(mask));
        Assert.Equal(BingoRules.Cells - 1, BingoRules.CellsRemaining(mask));
    }

    // The core daub invariant: one call marks the matching cell on every held card that prints the
    // number, wherever it happens to sit on each of them, and never touches a card that does not.
    [Fact]
    public void ACalledBallMarksEveryMatchingCellAcrossEveryHeldCard()
    {
        var first = SequentialCard();
        var second = SequentialCard();
        (second[0], second[4]) = (second[4], second[0]);
        var third = SequentialCard();
        for (var slot = 0; slot < BingoRules.Columns; slot++)
        {
            third[slot] += BingoRules.Columns;
        }

        var playback = new Aetherphone.Apps.Casino.Cabinets.BingoRoundPlayback();
        var entries = new[]
        {
            new CasinoRoomEntryDto("e1", 0, BingoRules.CardPrice, 0, 0, 0, first),
            new CasinoRoomEntryDto("e2", 0, BingoRules.CardPrice, 0, 0, 0, second),
            new CasinoRoomEntryDto("e3", 0, BingoRules.CardPrice, 0, 0, 0, third),
        };
        var snapshot = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.BingoHall,
            GameKind: CasinoWire.BingoKind,
            Phase: CasinoRoomPhases.Locked,
            RoundId: "room7",
            Numbers: new[] { 1 });

        playback.Update(snapshot, entries, 0.016f);

        Assert.True(BingoRules.IsMarked(playback.AutoMaskOf(0), BingoRules.CellForSlot(0)));
        Assert.False(BingoRules.IsMarked(playback.AutoMaskOf(0), BingoRules.CellForSlot(4)));
        Assert.True(BingoRules.IsMarked(playback.AutoMaskOf(1), BingoRules.CellForSlot(4)));
        Assert.False(BingoRules.IsMarked(playback.AutoMaskOf(1), BingoRules.CellForSlot(0)));
        Assert.Equal(BingoRules.FreeMask, playback.AutoMaskOf(2));
    }

    [Fact]
    public void ABallTheCardDoesNotPrintChangesNothing()
    {
        var card = SequentialCard();
        var before = BingoRules.AutoMask(card, new[] { 1 });
        var after = BingoRules.AutoMask(card, new[] { 1, 13, 14, 74 });
        Assert.Equal(before, after);
    }

    [Fact]
    public void AColumnOfCallsCompletesExactlyOneLine()
    {
        var card = SequentialCard();
        var mask = BingoRules.AutoMask(card, new[] { 1, 2, 3, 4, 5 });
        Assert.Equal(1, BingoRules.LinesOn(mask));
        Assert.Equal(0, BingoRules.ClosestLineGap(mask));
    }

    [Fact]
    public void OneCallShortOfALineReadsAsOneAway()
    {
        var card = SequentialCard();
        var mask = BingoRules.AutoMask(card, new[] { 1, 2, 3, 4 });
        Assert.Equal(0, BingoRules.LinesOn(mask));
        Assert.Equal(1, BingoRules.ClosestLineGap(mask));
    }

    // The row through the free centre needs four calls, not five: the middle column contributes
    // nothing to it, which is exactly the shape the engine's line masks encode.
    [Fact]
    public void TheRowThroughTheFreeCentreNeedsOnlyFourCalls()
    {
        var card = SequentialCard();
        var mask = BingoRules.AutoMask(card, new[] { 3, 18, 48, 63 });
        Assert.Equal(1, BingoRules.LinesOn(mask));
    }

    [Fact]
    public void EveryPrintedNumberCalledIsAFullHouse()
    {
        var card = SequentialCard();
        var mask = BingoRules.AutoMask(card, card);
        Assert.Equal(BingoRules.FullMask, mask);
        Assert.Equal(0, BingoRules.CellsRemaining(mask));
        Assert.Equal(BingoRules.StageFullHouse, BingoRules.StageReached(mask));
        Assert.Equal(12, BingoRules.LinesOn(mask));
    }

    [Fact]
    public void StagesClimbInTheOrderTheEngineAwardsThem()
    {
        var card = SequentialCard();
        Assert.Equal(-1, BingoRules.StageReached(BingoRules.AutoMask(card, new[] { 1, 2, 3 })));
        Assert.Equal(BingoRules.StageLine,
            BingoRules.StageReached(BingoRules.AutoMask(card, new[] { 1, 2, 3, 4, 5 })));
        Assert.Equal(BingoRules.StageTwoLines,
            BingoRules.StageReached(BingoRules.AutoMask(card, new[] { 1, 2, 3, 4, 5, 16, 17, 18, 19, 20 })));
    }

    // The published ladder: tenths of a chip per card in play, rounded up, per stage.
    [Fact]
    public void ThePrizeLadderIsTheEnginesRateTimesTheCardsInPlay()
    {
        Assert.Equal(28, BingoRules.PrizeFor(BingoRules.StageLine, 10));
        Assert.Equal(37, BingoRules.PrizeFor(BingoRules.StageTwoLines, 10));
        Assert.Equal(80, BingoRules.PrizeFor(BingoRules.StageFullHouse, 10));
        Assert.Equal(3, BingoRules.PrizeFor(BingoRules.StageLine, 1));
        Assert.Equal(4, BingoRules.PrizeFor(BingoRules.StageTwoLines, 1));
        Assert.Equal(8, BingoRules.PrizeFor(BingoRules.StageFullHouse, 1));
    }

    [Fact]
    public void AnEmptyHallPaysNothingAndNoStageOutsideTheLadderPaysAtAll()
    {
        Assert.Equal(0, BingoRules.PrizeFor(BingoRules.StageLine, 0));
        Assert.Equal(0, BingoRules.PrizeFor(BingoRules.StageFullHouse, -5));
        Assert.Equal(0, BingoRules.PrizeFor(BingoRules.StageCount, 50));
        Assert.Equal(0, BingoRules.PrizeFor(-1, 50));
    }

    // The disclosed ceiling: eight times a hundred and twenty five is exactly the single win
    // maximum, so the full house prize touches the house ceiling at the cap and stops there while
    // the stakes keep climbing.
    [Fact]
    public void PrizesStopGrowingAtTheDisclosedCardCap()
    {
        var atCap = BingoRules.PrizeFor(BingoRules.StageFullHouse, BingoRules.PrizeCardCap);
        Assert.Equal(BingoRules.MaxSingleWin, atCap);
        Assert.Equal(atCap, BingoRules.PrizeFor(BingoRules.StageFullHouse, BingoRules.PrizeCardCap + 1));
        Assert.Equal(atCap, BingoRules.PrizeFor(BingoRules.StageFullHouse, 100_000));

        var lineAtCap = BingoRules.PrizeFor(BingoRules.StageLine, BingoRules.PrizeCardCap);
        Assert.Equal(350, lineAtCap);
        Assert.Equal(lineAtCap, BingoRules.PrizeFor(BingoRules.StageLine, BingoRules.PrizeCardCap * 4));
        Assert.True(lineAtCap < BingoRules.MaxSingleWin);
    }

    [Fact]
    public void NoPrizeAtAnyHallSizeCrossesTheSingleWinCeiling()
    {
        for (var cards = 1; cards <= 400; cards++)
        {
            for (var stage = 0; stage < BingoRules.StageCount; stage++)
            {
                Assert.True(BingoRules.PrizeFor(stage, cards) <= BingoRules.MaxSingleWin);
            }
        }
    }

    [Fact]
    public void TheLadderReportsWhenItIsFrozenSoTheCabinetCanSaySo()
    {
        Assert.False(BingoRules.PrizesFrozen(BingoRules.PrizeCardCap - 1));
        Assert.True(BingoRules.PrizesFrozen(BingoRules.PrizeCardCap));
        Assert.True(BingoRules.PrizesFrozen(BingoRules.PrizeCardCap + 40));

        Span<long> ladder = stackalloc long[BingoRules.StageCount];
        BingoRules.PrizeLadder(BingoRules.PrizeCardCap + 40, ladder);
        Assert.Equal(BingoRules.PrizeFor(BingoRules.StageLine, BingoRules.PrizeCardCap), ladder[0]);
        Assert.Equal(BingoRules.PrizeFor(BingoRules.StageTwoLines, BingoRules.PrizeCardCap), ladder[1]);
        Assert.Equal(BingoRules.MaxSingleWin, ladder[2]);
    }

    [Fact]
    public void CardCountsAndStakesAreBoundedByTheRoomRule()
    {
        Assert.False(BingoRules.IsValidCardCount(0));
        Assert.True(BingoRules.IsValidCardCount(1));
        Assert.True(BingoRules.IsValidCardCount(BingoRules.MaxCards));
        Assert.False(BingoRules.IsValidCardCount(BingoRules.MaxCards + 1));
        Assert.Equal(80, BingoRules.StakeFor(BingoRules.MaxCards));
    }

    [Fact]
    public void TheRoomOnlyAcceptsAStakeItsOwnGameCanExplain()
    {
        Assert.True(CasinoRoomsStore.AcceptsStake(CasinoWire.BingoKind, 2, 40));
        Assert.False(CasinoRoomsStore.AcceptsStake(CasinoWire.BingoKind, 2, 39));
        Assert.False(CasinoRoomsStore.AcceptsStake(CasinoWire.BingoKind, 5, 100));
        Assert.False(CasinoRoomsStore.AcceptsStake(CasinoWire.BingoKind, 0, 0));
        Assert.True(CasinoRoomsStore.AcceptsStake(CasinoWire.WheelKind, 3, 25));
        Assert.False(CasinoRoomsStore.AcceptsStake(CasinoWire.WheelKind, 3, 20_000));
        Assert.False(CasinoRoomsStore.AcceptsStake("casino.mystery", 1, 20));
    }

    // Settlement is the room's call sheet and nothing else. Whatever the player stamped by hand,
    // the mask that decides a line is derived from the balls and the printed numbers alone, so two
    // players holding the same card at the same moment always read the same board.
    [Fact]
    public void SettlementNeverDependsOnWhatWasDaubedByHand()
    {
        var card = SequentialCard();
        var balls = new[] { 1, 2, 3, 4, 5, 16 };
        var truth = BingoRules.AutoMask(card, balls);

        var playback = new Aetherphone.Apps.Casino.Cabinets.BingoRoundPlayback();
        var snapshot = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.BingoHall,
            GameKind: CasinoWire.BingoKind,
            Phase: CasinoRoomPhases.Locked,
            RoundId: "room7",
            EntryCount: 3,
            Numbers: balls);
        var entries = new[] { new CasinoRoomEntryDto("e1", 0, BingoRules.CardPrice, 0, 0, 0, card) };

        playback.Update(snapshot, entries, 0.016f);
        Assert.Equal(truth, playback.AutoMaskOf(0));

        var stampedBefore = playback.StampedMaskOf(0);
        Assert.False(playback.Stamp(0, BingoRules.CellForSlot(6)));
        Assert.Equal(stampedBefore, playback.StampedMaskOf(0));
        Assert.Equal(truth, playback.AutoMaskOf(0));
        Assert.Equal(1, BingoRules.LinesOn(playback.AutoMaskOf(0)));
    }

    // A card seen for the first time is stamped whole, so a player who joins mid room never
    // watches twenty balls replay. From then on a mark either waits its beat or is pulled forward
    // by a tap, and the auto mask is untouched either way.
    [Fact]
    public void AHandStampOnlyPullsForwardAMarkTheRoomAlreadyCalled()
    {
        var card = SequentialCard();
        var playback = new Aetherphone.Apps.Casino.Cabinets.BingoRoundPlayback();
        var entries = new[] { new CasinoRoomEntryDto("e1", 0, BingoRules.CardPrice, 0, 0, 0, card) };
        var opening = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.BingoHall,
            GameKind: CasinoWire.BingoKind,
            Phase: CasinoRoomPhases.Locked,
            RoundId: "room7",
            Numbers: new[] { 1 });
        playback.Update(opening, entries, 0.016f);
        Assert.Equal(playback.AutoMaskOf(0), playback.StampedMaskOf(0));

        var next = opening with { Numbers = new[] { 1, 2 } };
        playback.Update(next, entries, 0.016f);
        var freshCell = BingoRules.CellForSlot(1);
        Assert.True(BingoRules.IsMarked(playback.AutoMaskOf(0), freshCell));
        Assert.False(BingoRules.IsMarked(playback.StampedMaskOf(0), freshCell));

        Assert.True(playback.Stamp(0, freshCell));
        Assert.True(BingoRules.IsMarked(playback.StampedMaskOf(0), freshCell));
        Assert.False(playback.Stamp(0, freshCell));
    }

    [Fact]
    public void AnUnwatchedMarkStampsItselfOnceTheBeatHasPassed()
    {
        var card = SequentialCard();
        var playback = new Aetherphone.Apps.Casino.Cabinets.BingoRoundPlayback();
        var entries = new[] { new CasinoRoomEntryDto("e1", 0, BingoRules.CardPrice, 0, 0, 0, card) };
        var opening = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.BingoHall,
            GameKind: CasinoWire.BingoKind,
            Phase: CasinoRoomPhases.Locked,
            RoundId: "room7",
            Numbers: new[] { 1 });
        playback.Update(opening, entries, 0.016f);
        playback.Update(opening with { Numbers = new[] { 1, 2 } }, entries, 0.016f);
        playback.Update(opening with { Numbers = new[] { 1, 2 } }, entries,
            Aetherphone.Apps.Casino.Cabinets.BingoRoundPlayback.StampDelaySeconds);

        Assert.Equal(playback.AutoMaskOf(0), playback.StampedMaskOf(0));
        Assert.Equal(2, playback.LatestBall);
        Assert.Equal(2, playback.BallCount);
    }

    [Fact]
    public void ANewRoomForgetsTheLastOnesMarks()
    {
        var card = SequentialCard();
        var playback = new Aetherphone.Apps.Casino.Cabinets.BingoRoundPlayback();
        var entries = new[] { new CasinoRoomEntryDto("e1", 0, BingoRules.CardPrice, 0, 0, 0, card) };
        var first = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.BingoHall,
            GameKind: CasinoWire.BingoKind,
            Phase: CasinoRoomPhases.Locked,
            RoundId: "room7",
            Numbers: new[] { 1, 2, 3 });
        playback.Update(first, entries, 0.016f);
        Assert.NotEqual(BingoRules.FreeMask, playback.AutoMaskOf(0));

        playback.Update(first with { RoundId = "room8", Numbers = null }, null, 0.016f);
        Assert.Equal(BingoRules.FreeMask, playback.AutoMaskOf(0));
        Assert.Equal(0, playback.BallCount);
        Assert.Equal(0, playback.CardCount);
    }
}
