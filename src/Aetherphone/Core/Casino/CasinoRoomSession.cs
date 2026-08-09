using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed record CasinoRoomState(
    string RoomId,
    int Epoch,
    long Seq,
    CasinoRoomSnapshotDto Snapshot,
    CasinoRoomPrivateDto? Private);

internal enum CasinoRoomApply
{
    Ignore,
    Apply,
    Resync,
}

// The socket only ever accelerates a room whose truth is the server snapshot, so every frame is
// gated on (Epoch, Seq): a frame from a restarted server, a duplicate, or one that arrives after
// a lost frame can never be folded into the held state. A gap parks the room on the stale
// snapshot and asks for a fresh one rather than guessing, and the ask is rate limited so a
// flapping socket turns one lost frame into one request, not a storm. Nothing here touches Draw:
// the pump is the store's, and Draw only ever reads the volatile state swapped in from here.
internal sealed class CasinoRoomSession
{
    private const long ResyncCooldownMilliseconds = 2_000;

    // The skew estimate crawls toward the server clock so one frame queued behind a slow send
    // cannot hand a countdown phantom seconds, but a machine that slept (or a clock the user
    // just corrected) has to snap: an estimate minutes wrong renders every deadline expired.
    private const long SkewReanchorMilliseconds = 5_000;
    private const int SkewSmoothingWeight = 4;

    private readonly RealtimeSignalBus signals;
    private readonly object gate = new();

    private volatile CasinoRoomState? state;
    private volatile string roomId = string.Empty;
    private volatile string closedReason = string.Empty;
    private volatile bool attached;
    private volatile bool awaitingSnapshot;
    private long skewMilliseconds;
    private long resyncAskedAtUnixMs;
    private bool skewAnchored;

    public CasinoRoomSession(RealtimeSignalBus signals)
    {
        this.signals = signals;
    }

    public CasinoRoomState? State => state;

    public string RoomId => roomId;

    public bool Attached => attached;

    public bool AwaitingSnapshot => awaitingSnapshot;

    public string ClosedReason => closedReason;

    public long SkewMilliseconds => Volatile.Read(ref skewMilliseconds);

    public long ServerNowUnixMs(long localNowUnixMs)
    {
        return localNowUnixMs + SkewMilliseconds;
    }

    public long RemainingMilliseconds(long deadlineUnixMs, long localNowUnixMs)
    {
        if (deadlineUnixMs <= 0)
        {
            return 0;
        }

        var remaining = deadlineUnixMs - ServerNowUnixMs(localNowUnixMs);
        return remaining > 0 ? remaining : 0;
    }

    public void Enter(string nextRoomId)
    {
        if (nextRoomId.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            if (string.Equals(roomId, nextRoomId, StringComparison.Ordinal) && (attached || awaitingSnapshot))
            {
                return;
            }

            roomId = nextRoomId;
            state = null;
            closedReason = string.Empty;
            attached = false;
            awaitingSnapshot = true;
            resyncAskedAtUnixMs = 0;
        }

        Send(SignalType.CasinoAttach, nextRoomId);
    }

    public void Leave()
    {
        string leaving;
        lock (gate)
        {
            leaving = roomId;
            ClearRoom();
        }

        if (leaving.Length > 0)
        {
            Send(SignalType.CasinoDetach, leaving);
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            ClearRoom();
        }
    }

    public void OnRealtimeConnected(bool connected)
    {
        var current = roomId;
        if (current.Length == 0)
        {
            return;
        }

        if (!connected)
        {
            attached = false;
            awaitingSnapshot = false;
            return;
        }

        awaitingSnapshot = true;
        Send(SignalType.CasinoAttach, current);
    }

    public void Receive(CasinoSignal signal, long localNowUnixMs)
    {
        var payload = signal.Payload;
        if (payload is null)
        {
            return;
        }

        var current = roomId;
        if (current.Length == 0 || !string.Equals(payload.RoomId, current, StringComparison.Ordinal))
        {
            return;
        }

        switch (signal.Type)
        {
            case SignalType.CasinoAttached:
                attached = true;
                closedReason = string.Empty;
                AbsorbSnapshot(payload, localNowUnixMs);
                return;
            case SignalType.CasinoSnapshot:
                AbsorbSnapshot(payload, localNowUnixMs);
                return;
            case SignalType.CasinoEvent:
                AbsorbEvent(payload, localNowUnixMs);
                return;
            case SignalType.CasinoDeclined:
            case SignalType.CasinoEnded:
                Close(signal.Reason ?? string.Empty);
                return;
        }
    }

    // The polling path carries the identical snapshot under the identical version rules, so a
    // player whose socket died keeps a current room from plain HTTP reads alone.
    public void AbsorbHttpState(string requestedRoomId, CasinoRoomStateDto fresh, long localNowUnixMs)
    {
        if (!string.Equals(roomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        if (!fresh.Granted)
        {
            Close(fresh.Reason);
            return;
        }

        var snapshot = fresh.Snapshot;
        if (snapshot is null || !string.Equals(snapshot.RoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        AbsorbServerTime(fresh.ServerNowUnixMs, localNowUnixMs);
        Absorb(requestedRoomId, fresh.Epoch, fresh.Seq, snapshot, fresh.Private);
    }

    internal static bool AcceptsSnapshot(CasinoRoomState? held, int epoch, long seq)
    {
        if (held is null)
        {
            return true;
        }

        if (epoch != held.Epoch)
        {
            return epoch > held.Epoch;
        }

        return seq >= held.Seq;
    }

    internal static CasinoRoomApply Decide(CasinoRoomState? held, int epoch, long seq)
    {
        if (held is null)
        {
            return CasinoRoomApply.Resync;
        }

        if (epoch < held.Epoch)
        {
            return CasinoRoomApply.Ignore;
        }

        if (epoch > held.Epoch)
        {
            return CasinoRoomApply.Resync;
        }

        if (seq <= held.Seq)
        {
            return CasinoRoomApply.Ignore;
        }

        return seq == held.Seq + 1 ? CasinoRoomApply.Apply : CasinoRoomApply.Resync;
    }

    internal static CasinoRoomState Applied(CasinoRoomState held, int epoch, long seq, CasinoRoomEventDto change)
    {
        var snapshot = held.Snapshot;
        var roundId = change.RoundId ?? snapshot.RoundId;
        var roundChanged = !string.Equals(roundId, snapshot.RoundId, StringComparison.Ordinal);
        var next = snapshot with
        {
            Phase = change.Phase ?? snapshot.Phase,
            RoundId = roundId,
            PhaseEndsAtUnixMs = change.PhaseEndsAtUnixMs ?? snapshot.PhaseEndsAtUnixMs,
            PlayerCount = change.PlayerCount ?? snapshot.PlayerCount,
            EntryCount = change.EntryCount ?? snapshot.EntryCount,
            StakeTotal = change.StakeTotal ?? snapshot.StakeTotal,
            Numbers = MergedNumbers(snapshot.Numbers, change.Numbers, roundChanged),
            Spots = ReplacedSpots(snapshot.Spots, change.Spots, roundChanged),
        };

        // Entries belong to the round that took the money. Carrying them past a round boundary
        // would render last round's stake as live money on this round's board.
        return held with
        {
            Epoch = epoch,
            Seq = seq,
            Snapshot = next,
            Private = roundChanged ? null : held.Private,
        };
    }

    // An event carries the draw it just made, never the whole board, so numbers accumulate for
    // as long as the round id holds and start over the moment it moves: a client that attached
    // mid round and one that watched from the first ball converge on the same board.
    internal static int[]? MergedNumbers(int[]? held, int[]? drawn, bool roundChanged)
    {
        if (drawn is null || drawn.Length == 0)
        {
            return roundChanged ? null : held;
        }

        if (roundChanged || held is null || held.Length == 0)
        {
            return drawn;
        }

        var merged = new int[held.Length + drawn.Length];
        held.CopyTo(merged, 0);
        drawn.CopyTo(merged, held.Length);
        return merged;
    }

    // A spot row is a total the server keeps, not a draw the room accumulates, so a present board
    // replaces the held one and an absent board leaves it alone. The round boundary still clears
    // it: last round's pot painted under this round's countdown reads as money already down.
    internal static CasinoRoomSpotDto[]? ReplacedSpots(CasinoRoomSpotDto[]? held, CasinoRoomSpotDto[]? fresh,
        bool roundChanged)
    {
        if (fresh is not null)
        {
            return fresh;
        }

        return roundChanged ? null : held;
    }

    internal static long SmoothedSkew(long held, long sample)
    {
        var drift = sample - held;
        if (drift >= SkewReanchorMilliseconds || drift <= -SkewReanchorMilliseconds)
        {
            return sample;
        }

        return held + drift / SkewSmoothingWeight;
    }

    private void AbsorbSnapshot(CasinoPayload payload, long localNowUnixMs)
    {
        var snapshot = payload.Snapshot;
        if (snapshot is null)
        {
            return;
        }

        AbsorbServerTime(payload.ServerNowUnixMs, localNowUnixMs);
        Absorb(payload.RoomId, payload.Epoch, payload.Seq, snapshot, payload.Private);
    }

    private void Absorb(string absorbedRoomId, int epoch, long seq, CasinoRoomSnapshotDto snapshot,
        CasinoRoomPrivateDto? personal)
    {
        lock (gate)
        {
            if (!AcceptsSnapshot(state, epoch, seq))
            {
                return;
            }

            state = new CasinoRoomState(absorbedRoomId, epoch, seq, snapshot, personal);
            awaitingSnapshot = false;
            resyncAskedAtUnixMs = 0;
        }
    }

    private void AbsorbEvent(CasinoPayload payload, long localNowUnixMs)
    {
        var change = payload.Event;
        if (change is null)
        {
            return;
        }

        AbsorbServerTime(payload.ServerNowUnixMs, localNowUnixMs);
        var asksForResync = false;
        lock (gate)
        {
            var held = state;
            var decision = Decide(held, payload.Epoch, payload.Seq);
            if (decision == CasinoRoomApply.Apply && held is not null)
            {
                state = Applied(held, payload.Epoch, payload.Seq, change);
            }
            else if (decision == CasinoRoomApply.Resync)
            {
                awaitingSnapshot = true;
                asksForResync = AsksForResync(localNowUnixMs);
            }
        }

        if (asksForResync)
        {
            Send(SignalType.CasinoResync, payload.RoomId);
        }
    }

    private bool AsksForResync(long localNowUnixMs)
    {
        if (resyncAskedAtUnixMs != 0 && localNowUnixMs - resyncAskedAtUnixMs < ResyncCooldownMilliseconds)
        {
            return false;
        }

        resyncAskedAtUnixMs = localNowUnixMs;
        return true;
    }

    private void AbsorbServerTime(long serverNowUnixMs, long localNowUnixMs)
    {
        if (serverNowUnixMs <= 0)
        {
            return;
        }

        lock (gate)
        {
            var sample = serverNowUnixMs - localNowUnixMs;
            Volatile.Write(ref skewMilliseconds, skewAnchored ? SmoothedSkew(skewMilliseconds, sample) : sample);
            skewAnchored = true;
        }
    }

    private void Close(string reason)
    {
        lock (gate)
        {
            ClearRoom();
            closedReason = reason;
        }
    }

    private void ClearRoom()
    {
        roomId = string.Empty;
        state = null;
        closedReason = string.Empty;
        attached = false;
        awaitingSnapshot = false;
        resyncAskedAtUnixMs = 0;
    }

    private void Send(string type, string targetRoomId)
    {
        signals.TrySend(new CallControl
        {
            Type = type,
            Casino = new CasinoPayload { RoomId = targetRoomId },
        });
    }
}
