using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Casino;

internal static class BlackjackSeatStates
{
    public const int Empty = 0;

    public const int Sitting = 1;

    public const int Betting = 2;

    public const int Acting = 3;

    public const int Waiting = 4;

    public const int Away = 5;

    public const int Out = 6;
}

internal static class BlackjackOutcomes
{
    public const int Pending = 0;

    public const int Win = 1;

    public const int Lose = 2;

    public const int Push = 3;

    public const int Blackjack = 4;

    public const int Bust = 5;

    public const int DealerBlackjack = 6;
}

internal static class BlackjackRules
{
    public const int SeatCount = 5;

    public const int Decks = 6;

    public const int MaxHandsPerSeat = 4;

    public const int DealerStandsOn = 17;

    public const int TargetTotal = 21;

    public const long MinBet = 250;

    public const long MaxBet = 10000;

    public const long BetStep = 10;

    public const int ActionHit = 1;

    public const int ActionStand = 2;

    public const int ActionDouble = 4;

    public const int ActionSplit = 8;

    private const long RackHands = 20;

    public static long RackFor(long tableMaxBet, long minBuyIn, long maxBuyIn, long bankroll)
    {
        if (bankroll < minBuyIn)
        {
            return 0;
        }

        var suggested = Math.Clamp(tableMaxBet * RackHands, minBuyIn, maxBuyIn);
        return Math.Min(suggested, bankroll);
    }

    public static bool Allows(int actionsMask, int action)
    {
        return action != 0 && (actionsMask & action) == action;
    }

    public static bool IsSeat(int seatIndex)
    {
        return seatIndex >= 0 && seatIndex < SeatCount;
    }

    public static int CardValue(int card)
    {
        if (!PlayingCards.IsCard(card))
        {
            return 0;
        }

        var rank = PlayingCards.Rank(card);
        if (rank == 0)
        {
            return 1;
        }

        return rank >= 9 ? 10 : rank + 1;
    }

    public static int Total(ReadOnlySpan<int> cards, out bool soft)
    {
        var sum = 0;
        var aces = 0;
        for (var index = 0; index < cards.Length; index++)
        {
            var value = CardValue(cards[index]);
            sum += value;
            if (value == 1)
            {
                aces++;
            }
        }

        soft = false;
        if (aces > 0 && sum + 10 <= TargetTotal)
        {
            soft = true;
            return sum + 10;
        }

        return sum;
    }

    public static bool IsBust(int total)
    {
        return total > TargetTotal;
    }

    public static bool IsNatural(ReadOnlySpan<int> cards, int splitIndex, bool seatSplit)
    {
        if (cards.Length != 2 || splitIndex != 0 || seatSplit)
        {
            return false;
        }

        return Total(cards, out _) == TargetTotal;
    }

    public static long BlackjackPayout(long bet)
    {
        return bet <= 0 ? 0 : bet * 3 / 2;
    }

    public static SeatPhase PhaseOf(int seatState)
    {
        return seatState switch
        {
            BlackjackSeatStates.Sitting => SeatPhase.Sitting,
            BlackjackSeatStates.Betting => SeatPhase.Betting,
            BlackjackSeatStates.Acting => SeatPhase.Acting,
            BlackjackSeatStates.Waiting => SeatPhase.Waiting,
            BlackjackSeatStates.Away => SeatPhase.Away,
            BlackjackSeatStates.Out => SeatPhase.Out,
            _ => SeatPhase.Empty,
        };
    }
}
