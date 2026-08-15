using System.Numerics;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackTableLayoutTests
{
    private static readonly Rect Felt = new(new Vector2(0f, 0f), new Vector2(340f, 540f));

    [Fact]
    public void RailSlotsAreAPermutationForEverySeatIMightHold()
    {
        for (var mySeat = 0; mySeat < BlackjackRules.SeatCount; mySeat++)
        {
            var seen = new bool[BlackjackRules.SeatCount - 1];
            for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
            {
                if (seatIndex == mySeat)
                {
                    continue;
                }

                var slot = BlackjackTableLayout.RailSlotOf(seatIndex, mySeat);
                Assert.InRange(slot, 0, BlackjackRules.SeatCount - 2);
                Assert.False(seen[slot]);
                seen[slot] = true;
            }
        }
    }

    [Fact]
    public void ASpectatorSeesTheSeatsInServerOrder()
    {
        Assert.Equal(BlackjackRules.SeatCount, BlackjackTableLayout.RailSeatCount(-1));
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            Assert.Equal(seatIndex, BlackjackTableLayout.RailSlotOf(seatIndex, -1));
        }
    }

    [Fact]
    public void TheSeatsToMyLeftRunLeftToRightAroundTheTable()
    {
        const int mySeat = 2;
        Assert.Equal(0, BlackjackTableLayout.RailSlotOf(3, mySeat));
        Assert.Equal(1, BlackjackTableLayout.RailSlotOf(4, mySeat));
        Assert.Equal(2, BlackjackTableLayout.RailSlotOf(5, mySeat));
        Assert.Equal(3, BlackjackTableLayout.RailSlotOf(0, mySeat));
        Assert.Equal(4, BlackjackTableLayout.RailSlotOf(1, mySeat));
    }

    [Fact]
    public void RailColumnsStayInsideTheFeltAndNeverOverlap()
    {
        for (var railCount = BlackjackRules.SeatCount - 1; railCount <= BlackjackRules.SeatCount; railCount++)
        {
            var columnWidth = BlackjackTableLayout.RailColumnWidth(Felt, railCount, 1f);
            var previousRight = float.NegativeInfinity;
            for (var slot = 0; slot < railCount; slot++)
            {
                var puck = BlackjackTableLayout.RailPuckCenter(Felt, slot, railCount, 1f);
                var columnLeft = puck.X - columnWidth * 0.5f;
                var columnRight = puck.X + columnWidth * 0.5f;
                Assert.True(columnLeft >= Felt.Min.X);
                Assert.True(columnRight <= Felt.Max.X);
                Assert.True(columnLeft >= previousRight - 0.001f);
                previousRight = columnRight;
            }
        }
    }

    [Fact]
    public void TheDealerZoneEndsAboveTheRailCards()
    {
        var fanCenter = BlackjackTableLayout.DealerFanCenter(Felt);
        var dealerBottom = fanCenter.Y + PlayingCards.HeightFor(BlackjackTableLayout.DealerCardWidth) * 0.5f
            + BlackjackTableLayout.DealerTotalDrop + 8f;
        var railCardsTop = BlackjackTableLayout.RailPuckY(Felt) - BlackjackTableLayout.RailCardsLift
            - PlayingCards.HeightFor(BlackjackTableLayout.RailCardWidth) * 0.5f;
        Assert.True(dealerBottom < railCardsTop);
    }

    [Fact]
    public void TheRailTextEndsAboveTheHeroCards()
    {
        var railBottom = BlackjackTableLayout.RailPuckY(Felt) + BlackjackTableLayout.RailStackDrop + 6f;
        var heroCardsTop = BlackjackTableLayout.HeroFanY(Felt)
            - PlayingCards.HeightFor(BlackjackTableLayout.HeroCardWidth(1)) * 0.5f - 8f;
        Assert.True(railBottom < heroCardsTop);
    }

    [Fact]
    public void TheHeroChipsEndAboveTheCapsule()
    {
        var fanBottom = BlackjackTableLayout.HeroFanY(Felt)
            + PlayingCards.HeightFor(BlackjackTableLayout.HeroCardWidth(1)) * 0.5f;
        var chipsLabelBottom = fanBottom + BlackjackTableLayout.HeroChipsDrop + 16f + 9f;
        var capsuleTop = Felt.Max.Y - BlackjackTableLayout.CapsuleDrop - BlackjackTableLayout.CapsuleHeight * 0.5f;
        Assert.True(chipsLabelBottom < capsuleTop);
    }

    [Fact]
    public void TheHeroTotalPillClearsTheChipColumn()
    {
        var pillBottom = BlackjackTableLayout.HeroTotalDrop + 8f;
        var chipColumnTop = BlackjackTableLayout.HeroChipsDrop
            - (BlackjackTableArt.ChipColumnCapacity - 1) * 4.2f - 7f;
        Assert.True(pillBottom < chipColumnTop);
    }

    [Fact]
    public void TheRailOutcomeBadgeStaysBetweenTheFanTopAndTheBetPlate()
    {
        const float badgeHalfHeight = 11f;
        var badgeBottom = -BlackjackTableLayout.RailBadgeLift + badgeHalfHeight;
        var betPlateTop = -BlackjackTableLayout.RailBetLift - 8.5f;
        Assert.True(badgeBottom < betPlateTop);

        var badgeTop = -BlackjackTableLayout.RailBadgeLift - badgeHalfHeight;
        var fanTop = -BlackjackTableLayout.RailCardsLift
            - PlayingCards.HeightFor(BlackjackTableLayout.RailCardWidth) * 0.5f;
        Assert.True(badgeTop > fanTop);
    }

    [Fact]
    public void SqueezedFansNeverExceedTheirColumn()
    {
        const float cardWidth = 18f;
        const float maxWidth = 60f;
        const int cardCount = 8;
        var step = BlackjackTableLayout.FanStep(cardWidth, cardCount, maxWidth);
        Assert.True(cardWidth + step * (cardCount - 1) <= maxWidth + 0.001f);
        Assert.Equal(0f, BlackjackTableLayout.FanStep(cardWidth, 1, maxWidth));
    }

    [Fact]
    public void HeroHandsSpreadSymmetricallyAroundTheCentre()
    {
        var single = BlackjackTableLayout.HeroHandCenter(Felt, 1, 0, 1f);
        Assert.Equal(Felt.Center.X, single.X, 3);

        var leftHand = BlackjackTableLayout.HeroHandCenter(Felt, 2, 0, 1f);
        var rightHand = BlackjackTableLayout.HeroHandCenter(Felt, 2, 1, 1f);
        Assert.True(leftHand.X < rightHand.X);
        Assert.Equal(Felt.Center.X - leftHand.X, rightHand.X - Felt.Center.X, 3);
    }
}
