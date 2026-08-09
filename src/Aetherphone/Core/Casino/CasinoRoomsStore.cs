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
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence directoryCadence;
    private readonly PollCadence roomCadence;
    private readonly CasinoRoomSession room;
    private readonly StoreWork work = new("CasinoRooms");

    private volatile CasinoRoomCardDto[] rooms = Array.Empty<CasinoRoomCardDto>();
    private volatile bool loadingRooms;
    private volatile bool loadedRooms;
    private int roomsFailed;
    private int fetchingRooms;
    private int fetchingRoomState;
    private long roomsAttemptedAtTick;
    private long roomAttemptedAtTick;
    private string? lastAccountId;

    public CasinoRoomsStore(AethernetSession session, CasinoClient casino, PhoneVisibility visibility,
        RealtimeSignalBus signals)
    {
        this.session = session;
        this.casino = casino;
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

    public bool TakeRoomsFailure()
    {
        return Interlocked.Exchange(ref roomsFailed, 0) != 0;
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
