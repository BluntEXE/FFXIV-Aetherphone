using System.Text.Json;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoRoomWireContractTests
{
    [Fact]
    public void RoutesMatchTheBackendRoomEndpoints()
    {
        Assert.Equal("/casino/rooms", CasinoClient.RoomsPath);
        Assert.Equal("/casino/rooms/wheel-1", CasinoClient.RoomPath("wheel-1"));
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
    public void TheBusFansThePayloadOutInsteadOfABarePing()
    {
        var bus = new RealtimeSignalBus();
        var received = new List<CasinoSignal>();
        bus.CasinoReceived += received.Add;

        bus.PublishCasino(new CasinoSignal(SignalType.CasinoEvent, null,
            new CasinoPayload { RoomId = "wheel-1", Seq = 9 }));

        Assert.Single(received);
        Assert.Equal(SignalType.CasinoEvent, received[0].Type);
        Assert.Equal("wheel-1", received[0].Payload!.RoomId);
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
        var payload = new CasinoPayload { RoomId = "wheel-1" };
        var json = JsonSerializer.Serialize(payload, TelephonyJsonContext.Default.CasinoPayload);
        Assert.Equal("{\"roomId\":\"wheel-1\",\"epoch\":0,\"seq\":0,\"serverNowUnixMs\":0}", json);
    }

    [Fact]
    public void AnAttachRidesTheOneEnvelopeFieldTheCasinoAdded()
    {
        var control = new CallControl
        {
            Type = SignalType.CasinoAttach,
            Casino = new CasinoPayload { RoomId = "wheel-1" },
        };
        var json = JsonSerializer.Serialize(control, TelephonyJsonContext.Default.CallControl);
        Assert.Contains("\"type\":\"casino.attach\"", json);
        Assert.Contains("\"casino\":{\"roomId\":\"wheel-1\",\"epoch\":0,\"seq\":0,\"serverNowUnixMs\":0}", json);
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

    [Fact]
    public void TheBackendAttachedFrameDeserializesIntoTheNestedPayload()
    {
        const string json = "{\"type\":\"casino.attached\",\"casino\":{"
            + "\"roomId\":\"wheel-1\",\"epoch\":3,\"seq\":412,\"serverNowUnixMs\":1754784000000,"
            + "\"snapshot\":{\"roomId\":\"wheel-1\",\"gameKind\":\"casino.wheel\",\"phase\":2,"
            + "\"roundId\":\"round-7\",\"phaseEndsAtUnixMs\":1754784020000,\"draining\":false,"
            + "\"playerCount\":11,\"entryCount\":19,\"stakeTotal\":940,\"minStake\":5,\"maxStake\":200,"
            + "\"numbers\":[13]},"
            + "\"private\":{\"roundId\":\"round-7\",\"staked\":40,\"entries\":["
            + "{\"entryId\":\"entry-1\",\"kind\":1,\"stake\":40,\"target\":7,\"payout\":0,\"state\":0}]}}}";

        var control = JsonSerializer.Deserialize(json, TelephonyJsonContext.Default.CallControl);

        Assert.NotNull(control);
        Assert.Equal(SignalType.CasinoAttached, control.Type);
        var payload = control.Casino;
        Assert.NotNull(payload);
        Assert.Equal("wheel-1", payload.RoomId);
        Assert.Equal(3, payload.Epoch);
        Assert.Equal(412, payload.Seq);
        Assert.Equal(1754784000000, payload.ServerNowUnixMs);
        var snapshot = payload.Snapshot;
        Assert.NotNull(snapshot);
        Assert.Equal("casino.wheel", snapshot.GameKind);
        Assert.Equal(2, snapshot.Phase);
        Assert.Equal("round-7", snapshot.RoundId);
        Assert.Equal(1754784020000, snapshot.PhaseEndsAtUnixMs);
        Assert.False(snapshot.Draining);
        Assert.Equal(11, snapshot.PlayerCount);
        Assert.Equal(19, snapshot.EntryCount);
        Assert.Equal(940, snapshot.StakeTotal);
        Assert.Equal(5, snapshot.MinStake);
        Assert.Equal(200, snapshot.MaxStake);
        Assert.Equal(new[] { 13 }, snapshot.Numbers);
        var personal = payload.Private;
        Assert.NotNull(personal);
        Assert.Equal("round-7", personal.RoundId);
        Assert.Equal(40, personal.Staked);
        Assert.NotNull(personal.Entries);
        Assert.Single(personal.Entries);
        Assert.Equal("entry-1", personal.Entries[0].EntryId);
        Assert.Equal(7, personal.Entries[0].Target);
    }

    [Fact]
    public void TheBackendEventFrameLeavesTheFieldsItOmitsUnspoken()
    {
        const string json = "{\"type\":\"casino.event\",\"casino\":{"
            + "\"roomId\":\"bingo-1\",\"epoch\":3,\"seq\":413,\"serverNowUnixMs\":1754784003000,"
            + "\"eventKind\":\"bingo.ball\",\"event\":{\"numbers\":[42]}}}";

        var control = JsonSerializer.Deserialize(json, TelephonyJsonContext.Default.CallControl);

        Assert.NotNull(control);
        var payload = control.Casino;
        Assert.NotNull(payload);
        Assert.Equal("bingo.ball", payload.EventKind);
        var change = payload.Event;
        Assert.NotNull(change);
        Assert.Equal(new[] { 42 }, change.Numbers);
        Assert.Null(change.Phase);
        Assert.Null(change.RoundId);
        Assert.Null(change.PhaseEndsAtUnixMs);
        Assert.Null(change.PlayerCount);
        Assert.Null(change.EntryCount);
        Assert.Null(change.StakeTotal);
        Assert.Null(payload.Snapshot);
    }

    [Fact]
    public void TheBackendDeclineCarriesItsReasonOnTheSharedEnvelopeField()
    {
        const string json = "{\"type\":\"casino.declined\",\"reason\":\"draining\","
            + "\"casino\":{\"roomId\":\"wheel-1\",\"epoch\":0,\"seq\":0,\"serverNowUnixMs\":0}}";

        var control = JsonSerializer.Deserialize(json, TelephonyJsonContext.Default.CallControl);

        Assert.NotNull(control);
        Assert.Equal("draining", control.Reason);
        Assert.Equal("wheel-1", control.Casino!.RoomId);
    }

    [Fact]
    public void TheRoomDirectoryDeserializesTheBackendShape()
    {
        const string json = "{\"rooms\":[{\"roomId\":\"wheel-1\",\"gameKind\":\"casino.wheel\",\"phase\":1,"
            + "\"phaseEndsAtUnixMs\":1754784020000,\"playerCount\":11,\"minStake\":5,\"maxStake\":200,"
            + "\"draining\":false}],\"serverNowUnixMs\":1754784000000}";

        var directory = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomDirectoryDto);

        Assert.NotNull(directory);
        Assert.NotNull(directory.Rooms);
        Assert.Single(directory.Rooms);
        Assert.Equal("wheel-1", directory.Rooms[0].RoomId);
        Assert.Equal("casino.wheel", directory.Rooms[0].GameKind);
        Assert.Equal(11, directory.Rooms[0].PlayerCount);
        Assert.Equal(200, directory.Rooms[0].MaxStake);
        Assert.Equal(1754784000000, directory.ServerNowUnixMs);
    }

    // The polled room read is the same versioned snapshot the socket delivers, so the two paths
    // can never disagree about what the client is allowed to apply.
    [Fact]
    public void ThePolledRoomStateCarriesTheSameVersionTheSocketDoes()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"epoch\":3,\"seq\":412,"
            + "\"serverNowUnixMs\":1754784000000,"
            + "\"snapshot\":{\"roomId\":\"wheel-1\",\"gameKind\":\"casino.wheel\",\"phase\":2,"
            + "\"roundId\":\"round-7\",\"phaseEndsAtUnixMs\":1754784020000,\"draining\":false,"
            + "\"playerCount\":11,\"entryCount\":19,\"stakeTotal\":940,\"minStake\":5,\"maxStake\":200,"
            + "\"numbers\":[13]},\"private\":null}";

        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomStateDto);

        Assert.NotNull(state);
        Assert.True(state.Granted);
        Assert.Equal(3, state.Epoch);
        Assert.Equal(412, state.Seq);
        Assert.Equal(1754784000000, state.ServerNowUnixMs);
        Assert.Equal("wheel-1", state.Snapshot!.RoomId);
        Assert.Null(state.Private);
    }

    [Fact]
    public void APolledDenialArrivesAsATwoHundredWithAReason()
    {
        const string json = "{\"granted\":false,\"reason\":\"room_closed\",\"epoch\":0,\"seq\":0,"
            + "\"serverNowUnixMs\":1754784000000,\"snapshot\":null,\"private\":null}";

        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomStateDto);

        Assert.NotNull(state);
        Assert.False(state.Granted);
        Assert.Equal("room_closed", state.Reason);
        Assert.Null(state.Snapshot);
    }
}
