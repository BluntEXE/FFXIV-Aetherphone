using System.Globalization;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackRulesTests
{
    [Fact]
    public void FaceCardsAreTenAndAnAceStartsAtOne()
    {
        Assert.Equal(1, BlackjackRules.CardValue(PlayingCards.Encode(0, PlayingCards.Spades)));
        Assert.Equal(2, BlackjackRules.CardValue(PlayingCards.Encode(1, PlayingCards.Hearts)));
        Assert.Equal(10, BlackjackRules.CardValue(PlayingCards.Encode(9, PlayingCards.Clubs)));
        Assert.Equal(10, BlackjackRules.CardValue(PlayingCards.Encode(12, PlayingCards.Diamonds)));
        Assert.Equal(0, BlackjackRules.CardValue(PlayingCards.FaceDown));
    }

    [Fact]
    public void OneAceRidesAsElevenWhenTheHandCanCarryIt()
    {
        Span<int> soft = stackalloc int[2]
        {
            PlayingCards.Encode(0, PlayingCards.Spades),
            PlayingCards.Encode(5, PlayingCards.Hearts),
        };
        Assert.Equal(17, BlackjackRules.Total(soft, out var isSoft));
        Assert.True(isSoft);
    }

    [Fact]
    public void TwoAcesCannotBothBeEleven()
    {
        Span<int> pair = stackalloc int[2]
        {
            PlayingCards.Encode(0, PlayingCards.Spades),
            PlayingCards.Encode(0, PlayingCards.Hearts),
        };
        Assert.Equal(12, BlackjackRules.Total(pair, out var isSoft));
        Assert.True(isSoft);
    }

    [Fact]
    public void AnAceDropsBackToOneWhenElevenWouldBust()
    {
        Span<int> hard = stackalloc int[3]
        {
            PlayingCards.Encode(0, PlayingCards.Spades),
            PlayingCards.Encode(9, PlayingCards.Hearts),
            PlayingCards.Encode(8, PlayingCards.Clubs),
        };
        Assert.Equal(20, BlackjackRules.Total(hard, out var isSoft));
        Assert.False(isSoft);
    }

    [Fact]
    public void AClosedCardCountsForNothingUntilItTurnsOver()
    {
        Span<int> dealing = stackalloc int[2]
        {
            PlayingCards.Encode(9, PlayingCards.Hearts),
            PlayingCards.FaceDown,
        };
        Assert.Equal(10, BlackjackRules.Total(dealing, out _));
    }

    [Fact]
    public void ATwoCardTwentyOneIsOnlyNaturalOnAnUnsplitFirstHand()
    {
        Span<int> natural = stackalloc int[2]
        {
            PlayingCards.Encode(0, PlayingCards.Spades),
            PlayingCards.Encode(12, PlayingCards.Hearts),
        };
        Assert.True(BlackjackRules.IsNatural(natural, 0, false));
        Assert.False(BlackjackRules.IsNatural(natural, 1, false));
        Assert.False(BlackjackRules.IsNatural(natural, 0, true));
    }

    [Fact]
    public void BlackjackPaysThreeToTwoAndTheFloorIsPinned()
    {
        Assert.Equal(15, BlackjackRules.BlackjackPayout(10));
        Assert.Equal(16, BlackjackRules.BlackjackPayout(11));
        Assert.Equal(37, BlackjackRules.BlackjackPayout(25));
        Assert.Equal(150, BlackjackRules.BlackjackPayout(100));
        Assert.Equal(499, BlackjackRules.BlackjackPayout(333));
        Assert.Equal(748, BlackjackRules.BlackjackPayout(499));
        Assert.Equal(750, BlackjackRules.BlackjackPayout(500));
        Assert.Equal(0, BlackjackRules.BlackjackPayout(0));
    }

    // The rounding is only ever a surprise if it is sprung after the money is committed, so the
    // confirm label carries the payout beside the stake and takes both numbers to say so.
    [Fact]
    public void TheConfirmLabelHasRoomForThePayoutBesideTheStake()
    {
        var label = L.Casino.BlackjackBetConfirm;
        Assert.Contains("{0}", label.Source, StringComparison.Ordinal);
        Assert.Contains("{1}", label.Source, StringComparison.Ordinal);
        Assert.Equal("Bet 11, blackjack pays 16",
            string.Format(CultureInfo.InvariantCulture, label.Source, 11, BlackjackRules.BlackjackPayout(11)));
    }

    [Fact]
    public void OnlyTheActionsTheServerSetAreAllowed()
    {
        var mask = BlackjackRules.ActionHit | BlackjackRules.ActionStand;
        Assert.True(BlackjackRules.Allows(mask, BlackjackRules.ActionHit));
        Assert.True(BlackjackRules.Allows(mask, BlackjackRules.ActionStand));
        Assert.False(BlackjackRules.Allows(mask, BlackjackRules.ActionDouble));
        Assert.False(BlackjackRules.Allows(mask, BlackjackRules.ActionSplit));
        Assert.False(BlackjackRules.Allows(0, BlackjackRules.ActionHit));
        Assert.False(BlackjackRules.Allows(mask, 0));
    }

    [Fact]
    public void SeatStatesMapOntoTheRingPhasesTheyPaint()
    {
        Assert.Equal(SeatPhase.Empty, BlackjackRules.PhaseOf(BlackjackSeatStates.Empty));
        Assert.Equal(SeatPhase.Betting, BlackjackRules.PhaseOf(BlackjackSeatStates.Betting));
        Assert.Equal(SeatPhase.Acting, BlackjackRules.PhaseOf(BlackjackSeatStates.Acting));
        Assert.Equal(SeatPhase.Away, BlackjackRules.PhaseOf(BlackjackSeatStates.Away));
        Assert.Equal(SeatPhase.Empty, BlackjackRules.PhaseOf(99));
    }

    [Fact]
    public void OnlyTheFiveSeatsExist()
    {
        Assert.False(BlackjackRules.IsSeat(-1));
        Assert.True(BlackjackRules.IsSeat(0));
        Assert.True(BlackjackRules.IsSeat(BlackjackRules.SeatCount - 1));
        Assert.False(BlackjackRules.IsSeat(BlackjackRules.SeatCount));
    }

    [Fact]
    public void CardsLeaveTheShoeOneEveryNinetyMillisecondsAndFlipInFlight()
    {
        Assert.Equal(0f, BlackjackDealChoreography.Travel(0f, 0), 3);
        Assert.Equal(0f, BlackjackDealChoreography.Travel(0.05f, 1), 3);
        Assert.Equal(1f, BlackjackDealChoreography.Travel(5f, 3), 3);

        var midway = BlackjackDealChoreography.Travel(BlackjackDealChoreography.TravelSeconds * 0.5f, 0);
        Assert.Equal(0.5f, midway, 3);
        Assert.False(BlackjackDealChoreography.FaceUp(midway));
        Assert.True(BlackjackDealChoreography.FaceUp(BlackjackDealChoreography.FlipFraction));
    }

    [Fact]
    public void TheFlipSquashesThroughNothingAndOpensAgain()
    {
        Assert.Equal(1f, BlackjackDealChoreography.FlipScaleX(0f), 3);
        Assert.Equal(0f, BlackjackDealChoreography.FlipScaleX(BlackjackDealChoreography.FlipFraction), 3);
        Assert.Equal(1f, BlackjackDealChoreography.FlipScaleX(1f), 3);
        Assert.True(BlackjackDealChoreography.FlipScaleX(0.3f) > 0f);
    }

    [Fact]
    public void TheDealLastsAsLongAsItsStaggerPlusOneTravel()
    {
        Assert.Equal(0f, BlackjackDealChoreography.Duration(0), 3);
        Assert.Equal(BlackjackDealChoreography.TravelSeconds, BlackjackDealChoreography.Duration(1), 3);
        Assert.Equal(BlackjackDealChoreography.StaggerSeconds * 3f + BlackjackDealChoreography.TravelSeconds,
            BlackjackDealChoreography.Duration(4), 3);
    }
}
