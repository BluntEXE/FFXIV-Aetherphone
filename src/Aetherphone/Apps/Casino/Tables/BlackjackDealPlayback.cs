using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

// One clock for the whole deal, reset when the hand id changes and never wound back by anything
// else. The clock is a frame accumulator on purpose: it paces a show, not a deadline, and a show
// that stops while the phone is unfocused is exactly right because nobody is watching it. Deadlines
// still come from the server clock, which is the one thing this class never touches.
internal sealed class BlackjackDealPlayback
{
    private string handId = string.Empty;
    private float elapsed;

    public float Elapsed => elapsed;

    public void Reset()
    {
        handId = string.Empty;
        elapsed = 0f;
    }

    public void Update(CasinoBlackjackRoomStateDto? board, int phase, float deltaSeconds)
    {
        if (board is null)
        {
            Reset();
            return;
        }

        if (!string.Equals(handId, board.HandId, StringComparison.Ordinal))
        {
            handId = board.HandId;
            elapsed = phase == CasinoRoomPhases.Locked
                ? 0f
                : BlackjackDealChoreography.Duration(BlackjackProjection.CardCount(board));
            return;
        }

        elapsed += deltaSeconds;
    }

    // A seat's card is dealt after every card ahead of it in the pass order, so its place in the
    // stagger is its own index plus everything the deal put down first.
    public float TravelOf(int cardOrdinal)
    {
        return BlackjackDealChoreography.Travel(elapsed, cardOrdinal);
    }
}
