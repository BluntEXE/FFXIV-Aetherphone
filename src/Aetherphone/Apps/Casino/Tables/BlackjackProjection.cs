using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

// The rendered hand is a projection of what the server last said, never a hand this client is
// playing along with. Everything arrives already versioned by (epoch, seq) and the gate here is the
// second half of that contract: a frame from before the one on screen, or from a table that
// restarted behind us, is dropped rather than merged, because a board assembled out of order would
// show cards in an order the shoe never dealt them.
//
// The public board and the seat scoped faces are held apart and only ever meet in CardAt: a private
// frame belonging to another round can then be ignored on the spot instead of quietly overpainting
// this round's placeholders.
internal sealed class BlackjackProjection
{
    private int epoch = -1;
    private long seq = -1;
    private int personalEpoch = -1;
    private long personalSeq = -1;

    public CasinoBlackjackRoomStateDto? Board { get; private set; }

    public CasinoBlackjackPrivateDto? Personal { get; private set; }

    public int Epoch => epoch;

    public long Seq => seq;

    public void Reset()
    {
        epoch = -1;
        seq = -1;
        personalEpoch = -1;
        personalSeq = -1;
        Board = null;
        Personal = null;
    }

    public bool Apply(CasinoRoomState? state)
    {
        if (state?.Blackjack is null)
        {
            return false;
        }

        if (state.Epoch < epoch || (state.Epoch == epoch && state.Seq <= seq))
        {
            return false;
        }

        epoch = state.Epoch;
        seq = state.Seq;
        Board = state.Blackjack;
        return true;
    }

    public bool ApplyPersonal(CasinoRoomPrivate? held)
    {
        if (held?.Blackjack is null)
        {
            return false;
        }

        if (held.Epoch < personalEpoch || (held.Epoch == personalEpoch && held.Seq <= personalSeq))
        {
            return false;
        }

        personalEpoch = held.Epoch;
        personalSeq = held.Seq;
        Personal = held.Blackjack;
        return true;
    }

    public CasinoBlackjackSeatDto? SeatAt(int seatIndex)
    {
        var seats = Board?.Seats;
        if (seats is null)
        {
            return null;
        }

        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].SeatIndex == seatIndex)
            {
                return seats[index];
            }
        }

        return null;
    }

    public static int CardCount(CasinoBlackjackRoomStateDto? board)
    {
        if (board is null)
        {
            return 0;
        }

        var count = board.DealerCards?.Length ?? 0;
        var seats = board.Seats;
        if (seats is null)
        {
            return count;
        }

        for (var seatIndex = 0; seatIndex < seats.Length; seatIndex++)
        {
            var hands = seats[seatIndex].Hands;
            if (hands is null)
            {
                continue;
            }

            for (var handIndex = 0; handIndex < hands.Length; handIndex++)
            {
                count += hands[handIndex].Cards?.Length ?? 0;
            }
        }

        return count;
    }

    // A face-down card stays face down for every seat but mine, and mine only turns over when the
    // private frame that carries it belongs to the round on the felt right now and to the same run
    // of the table. A round index repeats across a restart, so the epoch is what tells last table's
    // hole cards apart from this one's: without it the faces of a round seven that ended when the
    // room restarted would be painted onto the placeholders of the round seven that replaced it.
    public int CardAt(int seatIndex, int splitIndex, int cardIndex, int publicCard)
    {
        if (PlayingCards.IsCard(publicCard))
        {
            return publicCard;
        }

        var personal = Personal;
        var board = Board;
        if (personal is null || board is null || personalEpoch != epoch || personal.SeatIndex != seatIndex
            || personal.RoundIndex != board.RoundIndex)
        {
            return PlayingCards.FaceDown;
        }

        var hands = personal.Hands;
        if (hands is null || splitIndex < 0 || splitIndex >= hands.Length)
        {
            return PlayingCards.FaceDown;
        }

        var cards = hands[splitIndex];
        if (cards is null || cardIndex < 0 || cardIndex >= cards.Length)
        {
            return PlayingCards.FaceDown;
        }

        return PlayingCards.IsCard(cards[cardIndex]) ? cards[cardIndex] : PlayingCards.FaceDown;
    }
}
