using System.Text.Json;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoRoomWireContractTests
{
    [Fact]
    public void RoutesMatchTheBackendRoomEndpoints()
    {
        Assert.Equal("/casino/rooms", CasinoClient.RoomsPath);
        Assert.Equal("/casino/rooms/wheel-floor", CasinoClient.RoomPath(CasinoRoomIds.WheelFloor));
        Assert.Equal("/casino/rooms/a%2Fb", CasinoClient.RoomPath("a/b"));
    }

    [Fact]
    public void SignalTypesMatchTheBackendCasinoVocabulary()
    {
        Assert.Equal("casino.", SignalType.CasinoPrefix);
        Assert.Equal("casino.attach", SignalType.CasinoAttach);
        Assert.Equal("casino.detach", SignalType.CasinoDetach);
        Assert.Equal("casino.resync", SignalType.CasinoResync);
        Assert.Equal("casino.attached", SignalType.CasinoAttached);
        Assert.Equal("casino.declined", SignalType.CasinoDeclined);
        Assert.Equal("casino.snapshot", SignalType.CasinoSnapshot);
        Assert.Equal("casino.event", SignalType.CasinoEvent);
        Assert.Equal("casino.ended", SignalType.CasinoEnded);
        Assert.Equal("casino.ping", SignalType.CasinoPing);
    }

    // The router branches on the prefix alone, so a kind that fell outside it would land in the
    // call switch and log itself as an unhandled signal instead of reaching the room.
    [Fact]
    public void EveryCasinoKindIsCaughtByThePrefixTheRouterBranchesOn()
    {
        var kinds = new[]
        {
            SignalType.CasinoAttach,
            SignalType.CasinoDetach,
            SignalType.CasinoResync,
            SignalType.CasinoAttached,
            SignalType.CasinoDeclined,
            SignalType.CasinoSnapshot,
            SignalType.CasinoEvent,
            SignalType.CasinoEnded,
            SignalType.CasinoPing,
        };

        for (var index = 0; index < kinds.Length; index++)
        {
            Assert.StartsWith(SignalType.CasinoPrefix, kinds[index], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ThePhaseConstantsMirrorTheBackendCycle()
    {
        Assert.Equal(0, CasinoRoomPhases.Open);
        Assert.Equal(1, CasinoRoomPhases.Locked);
        Assert.Equal(2, CasinoRoomPhases.Result);
        Assert.Equal("wheel-floor", CasinoRoomIds.WheelFloor);
        Assert.Equal("bingo-hall", CasinoRoomIds.BingoHall);
    }

    [Fact]
    public void TheBusFansThePayloadOutInsteadOfABarePing()
    {
        var bus = new RealtimeSignalBus();
        var received = new List<CasinoSignal>();
        bus.CasinoReceived += received.Add;

        bus.PublishCasino(new CasinoSignal(SignalType.CasinoEvent, null,
            new CasinoPayload { RoomId = "wheel-floor", Seq = 9 }));

        Assert.Single(received);
        Assert.Equal(SignalType.CasinoEvent, received[0].Type);
        Assert.Equal("wheel-floor", received[0].Payload!.RoomId);
        Assert.Equal(9, received[0].Payload!.Seq);
    }

    [Fact]
    public void TheOutboundSeamStaysShutUntilTheSocketIsUp()
    {
        var bus = new RealtimeSignalBus();
        var sent = new List<CallControl>();
        bus.BindSender(sent.Add);

        Assert.False(bus.TrySend(new CallControl { Type = SignalType.CasinoAttach }));

        bus.SetActive(true);

        Assert.True(bus.TrySend(new CallControl { Type = SignalType.CasinoAttach }));
        Assert.Single(sent);

        bus.BindSender(null);

        Assert.False(bus.TrySend(new CallControl { Type = SignalType.CasinoAttach }));
        Assert.Single(sent);
    }

    [Fact]
    public void TheOutboundPayloadCarriesNothingButTheRoom()
    {
        var payload = new CasinoPayload { RoomId = "wheel-floor" };
        var json = JsonSerializer.Serialize(payload, TelephonyJsonContext.Default.CasinoPayload);
        Assert.Equal("{\"roomId\":\"wheel-floor\",\"epoch\":0,\"seq\":0,\"serverNowUnixMs\":0}", json);
    }

    [Fact]
    public void AnAttachRidesTheOneEnvelopeFieldTheCasinoAdded()
    {
        var control = new CallControl
        {
            Type = SignalType.CasinoAttach,
            Casino = new CasinoPayload { RoomId = "wheel-floor" },
        };
        var json = JsonSerializer.Serialize(control, TelephonyJsonContext.Default.CallControl);
        Assert.Contains("\"type\":\"casino.attach\"", json);
        Assert.Contains("\"casino\":{\"roomId\":\"wheel-floor\",\"epoch\":0,\"seq\":0,\"serverNowUnixMs\":0}", json);
    }

    // The envelope is shared with calls, chat, streams, and every ping, so the casino field has
    // to disappear entirely when it is not the casino talking: a call control that grew a key
    // would make every existing message pay for a feature it never uses.
    [Fact]
    public void ACallControlNeverPaysForTheCasinoField()
    {
        var control = new CallControl { Type = SignalType.Accept, CallId = "call-1" };
        var json = JsonSerializer.Serialize(control, TelephonyJsonContext.Default.CallControl);
        Assert.DoesNotContain("casino", json);
    }

    // The per-game half rides the envelope as a JSON string, so the registry owns the room and the
    // games own their contents: a new game kind ships without widening this shape.
    [Fact]
    public void TheBackendAttachedFrameDeserializesIntoTheNestedPayload()
    {
        const string json = "{\"type\":\"casino.attached\",\"casino\":{"
            + "\"roomId\":\"wheel-floor\",\"epoch\":3,\"seq\":412,\"serverNowUnixMs\":1754784000000,"
            + "\"snapshot\":{\"roomId\":\"wheel-floor\",\"gameKind\":\"casino.wheel\",\"kind\":1,\"state\":1,"
            + "\"phase\":2,\"phaseEndsAtUnixMs\":1754784020000,\"roundIndex\":7,"
            + "\"gameState\":\"{\\\"roundIndex\\\":7,\\\"segment\\\":31,\\\"staked\\\":940}\","
            + "\"occupancy\":11,\"attached\":true,\"epoch\":3,\"seq\":412,"
            + "\"serverNowUnixMs\":1754784000000}}}";

        var control = JsonSerializer.Deserialize(json, TelephonyJsonContext.Default.CallControl);

        Assert.NotNull(control);
        Assert.Equal(SignalType.CasinoAttached, control.Type);
        var payload = control.Casino;
        Assert.NotNull(payload);
        Assert.Equal("wheel-floor", payload.RoomId);
        Assert.Equal(3, payload.Epoch);
        Assert.Equal(412, payload.Seq);
        Assert.Equal(1754784000000, payload.ServerNowUnixMs);
        var snapshot = payload.Snapshot;
        Assert.NotNull(snapshot);
        Assert.Equal(CasinoWire.WheelKind, snapshot.GameKind);
        Assert.Equal(CasinoRoomPhases.Result, snapshot.Phase);
        Assert.Equal(7, snapshot.RoundIndex);
        Assert.Equal(1754784020000, snapshot.PhaseEndsAtUnixMs);
        Assert.Equal(11, snapshot.Occupancy);
        Assert.True(snapshot.Attached);

        var state = CasinoRoomSession.Build("wheel-floor", snapshot.Epoch, snapshot.Seq, snapshot);
        Assert.NotNull(state.Wheel);
        Assert.Equal(31, state.Wheel.Segment);
        Assert.Equal(940, state.Wheel.Staked);
        Assert.Null(state.Bingo);
    }

    // An event states the whole room rather than a delta, so every field is the live value and
    // nothing on the client accumulates: that is what makes a resync after a gap the only healing
    // path this client needs.
    [Fact]
    public void TheBackendEventFrameStatesTheWholeRoom()
    {
        const string json = "{\"type\":\"casino.event\",\"casino\":{"
            + "\"roomId\":\"bingo-hall\",\"epoch\":3,\"seq\":413,\"serverNowUnixMs\":1754784003000,"
            + "\"eventKind\":\"bingo.ball\",\"event\":{\"state\":1,\"phase\":1,"
            + "\"phaseEndsAtUnixMs\":1754784060000,\"roundIndex\":7,"
            + "\"gameState\":\"{\\\"roundIndex\\\":7,\\\"balls\\\":[42]}\",\"occupancy\":6}}}";

        var control = JsonSerializer.Deserialize(json, TelephonyJsonContext.Default.CallControl);

        Assert.NotNull(control);
        var payload = control.Casino;
        Assert.NotNull(payload);
        Assert.Equal("bingo.ball", payload.EventKind);
        var change = payload.Event;
        Assert.NotNull(change);
        Assert.Equal(CasinoRoomPhases.Locked, change.Phase);
        Assert.Equal(7, change.RoundIndex);
        Assert.Equal(6, change.Occupancy);
        Assert.Equal(1754784060000, change.PhaseEndsAtUnixMs);
        Assert.Null(payload.Snapshot);

        var held = CasinoRoomSession.Build("bingo-hall", 3, 412, new CasinoRoomSnapshotDto(
            RoomId: "bingo-hall",
            GameKind: CasinoWire.BingoKind));
        var applied = CasinoRoomSession.Applied(held, 3, 413, change);
        Assert.NotNull(applied.Bingo);
        Assert.Equal(new[] { 42 }, applied.Bingo.Balls);
        Assert.Equal(413, applied.Seq);
    }

    [Fact]
    public void TheBackendDeclineCarriesItsReasonOnTheSharedEnvelopeField()
    {
        const string json = "{\"type\":\"casino.declined\",\"reason\":\"draining\","
            + "\"casino\":{\"roomId\":\"wheel-floor\",\"epoch\":0,\"seq\":0,\"serverNowUnixMs\":0}}";

        var control = JsonSerializer.Deserialize(json, TelephonyJsonContext.Default.CallControl);

        Assert.NotNull(control);
        Assert.NotNull(control.Reason);
        Assert.Equal("draining", control.Reason);
        Assert.Equal("wheel-floor", control.Casino!.RoomId);
        Assert.True(CasinoReasons.TryMessage(control.Reason, out _));
    }

    [Fact]
    public void TheRoomDirectoryDeserializesTheBackendShape()
    {
        const string json = "{\"rooms\":[{\"roomId\":\"wheel-floor\",\"gameKind\":\"casino.wheel\",\"kind\":1,"
            + "\"state\":1,\"phase\":1,\"phaseEndsAtUnixMs\":1754784020000,\"roundIndex\":7,\"occupancy\":11},"
            + "{\"roomId\":\"bingo-hall\",\"gameKind\":\"casino.bingo\",\"kind\":2,\"state\":1,\"phase\":0,"
            + "\"phaseEndsAtUnixMs\":1754784080000,\"roundIndex\":3,\"occupancy\":6}],"
            + "\"serverNowUnixMs\":1754784000000}";

        var directory = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomListDto);

        Assert.NotNull(directory);
        Assert.NotNull(directory.Rooms);
        Assert.Equal(2, directory.Rooms.Length);
        Assert.Equal(CasinoRoomIds.WheelFloor, directory.Rooms[0].RoomId);
        Assert.Equal(CasinoWire.WheelKind, directory.Rooms[0].GameKind);
        Assert.Equal(11, directory.Rooms[0].Occupancy);
        Assert.Equal(CasinoRoomPhases.Locked, directory.Rooms[0].Phase);
        Assert.Equal(CasinoRoomIds.BingoHall, directory.Rooms[1].RoomId);
        Assert.Equal(6, directory.Rooms[1].Occupancy);
        Assert.Equal(1754784000000, directory.ServerNowUnixMs);
    }

    // One shape serves the socket and the poll: GET /casino/rooms/{id} answers with exactly the
    // snapshot casino.attached carries, so a player whose socket is dead plays the same room from
    // the same numbers.
    [Fact]
    public void ThePolledRoomStateIsTheSameSnapshotTheSocketDelivers()
    {
        const string json = "{\"roomId\":\"wheel-floor\",\"gameKind\":\"casino.wheel\",\"kind\":1,\"state\":1,"
            + "\"phase\":2,\"phaseEndsAtUnixMs\":1754784020000,\"roundIndex\":7,"
            + "\"gameState\":\"{\\\"roundIndex\\\":7,\\\"segment\\\":31}\",\"occupancy\":11,\"attached\":false,"
            + "\"epoch\":3,\"seq\":412,\"serverNowUnixMs\":1754784000000}";

        var snapshot = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomSnapshotDto);

        Assert.NotNull(snapshot);
        Assert.Equal(3, snapshot.Epoch);
        Assert.Equal(412, snapshot.Seq);
        Assert.Equal(1754784000000, snapshot.ServerNowUnixMs);
        Assert.Equal("wheel-floor", snapshot.RoomId);
        Assert.False(snapshot.Attached);
    }

    // The 30 second attempt backoff is shared by every read this store makes, so a flapping socket
    // turns one lost frame into one request rather than a storm.
    [Fact]
    public void EveryFailedReadBacksOffTheSharedThirtySeconds()
    {
        Assert.False(CasinoRoomsStore.CoolingDown(0, 100_000));
        Assert.True(CasinoRoomsStore.CoolingDown(100_000, 100_000));
        Assert.True(CasinoRoomsStore.CoolingDown(100_000, 129_999));
        Assert.False(CasinoRoomsStore.CoolingDown(100_000, 130_000));
    }

    // The public room moves on the socket while the private half never does, so a round that
    // turned over or a phase that moved is the whole set of moments a player's own money can have
    // changed hands, and each one costs exactly one read.
    [Fact]
    public void ThePersonalHalfIsRefetchedWheneverTheRoundOrThePhaseMoves()
    {
        Assert.False(CasinoRoomsStore.PersonalIsStale(7, CasinoRoomPhases.Open, 7, CasinoRoomPhases.Open));
        Assert.True(CasinoRoomsStore.PersonalIsStale(7, CasinoRoomPhases.Open, 8, CasinoRoomPhases.Open));
        Assert.True(CasinoRoomsStore.PersonalIsStale(7, CasinoRoomPhases.Open, 7, CasinoRoomPhases.Locked));
        Assert.True(CasinoRoomsStore.PersonalIsStale(-1, -1, 0, CasinoRoomPhases.Open));
    }

    // The staleness question is asked of the read that landed, never of the read that was asked
    // for, so a personal read the network swallowed is still owed a retry inside the same phase.
    // A bingo hall calls for minutes on one phase, and writing the phase off on a single lost read
    // would leave the player watching their own numbers go by with no cards on screen.
    [Fact]
    public void APersonalReadThatNeverLandedLeavesThePhaseStale()
    {
        const long loadedRound = -1;
        const int loadedPhase = -1;

        Assert.True(CasinoRoomsStore.PersonalIsStale(loadedRound, loadedPhase, 7, CasinoRoomPhases.Locked));
        Assert.False(CasinoRoomsStore.PersonalIsStale(7, CasinoRoomPhases.Locked, 7, CasinoRoomPhases.Locked));
    }

    // One purchase id is one order. The server replays the stored purchase behind an id it has
    // already answered, so a four card order sent under the id of a one card order is charged and
    // dealt as one card while the composer asked for four.
    [Fact]
    public void APurchaseIdIsOnlyReusedForTheSameRoundAndTheSameCount()
    {
        Assert.True(CasinoRoomsStore.ReusesPurchase(7, 1, 7, 1));
        Assert.False(CasinoRoomsStore.ReusesPurchase(7, 1, 7, 4));
        Assert.False(CasinoRoomsStore.ReusesPurchase(7, 4, 8, 4));
        Assert.False(CasinoRoomsStore.ReusesPurchase(-1, -1, 7, 1));
    }
}
