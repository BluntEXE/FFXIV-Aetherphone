using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed record CasinoRoomState(
    string RoomId,
    int Epoch,
    long Seq,
    CasinoRoomSnapshotDto Snapshot,
    CasinoWheelRoomStateDto? Wheel,
    CasinoBingoRoomStateDto? Bingo,
    CasinoBlackjackRoomStateDto? Blackjack);

// The seat scoped half of a room, held beside the public one and versioned by the same pair. A
// private frame is the only thing on this socket that is not true for everybody at the rail, so it
// is stored apart rather than merged in: a snapshot that arrives without one must never be able to
// erase a face the server already dealt to this seat, and a stale one must never paint it back.
internal sealed record CasinoRoomPrivate(
    string RoomId,
    int Epoch,
    long Seq,
    CasinoPrivateDto Payload,
    CasinoBlackjackPrivateDto? Blackjack);

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
// flapping socket turns one lost frame into one request, not a storm.
//
// The per-game half of a room rides the snapshot as a JSON string, and it is parsed here rather
// than in Draw: the pump is the store's, Draw only ever reads the volatile state swapped in from
// here, and an immediate mode frame can afford neither the parse nor the garbage.
//
// Every absorb re-reads the room id inside the gate. The receive thread checks the id before it
// blocks on the lock, so without that second read a frame for the room the player just left can
// be parked under the room they just entered, and the two rooms keep independent sequences behind
// one shared epoch: the wrong snapshot would then out-rank every genuine one until the new room's
// sequence overtook it.
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
    private volatile CasinoRoomPrivate? privateState;
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

    public CasinoRoomPrivate? Private => privateState;

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

    // The room directory carries the same server clock a room does, and the floor tiles count the
    // next room down from it, so a phone that has never stepped into a room still paints honest
    // deadlines on the tiles.
    public void AbsorbClock(long serverNowUnixMs, long localNowUnixMs)
    {
        AbsorbServerTime(serverNowUnixMs, localNowUnixMs);
    }

    // Attach and detach are ordered by the same gate the room state moves under. The socket answers
    // whichever it hears last, so a detach that overtook the attach it was meant to cancel would
    // leave the server fanning a room the phone dropped every frame of into nothing.
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
            privateState = null;
            closedReason = string.Empty;
            attached = false;
            awaitingSnapshot = true;
            resyncAskedAtUnixMs = 0;
            Send(SignalType.CasinoAttach, nextRoomId);
        }
    }

    public void Leave()
    {
        lock (gate)
        {
            var leaving = roomId;
            ClearRoom();
            if (leaving.Length > 0)
            {
                Send(SignalType.CasinoDetach, leaving);
            }
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
        lock (gate)
        {
            var current = roomId;
            if (current.Length == 0)
            {
                return;
            }

            if (!connected)
            {
                attached = false;
                awaitingSnapshot = true;
                return;
            }

            awaitingSnapshot = true;
            Send(SignalType.CasinoAttach, current);
        }
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
            case SignalType.CasinoPrivate:
                AbsorbPrivate(payload, localNowUnixMs);
                return;
            case SignalType.CasinoDeclined:
            case SignalType.CasinoEnded:
                Close(payload.RoomId, signal.Reason ?? string.Empty);
                return;
        }
    }

    // The polling path carries the identical snapshot under the identical version rules, so a
    // player whose socket died keeps a current room from plain HTTP reads alone.
    public void AbsorbHttpState(string requestedRoomId, CasinoRoomSnapshotDto fresh, long localNowUnixMs)
    {
        if (!string.Equals(roomId, requestedRoomId, StringComparison.Ordinal)
            || !string.Equals(fresh.RoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        AbsorbServerTime(fresh.ServerNowUnixMs, localNowUnixMs);
        Absorb(requestedRoomId, fresh.Epoch, fresh.Seq, fresh);
    }

    // A room the server no longer serves is the one refusal the poll can name on its own: a 404 is
    // the same fact casino.ended carries, and a player with a dead socket has no other way to hear
    // it. Every other failure leaves the held room alone, because one dropped read must never look
    // like a closed table.
    public void CloseFromHttp(string requestedRoomId, string reason)
    {
        Close(requestedRoomId, reason);
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

    internal static bool AcceptsPrivate(CasinoRoomPrivate? held, int epoch, long seq)
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

    // An event states the whole room, so applying one is a replacement rather than a merge: the
    // phase, the deadline, the round and the game blob all arrive together and nothing on this
    // side accumulates. That is what makes a resync after a gap cheap enough to be the only
    // healing path the client needs.
    internal static CasinoRoomState Applied(CasinoRoomState held, int epoch, long seq, CasinoRoomEventDto change)
    {
        var next = held.Snapshot with
        {
            State = change.State,
            Phase = change.Phase,
            PhaseEndsAtUnixMs = change.PhaseEndsAtUnixMs,
            RoundIndex = change.RoundIndex,
            GameState = change.GameState,
            Occupancy = change.Occupancy,
            Epoch = epoch,
            Seq = seq,
        };

        return Build(held.RoomId, epoch, seq, next);
    }

    // The blob belongs to the game, so a room whose kind this client does not know parks on the
    // envelope and paints nothing rather than guessing at a shape. A blob that fails to parse is
    // the same case: the room keeps its clock and its occupancy and the cabinet says it is waiting.
    internal static CasinoRoomState Build(string roomId, int epoch, long seq, CasinoRoomSnapshotDto snapshot)
    {
        if (string.Equals(snapshot.GameKind, CasinoWire.WheelKind, StringComparison.Ordinal))
        {
            return new CasinoRoomState(roomId, epoch, seq, snapshot,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoWheelRoomStateDto), null, null);
        }

        if (string.Equals(snapshot.GameKind, CasinoWire.BingoKind, StringComparison.Ordinal))
        {
            return new CasinoRoomState(roomId, epoch, seq, snapshot, null,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoBingoRoomStateDto), null);
        }

        if (string.Equals(snapshot.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal))
        {
            return new CasinoRoomState(roomId, epoch, seq, snapshot, null, null,
                Parse(snapshot.GameState, AethernetJsonContext.Default.CasinoBlackjackRoomStateDto));
        }

        return new CasinoRoomState(roomId, epoch, seq, snapshot, null, null, null);
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

    private static TState? Parse<TState>(string gameState,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TState> typeInfo)
        where TState : class
    {
        if (gameState.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(gameState, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void AbsorbSnapshot(CasinoPayload payload, long localNowUnixMs)
    {
        var snapshot = payload.Snapshot;
        if (snapshot is null)
        {
            return;
        }

        AbsorbServerTime(payload.ServerNowUnixMs, localNowUnixMs);
        Absorb(payload.RoomId, payload.Epoch, payload.Seq, snapshot);
    }

    private void Absorb(string absorbedRoomId, int epoch, long seq, CasinoRoomSnapshotDto snapshot)
    {
        lock (gate)
        {
            if (!string.Equals(roomId, absorbedRoomId, StringComparison.Ordinal)
                || !AcceptsSnapshot(state, epoch, seq))
            {
                return;
            }

            state = Build(absorbedRoomId, epoch, seq, snapshot);
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
            if (!string.Equals(roomId, payload.RoomId, StringComparison.Ordinal))
            {
                return;
            }

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

    private void AbsorbPrivate(CasinoPayload payload, long localNowUnixMs)
    {
        var personal = payload.Private;
        if (personal is null)
        {
            return;
        }

        AbsorbServerTime(payload.ServerNowUnixMs, localNowUnixMs);
        lock (gate)
        {
            if (!string.Equals(roomId, payload.RoomId, StringComparison.Ordinal)
                || !AcceptsPrivate(privateState, payload.Epoch, payload.Seq))
            {
                return;
            }

            privateState = new CasinoRoomPrivate(payload.RoomId, payload.Epoch, payload.Seq, personal,
                BuildPrivate(personal));
        }
    }

    internal static CasinoBlackjackPrivateDto? BuildPrivate(CasinoPrivateDto personal)
    {
        if (!string.Equals(personal.EventKind, CasinoWire.BlackjackHandEvent, StringComparison.Ordinal))
        {
            return null;
        }

        return Parse(personal.Payload, AethernetJsonContext.Default.CasinoBlackjackPrivateDto);
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

    private void Close(string closingRoomId, string reason)
    {
        lock (gate)
        {
            if (!string.Equals(roomId, closingRoomId, StringComparison.Ordinal))
            {
                return;
            }

            ClearRoom();
            closedReason = reason;
        }
    }

    private void ClearRoom()
    {
        roomId = string.Empty;
        state = null;
        privateState = null;
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
