using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

// The seat half of the table screen: what this account is to this table right now, and which of the
// four intents the felt is allowed to offer. It holds no money and decides no legality, it only
// keeps the state machine fed from two directions at once, which is the whole difficulty: a
// snapshot arriving every few hundred milliseconds and an intent answering seconds later must never
// be able to leave the screen claiming both that the seat is mine and that I am asking for it.
//
// The join latch lives here rather than on the wire because a client that sat down knows it sat
// down mid hand before any snapshot can say so, and a seat that shows a live bet composer for one
// frame before the server corrects it has already lied about being dealt in.
//
// The settle latch is the same idea pointed the other way: an answered intent outranks every board
// taken before it, so a grant holds the stage until a snapshot confirms it rather than being undone
// two lines later by the poll that has not caught up yet.
internal sealed class BlackjackSeatFlow
{
    private readonly CasinoTablesStore tables;

    private CasinoSeatStage stage = CasinoSeatStage.Watching;
    private CasinoSeatSettle settle = CasinoSeatSettle.None;
    private string reason = string.Empty;
    private long settleArmedAtTick;
    private bool waitArmed;
    private bool standQueued;

    public BlackjackSeatFlow(CasinoTablesStore tables)
    {
        this.tables = tables;
    }

    public CasinoSeatStage Stage => stage;

    public string Reason => reason;

    public bool Waiting => waitArmed;

    public bool StandQueued => standQueued;

    public bool Busy => CasinoSeatMachine.Busy(stage) || tables.IntentInFlight;

    public void Reset()
    {
        stage = CasinoSeatStage.Watching;
        settle = CasinoSeatSettle.None;
        settleArmedAtTick = 0;
        reason = string.Empty;
        waitArmed = false;
        standQueued = false;
    }

    public void ClearReason()
    {
        reason = string.Empty;
    }

    public void Observe(CasinoBlackjackRoomStateDto? board, int phase, long nowTick)
    {
        ConsumeOutcomes(nowTick);
        if (board is null)
        {
            return;
        }

        var hasSeat = BlackjackRules.IsSeat(board.MySeat);
        var signal = CasinoSeatMachine.SignalFor(hasSeat, board.BoundElsewhere);
        if (!Settled(signal, nowTick))
        {
            return;
        }

        stage = CasinoSeatMachine.Next(stage, signal);
        if (!hasSeat)
        {
            waitArmed = false;
            standQueued = false;
            return;
        }

        if (CasinoJoinGate.ClearsWait(phase))
        {
            waitArmed = false;
        }

        waitArmed = CasinoJoinGate.Waiting(true, waitArmed, board.JoinsNextHand);
    }

    // A seat already held is never asked for again. The board can be a poll behind the grant that
    // bound it, and a second sit posted against that older board comes back refused at a felt the
    // player is already sitting at.
    public void Sit(string roomId, int seatIndex, long buyIn, int phase)
    {
        if (Busy || CasinoSeatMachine.Holds(stage) || roomId.Length == 0)
        {
            return;
        }

        reason = string.Empty;
        waitArmed = CasinoJoinGate.ArmsWait(phase);
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.SitRequested);
        tables.Sit(roomId, seatIndex, buyIn);
    }

    public void Stand(string roomId)
    {
        if (Busy || roomId.Length == 0)
        {
            return;
        }

        reason = string.Empty;
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.StandRequested);
        tables.Stand(roomId);
    }

    // Never called by anything but a press. The machine will not move Elsewhere along without this,
    // and this is not called from any observation path, so a takeover cannot happen by itself.
    public void TakeOver(string roomId)
    {
        if (Busy || roomId.Length == 0)
        {
            return;
        }

        reason = string.Empty;
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.TakeOverRequested);
        tables.Claim(roomId);
    }

    public void Left()
    {
        stage = CasinoSeatMachine.Next(stage, CasinoSeatSignal.Left);
        settle = CasinoSeatSettle.None;
        settleArmedAtTick = 0;
        waitArmed = false;
        standQueued = false;
    }

    // The latch opens on the first board that agrees with the grant and on nothing else, and it
    // expires so a table that never confirms hands the screen back rather than freezing it.
    private bool Settled(CasinoSeatSignal signal, long nowTick)
    {
        if (settle == CasinoSeatSettle.None)
        {
            return true;
        }

        if (!CasinoSeatMachine.AcceptsBoard(settle, signal, settleArmedAtTick, nowTick))
        {
            return false;
        }

        settle = CasinoSeatSettle.None;
        settleArmedAtTick = 0;
        return true;
    }

    private void ConsumeOutcomes(long nowTick)
    {
        if (tables.TakeIntentFailure())
        {
            reason = CasinoReasons.Unreachable;
            settle = CasinoSeatSettle.None;
            settleArmedAtTick = 0;
            stage = RefusalOf(stage);
        }

        var outcome = tables.TakeSeatOutcome();
        if (outcome is null)
        {
            return;
        }

        if (!outcome.Granted)
        {
            reason = outcome.Reason;
            settle = CasinoSeatSettle.None;
            settleArmedAtTick = 0;
            stage = RefusalOf(stage);
            return;
        }

        reason = string.Empty;
        standQueued = outcome.AtHandEnd;
        if (outcome.JoinsNextHand)
        {
            waitArmed = true;
        }

        settle = CasinoSeatMachine.SettleFor(stage, outcome.AtHandEnd);
        settleArmedAtTick = nowTick;
        stage = CasinoSeatMachine.Next(stage, GrantOf(stage, outcome.AtHandEnd));
    }

    private static CasinoSeatSignal GrantOf(CasinoSeatStage stage, bool atHandEnd)
    {
        return stage switch
        {
            CasinoSeatStage.Sitting => CasinoSeatSignal.SitGranted,
            CasinoSeatStage.Claiming => CasinoSeatSignal.ClaimGranted,
            CasinoSeatStage.Standing => atHandEnd ? CasinoSeatSignal.StandQueued : CasinoSeatSignal.StandGranted,
            _ => CasinoSeatSignal.SeatBoundHere,
        };
    }

    private static CasinoSeatStage RefusalOf(CasinoSeatStage stage)
    {
        return stage switch
        {
            CasinoSeatStage.Sitting => CasinoSeatMachine.Next(stage, CasinoSeatSignal.SitRefused),
            CasinoSeatStage.Claiming => CasinoSeatMachine.Next(stage, CasinoSeatSignal.ClaimRefused),
            CasinoSeatStage.Standing => CasinoSeatMachine.Next(stage, CasinoSeatSignal.StandRefused),
            _ => stage,
        };
    }
}
