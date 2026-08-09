using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

// Money never moves over the socket, so a room has to stay playable without one: entering asks
// the socket to attach and reads the HTTP snapshot in the same breath, and the room keeps
// polling on its own clock for as long as the attach is unconfirmed. The socket only ever saves
// the polls, which is why the directory keeps the ordinary 60/120 cadence while the room in
// play gets a tight one, and why every failed attempt still backs off for the shared 30 seconds.
internal sealed class CasinoRoomsStore : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ForegroundRoomPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BackgroundRoomPollInterval = TimeSpan.FromSeconds(10);
    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore chips;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence directoryCadence;
    private readonly PollCadence roomCadence;
    private readonly CasinoRoomSession room;
    private readonly StoreWork work = new("CasinoRooms");
    private readonly object stakeGate = new();

    private volatile CasinoRoomCardDto[] rooms = Array.Empty<CasinoRoomCardDto>();
    private volatile bool loadingRooms;
    private volatile bool loadedRooms;
    private volatile bool stakeInFlight;
    private CasinoRoomStakeDto? stakeResult;
    private string unansweredStakeId = string.Empty;
    private string unansweredStakeRoundId = string.Empty;
    private int unansweredStakeTarget = -1;
    private long unansweredStakeAmount;
    private int roomsFailed;
    private int stakeFailed;
    private int fetchingRooms;
    private int fetchingRoomState;
    private long roomsAttemptedAtTick;
    private long roomAttemptedAtTick;
    private string? lastAccountId;

    public CasinoRoomsStore(AethernetSession session, CasinoClient casino, CasinoStore chips,
        PhoneVisibility visibility, RealtimeSignalBus signals)
    {
        this.session = session;
        this.casino = casino;
        this.chips = chips;
        this.signals = signals;
        directoryCadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        roomCadence = new PollCadence(visibility, ForegroundRoomPollInterval, BackgroundRoomPollInterval);
        room = new CasinoRoomSession(signals);
        session.Changed += OnSessionChanged;
        signals.CasinoReceived += OnCasinoSignal;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public CasinoRoomSession Room => room;

    public CasinoRoomCardDto[] Rooms => rooms;

    public bool LoadingRooms => loadingRooms;

    public bool LoadedRooms => loadedRooms;

    public bool StakeInFlight => stakeInFlight;

    public bool TakeRoomsFailure()
    {
        return Interlocked.Exchange(ref roomsFailed, 0) != 0;
    }

    public CasinoRoomStakeDto? TakeStakeResult()
    {
        return Interlocked.Exchange(ref stakeResult, null);
    }

    public bool TakeStakeFailure()
    {
        return Interlocked.Exchange(ref stakeFailed, 0) != 0;
    }

    public int OccupancyOf(string roomId)
    {
        var current = room.State?.Snapshot;
        if (current is not null && string.Equals(current.RoomId, roomId, StringComparison.Ordinal))
        {
            return current.PlayerCount;
        }

        var directory = rooms;
        for (var index = 0; index < directory.Length; index++)
        {
            if (string.Equals(directory[index].RoomId, roomId, StringComparison.Ordinal))
            {
                return directory[index].PlayerCount;
            }
        }

        return 0;
    }

    // A tile wants the same two facts about a room nobody is standing in: which phase it is in and
    // when that phase ends. The live room answers for the room in play because its snapshot is
    // seconds fresher than a directory page that refreshes once a minute.
    public bool TryRoomClock(string roomId, out int phase, out long phaseEndsAtUnixMs)
    {
        var current = room.State?.Snapshot;
        if (current is not null && string.Equals(current.RoomId, roomId, StringComparison.Ordinal))
        {
            phase = current.Phase;
            phaseEndsAtUnixMs = current.PhaseEndsAtUnixMs;
            return true;
        }

        var directory = rooms;
        for (var index = 0; index < directory.Length; index++)
        {
            if (!string.Equals(directory[index].RoomId, roomId, StringComparison.Ordinal))
            {
                continue;
            }

            phase = directory[index].Phase;
            phaseEndsAtUnixMs = directory[index].PhaseEndsAtUnixMs;
            return true;
        }

        phase = CasinoRoomPhases.Open;
        phaseEndsAtUnixMs = 0;
        return false;
    }

    // A communal stake has no replay-before-staking rule to lean on: the round it belongs to
    // closes on the server clock whether or not this client ever heard back, so a lost response
    // is healed by the next snapshot rather than by re-POSTing blind. The one thing that must
    // not happen is charging twice for one tap, so an attempt whose response never arrived keeps
    // its entry id and the next identical tap reuses it. Any answer at all retires the id.
    public void PlaceStake(int target, long amount)
    {
        var roomId = room.RoomId;
        var snapshot = room.State?.Snapshot;
        if (stakeInFlight || !session.IsSignedIn || roomId.Length == 0 || snapshot is null)
        {
            return;
        }

        if (!AcceptsStake(snapshot.GameKind, target, amount))
        {
            return;
        }

        var roundId = snapshot.RoundId;
        if (roundId.Length == 0)
        {
            return;
        }

        var sittingId = chips.State?.Sitting?.Id ?? string.Empty;
        string entryId;
        lock (stakeGate)
        {
            entryId = ReusableStakeId(roundId, target, amount);
            if (entryId.Length == 0)
            {
                entryId = Guid.NewGuid().ToString("N");
            }

            unansweredStakeId = entryId;
            unansweredStakeRoundId = roundId;
            unansweredStakeTarget = target;
            unansweredStakeAmount = amount;
        }

        stakeInFlight = true;
        work.Run("room stake", async token =>
        {
            var result = await casino
                .StakeRoomAsync(roomId, roundId, entryId, target, amount, token)
                .ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref stakeFailed, 1);
                return;
            }

            ForgetUnansweredStake(entryId);
            Interlocked.Exchange(ref stakeResult, result);
            if (!result.Granted)
            {
                chips.RefreshNow();
                return;
            }

            if (sittingId.Length > 0)
            {
                chips.AbsorbStack(sittingId, result.Stack);
            }

            // The board a bet just moved has to come back authoritatively rather than be patched
            // in: a locally folded total carries no sequence, so the next room event would either
            // overwrite it or stack on top of it. One read per accepted bet buys that certainty,
            // and it is the only way a player with a quiet socket watches their own chips land.
            Interlocked.Exchange(ref roomAttemptedAtTick, 0);
            roomCadence.Reset();
            RefreshRoomState();
        }, () => stakeInFlight = false);
    }

    private string ReusableStakeId(string roundId, int target, long amount)
    {
        if (unansweredStakeId.Length == 0
            || unansweredStakeTarget != target
            || unansweredStakeAmount != amount
            || !string.Equals(unansweredStakeRoundId, roundId, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return unansweredStakeId;
    }

    private void ForgetUnansweredStake(string entryId)
    {
        lock (stakeGate)
        {
            if (!string.Equals(unansweredStakeId, entryId, StringComparison.Ordinal))
            {
                return;
            }

            unansweredStakeId = string.Empty;
            unansweredStakeRoundId = string.Empty;
            unansweredStakeTarget = -1;
            unansweredStakeAmount = 0;
        }
    }

    public void EnsureFresh()
    {
        if (!session.IsSignedIn || !directoryCadence.Due(DateTime.UtcNow))
        {
            return;
        }

        RefreshRooms();
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
        directoryCadence.Reset();
        RefreshRooms();
    }

    public void Enter(string roomId)
    {
        if (roomId.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        room.Enter(roomId);
        roomCadence.Reset();
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
        RefreshRoomState();
    }

    public void Leave()
    {
        room.Leave();
        roomCadence.Reset();
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
    }

    internal static bool CoolingDown(long attemptedAtTick, long nowTick)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < RetryAfterAttemptMilliseconds;
    }

    // What a target and an amount mean is the game's business, not the room's: a wheel target is a
    // bet spot with its own range, a bingo target is how many cards to print at a fixed price. A
    // room whose game this client does not know refuses to send money at all rather than posting a
    // number the server would have to interpret.
    internal static bool AcceptsStake(string gameKind, int target, long amount)
    {
        if (string.Equals(gameKind, CasinoWire.WheelKind, StringComparison.Ordinal))
        {
            return WheelRules.IsSpot(target) && WheelRules.IsStakeInRange(amount);
        }

        if (string.Equals(gameKind, CasinoWire.BingoKind, StringComparison.Ordinal))
        {
            return BingoRules.IsValidCardCount(target) && amount == BingoRules.StakeFor(target);
        }

        return false;
    }

    private static long NowUnixMilliseconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn || room.RoomId.Length == 0)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        if (signals.RealtimeActive && room.Attached)
        {
            roomCadence.Mark(nowUtc);
            return;
        }

        if (!roomCadence.Due(nowUtc))
        {
            return;
        }

        RefreshRoomState();
    }

    private void OnCasinoSignal(CasinoSignal signal)
    {
        if (string.Equals(signal.Type, SignalType.CasinoPing, StringComparison.Ordinal))
        {
            directoryCadence.RequestImmediate();
            return;
        }

        room.Receive(signal, NowUnixMilliseconds());
    }

    private void OnRealtimeConnected(bool connected)
    {
        room.OnRealtimeConnected(connected);
        if (connected)
        {
            directoryCadence.RequestImmediate();
            return;
        }

        roomCadence.Reset();
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        room.Reset();
        rooms = Array.Empty<CasinoRoomCardDto>();
        loadedRooms = false;
        ForgetUnansweredStake(unansweredStakeId);
        Interlocked.Exchange(ref stakeResult, null);
        Interlocked.Exchange(ref stakeFailed, 0);
        Interlocked.Exchange(ref roomsFailed, 0);
        Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
        directoryCadence.Reset();
        roomCadence.Reset();
    }

    private void RefreshRooms()
    {
        if (!session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref roomsAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingRooms, 1) != 0)
        {
            return;
        }

        loadingRooms = true;
        Interlocked.Exchange(ref roomsAttemptedAtTick, Environment.TickCount64);
        work.Run("rooms directory", async token =>
        {
            var directory = await casino.RoomsAsync(token).ConfigureAwait(false);
            if (directory is null)
            {
                Interlocked.Exchange(ref roomsFailed, 1);
                return;
            }

            Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
            room.AbsorbClock(directory.ServerNowUnixMs, NowUnixMilliseconds());
            rooms = directory.Rooms ?? Array.Empty<CasinoRoomCardDto>();
            loadedRooms = true;
        }, () =>
        {
            loadingRooms = false;
            Interlocked.Exchange(ref fetchingRooms, 0);
        });
    }

    private void RefreshRoomState()
    {
        var target = room.RoomId;
        if (target.Length == 0 || !session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref roomAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingRoomState, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref roomAttemptedAtTick, Environment.TickCount64);
        work.Run("room state", async token =>
        {
            var fresh = await casino.RoomStateAsync(target, token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            Interlocked.Exchange(ref roomAttemptedAtTick, 0);
            room.AbsorbHttpState(target, fresh, NowUnixMilliseconds());
        }, () => Interlocked.Exchange(ref fetchingRoomState, 0));
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        session.Changed -= OnSessionChanged;
        signals.CasinoReceived -= OnCasinoSignal;
        signals.ConnectedChanged -= OnRealtimeConnected;
        work.Dispose();
    }
}
