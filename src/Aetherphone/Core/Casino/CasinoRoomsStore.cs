using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

// What the composer needs from a money post and nothing else: the two games answer with different
// shapes, and everything else either lands in the chip stack or arrives on the next personal read.
internal sealed record CasinoStakeOutcome(bool Granted, string Reason);

// Money never moves over the socket, so a room has to stay playable without one: entering asks the
// socket to attach and reads the HTTP snapshot in the same breath, and the room keeps polling on
// its own clock for as long as it is not attached or is waiting on a snapshot it asked for. The
// socket only ever saves polls, which is why the directory keeps the ordinary 60/120 cadence while
// the room in play gets a tight one, and why every failed attempt still backs off the shared 30
// seconds.
//
// The public room and the player's own money are two different reads and they are kept that way.
// The snapshot is what everyone at the rail sees; the personal half (accepted bets, printed cards,
// what the hall settled) lives only behind the per-game read, is refetched whenever the round or
// the phase moves, and is never inferred from a local tally. That separation is what stops a bet
// the server refused from being painted as live money.
internal sealed class CasinoRoomsStore : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ForegroundRoomPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BackgroundRoomPollInterval = TimeSpan.FromSeconds(10);
    private const long RetryAfterAttemptMilliseconds = 30_000;
    private const int NotFoundStatus = 404;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore chips;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence directoryCadence;
    private readonly PollCadence roomCadence;
    private readonly CasinoRoomSession room;
    private readonly StoreWork work = new("CasinoRooms");
    private readonly object stakeGate = new();
    private readonly Action<int> roomStatusSink;

    private volatile CasinoRoomListItemDto[] rooms = Array.Empty<CasinoRoomListItemDto>();
    private volatile CasinoWheelBetsDto? wheelBets;
    private volatile CasinoBingoCardsDto? bingoCards;
    private volatile bool loadingRooms;
    private volatile bool loadedRooms;
    private volatile bool stakeInFlight;
    private CasinoStakeOutcome? stakeResult;
    private string chipRoundId = string.Empty;
    private long chipRoundIndex = -1;
    private string unansweredBetId = string.Empty;
    private int unansweredBetSpot = -1;
    private long unansweredBetAmount;
    private long unansweredBetRoundIndex = -1;
    private string unansweredPurchaseId = string.Empty;
    private long unansweredPurchaseRoundIndex = -1;
    private long personalRoundIndex = -1;
    private int personalPhase = -1;
    private int roomsFailed;
    private int stakeFailed;
    private int fetchingRooms;
    private int fetchingRoomState;
    private int fetchingPersonal;
    private int roomGoneStatus;
    private long roomsAttemptedAtTick;
    private long roomAttemptedAtTick;
    private long personalAttemptedAtTick;
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
        roomStatusSink = OnRoomStatus;
        session.Changed += OnSessionChanged;
        signals.CasinoReceived += OnCasinoSignal;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public CasinoRoomSession Room => room;

    public CasinoRoomListItemDto[] Rooms => rooms;

    public bool LoadingRooms => loadingRooms;

    public bool LoadedRooms => loadedRooms;

    public bool StakeInFlight => stakeInFlight;

    public CasinoWheelBetsDto? WheelBets => wheelBets;

    public CasinoBingoCardsDto? BingoCards => bingoCards;

    public bool TakeRoomsFailure()
    {
        return Interlocked.Exchange(ref roomsFailed, 0) != 0;
    }

    public CasinoStakeOutcome? TakeStakeResult()
    {
        return Interlocked.Exchange(ref stakeResult, null);
    }

    public bool TakeStakeFailure()
    {
        return Interlocked.Exchange(ref stakeFailed, 0) != 0;
    }

    // A personal read only speaks for the room and the round it was taken in. Handing back the
    // wrong round's cards would show last room's full house under this room's ladder, so the match
    // is checked here rather than trusted at every call site.
    public CasinoWheelBetsDto? WheelBetsFor(string roomId, long roundIndex)
    {
        var held = wheelBets;
        if (held is null || held.RoundIndex != roundIndex
            || !string.Equals(held.RoomId, roomId, StringComparison.Ordinal))
        {
            return null;
        }

        return held;
    }

    public CasinoBingoCardsDto? BingoCardsFor(string roomId, long roundIndex)
    {
        var held = bingoCards;
        if (held is null || !held.Granted || held.RoundIndex != roundIndex
            || !string.Equals(held.RoomId, roomId, StringComparison.Ordinal))
        {
            return null;
        }

        return held;
    }

    public int OccupancyOf(string roomId)
    {
        var current = room.State?.Snapshot;
        if (current is not null && string.Equals(current.RoomId, roomId, StringComparison.Ordinal))
        {
            return current.Occupancy;
        }

        var directory = rooms;
        for (var index = 0; index < directory.Length; index++)
        {
            if (string.Equals(directory[index].RoomId, roomId, StringComparison.Ordinal))
            {
                return directory[index].Occupancy;
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

    // Every bet of one spin shares a chip round id and each tap carries its own bet id, which is
    // what makes the retry of a lost response free: the same bet id is the same bet, never a second
    // one. The chip round id is minted once per room round, because the server ties every leg of a
    // spin to it and refuses a bet that arrives under a different one.
    public void PlaceWheelBet(int spot, long amount)
    {
        var roomId = room.RoomId;
        var snapshot = room.State?.Snapshot;
        if (stakeInFlight || !session.IsSignedIn || roomId.Length == 0 || snapshot is null)
        {
            return;
        }

        if (!string.Equals(snapshot.GameKind, CasinoWire.WheelKind, StringComparison.Ordinal)
            || !WheelRules.IsSpot(spot) || !WheelRules.IsStakeInRange(amount))
        {
            return;
        }

        var roundIndex = snapshot.RoundIndex;
        var sittingId = chips.State?.Sitting?.Id ?? string.Empty;
        string betId;
        string clientRoundId;
        lock (stakeGate)
        {
            clientRoundId = ChipRoundFor(roundIndex);
            betId = ReusableBetId(roundIndex, spot, amount);
            if (betId.Length == 0)
            {
                betId = Guid.NewGuid().ToString("N");
            }

            unansweredBetId = betId;
            unansweredBetSpot = spot;
            unansweredBetAmount = amount;
            unansweredBetRoundIndex = roundIndex;
        }

        stakeInFlight = true;
        work.Run("wheel bet", async token =>
        {
            var result = await casino
                .PlaceWheelBetAsync(roomId, roundIndex, clientRoundId, betId, spot, amount, token)
                .ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref stakeFailed, 1);
                return;
            }

            ForgetUnansweredBet(betId);
            Interlocked.Exchange(ref stakeResult, new CasinoStakeOutcome(result.Granted, result.Reason));
            if (!result.Granted)
            {
                chips.RefreshNow();
                RefreshPersonal();
                return;
            }

            if (sittingId.Length > 0)
            {
                chips.AbsorbStack(sittingId, result.Stack);
            }

            RefreshPersonal();
        }, () => stakeInFlight = false);
    }

    // One purchase is one game: the server keys the entry on the room, the round and the identity,
    // so a second post is refused rather than added to. The client sends the whole order once.
    public void BuyBingoCards(int cardCount)
    {
        var roomId = room.RoomId;
        var snapshot = room.State?.Snapshot;
        if (stakeInFlight || !session.IsSignedIn || roomId.Length == 0 || snapshot is null)
        {
            return;
        }

        if (!string.Equals(snapshot.GameKind, CasinoWire.BingoKind, StringComparison.Ordinal)
            || !BingoRules.IsValidCardCount(cardCount))
        {
            return;
        }

        var roundIndex = snapshot.RoundIndex;
        var sittingId = chips.State?.Sitting?.Id ?? string.Empty;
        string purchaseId;
        lock (stakeGate)
        {
            purchaseId = ReusablePurchaseId(roundIndex);
            if (purchaseId.Length == 0)
            {
                purchaseId = Guid.NewGuid().ToString("N");
            }

            unansweredPurchaseId = purchaseId;
            unansweredPurchaseRoundIndex = roundIndex;
        }

        stakeInFlight = true;
        work.Run("bingo cards", async token =>
        {
            var result = await casino
                .BuyBingoCardsAsync(roomId, roundIndex, purchaseId, cardCount, token)
                .ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref stakeFailed, 1);
                return;
            }

            ForgetUnansweredPurchase(purchaseId);
            Interlocked.Exchange(ref stakeResult, new CasinoStakeOutcome(result.Granted, result.Reason));
            if (!result.Granted)
            {
                chips.RefreshNow();
                RefreshPersonal();
                return;
            }

            if (sittingId.Length > 0)
            {
                chips.AbsorbStack(sittingId, result.Stack);
            }

            AbsorbBingoCards(roomId, result);
        }, () => stakeInFlight = false);
    }

    private string ChipRoundFor(long roundIndex)
    {
        if (chipRoundIndex == roundIndex && chipRoundId.Length > 0)
        {
            return chipRoundId;
        }

        chipRoundIndex = roundIndex;
        chipRoundId = Guid.NewGuid().ToString("N");
        return chipRoundId;
    }

    private string ReusableBetId(long roundIndex, int spot, long amount)
    {
        if (unansweredBetId.Length == 0
            || unansweredBetSpot != spot
            || unansweredBetAmount != amount
            || unansweredBetRoundIndex != roundIndex)
        {
            return string.Empty;
        }

        return unansweredBetId;
    }

    private string ReusablePurchaseId(long roundIndex)
    {
        return unansweredPurchaseId.Length > 0 && unansweredPurchaseRoundIndex == roundIndex
            ? unansweredPurchaseId
            : string.Empty;
    }

    private void ForgetUnansweredBet(string betId)
    {
        lock (stakeGate)
        {
            if (!string.Equals(unansweredBetId, betId, StringComparison.Ordinal))
            {
                return;
            }

            unansweredBetId = string.Empty;
            unansweredBetSpot = -1;
            unansweredBetAmount = 0;
            unansweredBetRoundIndex = -1;
        }
    }

    private void ForgetUnansweredPurchase(string purchaseId)
    {
        lock (stakeGate)
        {
            if (!string.Equals(unansweredPurchaseId, purchaseId, StringComparison.Ordinal))
            {
                return;
            }

            unansweredPurchaseId = string.Empty;
            unansweredPurchaseRoundIndex = -1;
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

        ClearPersonal();
        room.Enter(roomId);
        roomCadence.Reset();
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
        RefreshRoomState();
    }

    public void Leave()
    {
        room.Leave();
        ClearPersonal();
        roomCadence.Reset();
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
    }

    internal static bool CoolingDown(long attemptedAtTick, long nowTick)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < RetryAfterAttemptMilliseconds;
    }

    // The public snapshot moves on the socket while the private half never does, so the two are
    // resynchronised here: a round that turned over or a phase that moved is the whole set of
    // moments at which a player's own money can have changed hands, and each one costs one read.
    internal static bool PersonalIsStale(long heldRoundIndex, int heldPhase, long roundIndex, int phase)
    {
        return heldRoundIndex != roundIndex || heldPhase != phase;
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

        SyncPersonal();
        var nowUtc = DateTime.UtcNow;
        if (signals.RealtimeActive && room.Attached && !room.AwaitingSnapshot)
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

    private void SyncPersonal()
    {
        var snapshot = room.State?.Snapshot;
        if (snapshot is null)
        {
            return;
        }

        if (!PersonalIsStale(personalRoundIndex, personalPhase, snapshot.RoundIndex, snapshot.Phase))
        {
            return;
        }

        personalRoundIndex = snapshot.RoundIndex;
        personalPhase = snapshot.Phase;
        Interlocked.Exchange(ref personalAttemptedAtTick, 0);
        RefreshPersonal();
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
        rooms = Array.Empty<CasinoRoomListItemDto>();
        loadedRooms = false;
        ClearPersonal();
        Interlocked.Exchange(ref stakeResult, null);
        Interlocked.Exchange(ref stakeFailed, 0);
        Interlocked.Exchange(ref roomsFailed, 0);
        Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
        directoryCadence.Reset();
        roomCadence.Reset();
    }

    private void ClearPersonal()
    {
        wheelBets = null;
        bingoCards = null;
        personalRoundIndex = -1;
        personalPhase = -1;
        chipRoundId = string.Empty;
        chipRoundIndex = -1;
        Interlocked.Exchange(ref personalAttemptedAtTick, 0);
        lock (stakeGate)
        {
            unansweredBetId = string.Empty;
            unansweredBetSpot = -1;
            unansweredBetAmount = 0;
            unansweredBetRoundIndex = -1;
            unansweredPurchaseId = string.Empty;
            unansweredPurchaseRoundIndex = -1;
        }
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
            rooms = directory.Rooms ?? Array.Empty<CasinoRoomListItemDto>();
            loadedRooms = true;
        }, () =>
        {
            loadingRooms = false;
            Interlocked.Exchange(ref fetchingRooms, 0);
        });
    }

    private void OnRoomStatus(int statusCode)
    {
        if (statusCode == NotFoundStatus)
        {
            Interlocked.Exchange(ref roomGoneStatus, 1);
        }
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
        Interlocked.Exchange(ref roomGoneStatus, 0);
        work.Run("room state", async token =>
        {
            var fresh = await casino.RoomStateAsync(target, roomStatusSink, token).ConfigureAwait(false);
            if (fresh is null)
            {
                // A 404 is the same fact casino.ended carries and the only refusal this read can
                // name on its own. Every other empty answer leaves the held room alone, because one
                // dropped read must never look like a closed table.
                if (Interlocked.Exchange(ref roomGoneStatus, 0) != 0)
                {
                    Interlocked.Exchange(ref roomAttemptedAtTick, 0);
                    room.CloseFromHttp(target, CasinoReasons.Ended);
                }

                return;
            }

            Interlocked.Exchange(ref roomAttemptedAtTick, 0);
            room.AbsorbHttpState(target, fresh, NowUnixMilliseconds());
        }, () => Interlocked.Exchange(ref fetchingRoomState, 0));
    }

    private void RefreshPersonal()
    {
        var target = room.RoomId;
        var snapshot = room.State?.Snapshot;
        if (target.Length == 0 || snapshot is null || !session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref personalAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingPersonal, 1) != 0)
        {
            return;
        }

        var gameKind = snapshot.GameKind;
        Interlocked.Exchange(ref personalAttemptedAtTick, Environment.TickCount64);
        work.Run("room personal", async token =>
        {
            if (string.Equals(gameKind, CasinoWire.WheelKind, StringComparison.Ordinal))
            {
                var bets = await casino.MyWheelBetsAsync(target, token).ConfigureAwait(false);
                if (bets is null)
                {
                    return;
                }

                Interlocked.Exchange(ref personalAttemptedAtTick, 0);
                AbsorbWheelBets(target, bets);
                return;
            }

            if (!string.Equals(gameKind, CasinoWire.BingoKind, StringComparison.Ordinal))
            {
                Interlocked.Exchange(ref personalAttemptedAtTick, 0);
                return;
            }

            var cards = await casino.MyBingoCardsAsync(target, token).ConfigureAwait(false);
            if (cards is null)
            {
                return;
            }

            Interlocked.Exchange(ref personalAttemptedAtTick, 0);
            AbsorbBingoCards(target, cards);
        }, () => Interlocked.Exchange(ref fetchingPersonal, 0));
    }

    // A personal read that came back for a room the player has already left is dropped rather than
    // parked, for the same reason a snapshot is: the next room would render another room's money.
    private void AbsorbWheelBets(string requestedRoomId, CasinoWheelBetsDto fresh)
    {
        if (!string.Equals(room.RoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        wheelBets = fresh;
    }

    private void AbsorbBingoCards(string requestedRoomId, CasinoBingoCardsDto fresh)
    {
        if (!string.Equals(room.RoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        bingoCards = fresh;
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
