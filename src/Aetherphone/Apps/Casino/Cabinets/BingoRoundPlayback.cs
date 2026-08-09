using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum BingoStage
{
    Waiting,
    Selling,
    Calling,
    Wrapped,
}

// Two masks per card and the difference between them is the whole design. The auto mask is what
// the room has called against what the card prints, and it is the only mask anything reads for a
// line, a full house or a one away whisper. The stamped mask is what the player has watched land:
// it catches up to the auto mask on its own after a beat, or immediately when a finger beats it
// there. A tap can therefore never add a mark the room did not call, and never withhold one it
// did, which is what makes hand daubing safe to offer beside a settlement the server owns.
//
// Nothing here counts a ball down on frame time. Balls are whatever the last snapshot listed, and
// the only clock this keeps is how long the newest one has been on screen, which is presentation.
internal sealed class BingoRoundPlayback
{
    public const float StampDelaySeconds = 0.85f;

    public const float PopSeconds = 0.40f;

    public const float BallEntrySeconds = 0.55f;

    private readonly bool[] called = new bool[BingoRules.Balls + 1];
    private readonly int[] autoMasks = new int[BingoRules.MaxCards];
    private readonly int[] stampedMasks = new int[BingoRules.MaxCards];
    private readonly float[] popSeconds = new float[BingoRules.MaxCards * BingoRules.Cells];

    private string roundId = string.Empty;
    private int cardCount;
    private int ballCount;
    private int latestBall;
    private float sinceBall = BallEntrySeconds;
    private bool primed;

    public BingoRoundPlayback()
    {
        ClearRound();
    }

    public string RoundId => roundId;

    public int CardCount => cardCount;

    public int BallCount => ballCount;

    public int LatestBall => latestBall;

    public float SinceBall => sinceBall;

    public void Reset()
    {
        roundId = string.Empty;
        ClearRound();
    }

    public int AutoMaskOf(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < autoMasks.Length ? autoMasks[cardIndex] : BingoRules.FreeMask;
    }

    public int StampedMaskOf(int cardIndex)
    {
        return cardIndex >= 0 && cardIndex < stampedMasks.Length ? stampedMasks[cardIndex] : BingoRules.FreeMask;
    }

    public float PopOf(int cardIndex, int cell)
    {
        var slot = PopSlot(cardIndex, cell);
        return slot < 0 ? 0f : popSeconds[slot];
    }

    public bool IsCalled(int ball)
    {
        return BingoRules.IsBall(ball) && called[ball];
    }

    // A room is identified by the room and its index, never by a round id: the hall turns over on
    // its own clock whether or not this player bought in, and a player who bought nothing has no
    // round id to compare against at all.
    internal static string RoundKeyOf(CasinoRoomSnapshotDto snapshot)
    {
        return string.Concat(snapshot.RoomId, "#",
            snapshot.RoundIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // The room publishes the calls it has made, in the order it made them, so the length is the
    // count. Nothing here derives a call from the clock: a phone that slept through six balls
    // catches up on the next snapshot rather than inventing the ones it missed.
    internal static int[] CalledBalls(CasinoBingoRoomStateDto? board)
    {
        return board?.Balls ?? Array.Empty<int>();
    }

    internal static int[]? CardAt(CasinoBingoCardsDto? mine, int cardIndex)
    {
        var cards = mine?.Cards;
        if (cards is null || cardIndex < 0 || cardIndex >= cards.Length)
        {
            return null;
        }

        return cards[cardIndex];
    }

    public void Update(CasinoRoomSnapshotDto? snapshot, CasinoBingoRoomStateDto? board,
        CasinoBingoCardsDto? mine, float deltaSeconds)
    {
        sinceBall += deltaSeconds;
        AdvancePops(deltaSeconds);
        if (snapshot is null)
        {
            return;
        }

        var nextRoundId = RoundKeyOf(snapshot);
        if (!string.Equals(nextRoundId, roundId, StringComparison.Ordinal))
        {
            OpenRound(nextRoundId);
        }

        var balls = CalledBalls(board);
        var drawn = balls.Length;
        var held = mine?.Cards?.Length ?? 0;
        if (held > BingoRules.MaxCards)
        {
            held = BingoRules.MaxCards;
        }

        if (drawn != ballCount || held != cardCount)
        {
            AbsorbBoard(balls, mine, drawn, held);
        }

        if (!primed)
        {
            StampEverything();
            primed = true;
            return;
        }

        if (sinceBall >= StampDelaySeconds)
        {
            StampEverything();
        }
    }

    // A tap only ever pulls a mark forward. The cell has to be one the room called, which is why
    // this asks the auto mask rather than trusting the caller to have checked.
    public bool Stamp(int cardIndex, int cell)
    {
        if (cardIndex < 0 || cardIndex >= cardCount || !BingoRules.IsCell(cell))
        {
            return false;
        }

        var bit = 1 << cell;
        if ((autoMasks[cardIndex] & bit) == 0 || (stampedMasks[cardIndex] & bit) != 0)
        {
            return false;
        }

        stampedMasks[cardIndex] |= bit;
        PopCell(cardIndex, cell);
        return true;
    }

    private void OpenRound(string nextRoundId)
    {
        roundId = nextRoundId;
        ClearRound();
    }

    // An empty slot reads as a card with nothing but its free centre, which is what the rules
    // answer for a card that does not exist. Zero would claim the centre had not been given away.
    private void ClearRound()
    {
        cardCount = 0;
        ballCount = 0;
        latestBall = 0;
        sinceBall = BallEntrySeconds;
        primed = false;
        Array.Clear(called);
        Array.Clear(popSeconds);
        Array.Fill(autoMasks, BingoRules.FreeMask);
        Array.Fill(stampedMasks, BingoRules.FreeMask);
    }

    private void AbsorbBoard(int[] balls, CasinoBingoCardsDto? mine, int drawn, int held)
    {
        if (drawn > ballCount && drawn > 0)
        {
            latestBall = balls[drawn - 1];
            sinceBall = 0f;
        }

        if (held != cardCount)
        {
            primed = false;
        }

        ballCount = drawn;
        cardCount = held;
        BingoRules.MarkCalled(balls, called);
        for (var cardIndex = 0; cardIndex < autoMasks.Length; cardIndex++)
        {
            var card = cardIndex < held ? CardAt(mine, cardIndex) : null;
            autoMasks[cardIndex] = BingoRules.AutoMask(card, called);
            stampedMasks[cardIndex] &= autoMasks[cardIndex];
        }
    }

    private void StampEverything()
    {
        for (var cardIndex = 0; cardIndex < cardCount; cardIndex++)
        {
            var pending = autoMasks[cardIndex] & ~stampedMasks[cardIndex];
            if (pending == 0)
            {
                continue;
            }

            stampedMasks[cardIndex] |= pending;
            for (var cell = 0; cell < BingoRules.Cells; cell++)
            {
                if ((pending & (1 << cell)) != 0)
                {
                    PopCell(cardIndex, cell);
                }
            }
        }
    }

    private void PopCell(int cardIndex, int cell)
    {
        var slot = PopSlot(cardIndex, cell);
        if (slot >= 0)
        {
            popSeconds[slot] = PopSeconds;
        }
    }

    private void AdvancePops(float deltaSeconds)
    {
        for (var index = 0; index < popSeconds.Length; index++)
        {
            if (popSeconds[index] <= 0f)
            {
                continue;
            }

            popSeconds[index] -= deltaSeconds;
            if (popSeconds[index] < 0f)
            {
                popSeconds[index] = 0f;
            }
        }
    }

    private static int PopSlot(int cardIndex, int cell)
    {
        if (cardIndex < 0 || cardIndex >= BingoRules.MaxCards || !BingoRules.IsCell(cell))
        {
            return -1;
        }

        return cardIndex * BingoRules.Cells + cell;
    }
}
