using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

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

    public float TravelOf(int cardOrdinal)
    {
        return BlackjackDealChoreography.Travel(elapsed, cardOrdinal);
    }
}
