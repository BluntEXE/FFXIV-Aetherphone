using System.Text.Json;
using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoBingoWireContractTests
{
    [Fact]
    public void TheHallHangsOffTheSameRoomRoutesAsTheWheel()
    {
        Assert.Equal("bingo-hall", CasinoRoomIds.BingoHall);
        Assert.Equal("casino.bingo", CasinoWire.BingoKind);
        Assert.Equal("/casino/rooms/bingo-hall", CasinoClient.RoomPath(CasinoRoomIds.BingoHall));
        Assert.Equal("/casino/rooms/bingo-hall/stake", CasinoClient.RoomStakePath(CasinoRoomIds.BingoHall));
    }

    // Buying cards is the same money path a wheel bet takes: the round id refuses a purchase that
    // arrived after the selling window closed, and the client entry id makes the retry of a lost
    // response free instead of printing a second set of cards.
    [Fact]
    public void BuyingCardsSerializesAsAnOrdinaryRoomStake()
    {
        var request = new CasinoRoomStakeRequest("bingo-hall", "room7", "entry1", 3, 60);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoRoomStakeRequest);
        Assert.Equal(
            "{\"roomId\":\"bingo-hall\",\"roundId\":\"room7\",\"clientEntryId\":\"entry1\",\"target\":3,"
            + "\"amount\":60}",
            json);
        Assert.Equal(60, BingoRules.StakeFor(3));
    }

    [Fact]
    public void APrivatePayloadCarriesOneEntryPerCardWithItsPrintedNumbers()
    {
        const string json = "{\"roundId\":\"room7\",\"staked\":40,\"entries\":["
            + "{\"entryId\":\"e1\",\"kind\":0,\"stake\":20,\"target\":0,\"payout\":0,\"state\":0,"
            + "\"numbers\":[1,2,3,4,5,16,17,18,19,20,31,32,33,34,46,47,48,49,50,61,62,63,64,65]},"
            + "{\"entryId\":\"e2\",\"kind\":0,\"stake\":20,\"target\":1,\"payout\":80,\"state\":1,"
            + "\"numbers\":[6,7,8,9,10,21,22,23,24,25,36,37,38,39,51,52,53,54,55,66,67,68,69,70]}]}";
        var personal = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomPrivateDto);

        Assert.NotNull(personal);
        Assert.NotNull(personal.Entries);
        Assert.Equal(2, personal.Entries.Length);
        Assert.NotNull(personal.Entries[0].Numbers);
        Assert.Equal(BingoRules.CardNumbers, personal.Entries[0].Numbers!.Length);
        Assert.Equal(1, personal.Entries[0].Numbers![0]);
        Assert.Equal(80, personal.Entries[1].Payout);
        Assert.Equal(100, BingoCabinet.PayoutOf(personal.Entries) + 20);
    }

    // A wheel entry carries no printed numbers, which is the whole reason the field is nullable:
    // one carrier serves both communal games without a shape per game.
    [Fact]
    public void AWheelEntryStillParsesWithoutPrintedNumbers()
    {
        const string json = "{\"entryId\":\"e1\",\"kind\":0,\"stake\":25,\"target\":3,\"payout\":0,\"state\":0}";
        var entry = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomEntryDto);

        Assert.NotNull(entry);
        Assert.Null(entry.Numbers);
        Assert.Equal(3, entry.Target);
    }

    [Fact]
    public void ASnapshotCarriesTheCalledBallsTheCardsInPlayAndTheAwardedStages()
    {
        const string json = "{\"roomId\":\"bingo-hall\",\"gameKind\":\"casino.bingo\",\"phase\":1,"
            + "\"roundId\":\"room7\",\"phaseEndsAtUnixMs\":1770000000000,\"playerCount\":18,\"entryCount\":44,"
            + "\"stakeTotal\":880,\"numbers\":[12,45,3,71],"
            + "\"stages\":[{\"stage\":0,\"ball\":31,\"prize\":124,\"winners\":2}]}";
        var snapshot = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomSnapshotDto);

        Assert.NotNull(snapshot);
        Assert.Equal(CasinoRoomPhases.Locked, snapshot.Phase);
        Assert.Equal(18, snapshot.PlayerCount);
        Assert.Equal(44, snapshot.EntryCount);
        Assert.NotNull(snapshot.Numbers);
        Assert.Equal(4, snapshot.Numbers.Length);
        Assert.NotNull(snapshot.Stages);
        var line = BingoCabinet.StageAwarded(snapshot, BingoRules.StageLine);
        Assert.NotNull(line);
        Assert.Equal(31, line.Ball);
        Assert.Equal(124, line.Prize);
        Assert.Equal(2, line.Winners);
        Assert.Null(BingoCabinet.StageAwarded(snapshot, BingoRules.StageFullHouse));
    }

    // Balls accumulate across events the way the wheel's single wedge does, but stages are running
    // totals the server keeps, so a present list replaces the held one outright.
    [Fact]
    public void BallsAccumulateWhileAwardedStagesReplace()
    {
        var snapshot = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.BingoHall,
            GameKind: CasinoWire.BingoKind,
            Phase: CasinoRoomPhases.Locked,
            RoundId: "room7",
            EntryCount: 20,
            Numbers: new[] { 12, 45 },
            Stages: new[] { new CasinoRoomStageDto(BingoRules.StageLine, 31, 56, 1) });
        var held = new CasinoRoomState(CasinoRoomIds.BingoHall, 1, 5, snapshot, null);

        var applied = CasinoRoomSession.Applied(held, 1, 6, new CasinoRoomEventDto(
            Numbers: new[] { 3 },
            Stages: new[]
            {
                new CasinoRoomStageDto(BingoRules.StageLine, 31, 56, 1),
                new CasinoRoomStageDto(BingoRules.StageTwoLines, 44, 74, 1),
            }));

        Assert.NotNull(applied.Snapshot.Numbers);
        Assert.Equal(3, applied.Snapshot.Numbers.Length);
        Assert.Equal(3, applied.Snapshot.Numbers[2]);
        Assert.NotNull(applied.Snapshot.Stages);
        Assert.Equal(2, applied.Snapshot.Stages.Length);

        var quiet = CasinoRoomSession.Applied(applied, 1, 7, new CasinoRoomEventDto(PlayerCount: 19));
        Assert.NotNull(quiet.Snapshot.Stages);
        Assert.Equal(2, quiet.Snapshot.Stages.Length);
        Assert.Equal(19, quiet.Snapshot.PlayerCount);
    }

    [Fact]
    public void ANewRoomClearsLastRoomsBallsStagesAndCards()
    {
        var snapshot = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.BingoHall,
            GameKind: CasinoWire.BingoKind,
            Phase: CasinoRoomPhases.Result,
            RoundId: "room7",
            Numbers: new[] { 12, 45, 3 },
            Stages: new[] { new CasinoRoomStageDto(BingoRules.StageFullHouse, 52, 160, 1) });
        var personal = new CasinoRoomPrivateDto("room7", 20,
            new[] { new CasinoRoomEntryDto("e1", 0, 20, 0, 160, 1, new int[BingoRules.CardNumbers]) });
        var held = new CasinoRoomState(CasinoRoomIds.BingoHall, 1, 5, snapshot, personal);

        var applied = CasinoRoomSession.Applied(held, 1, 6,
            new CasinoRoomEventDto(Phase: CasinoRoomPhases.Open, RoundId: "room8"));

        Assert.Equal("room8", applied.Snapshot.RoundId);
        Assert.Null(applied.Snapshot.Numbers);
        Assert.Null(applied.Snapshot.Stages);
        Assert.Null(applied.Private);
    }

    // Cards belong to the round that printed them, so a private payload left over from the last
    // room is never read against this one.
    [Fact]
    public void CardsAreOnlyReadWhenTheyBelongToTheRoundInPlay()
    {
        var personal = new CasinoRoomPrivateDto("room7", 20,
            new[] { new CasinoRoomEntryDto("e1", 0, 20, 0, 0, 0, new int[BingoRules.CardNumbers]) });
        var snapshot = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.BingoHall,
            GameKind: CasinoWire.BingoKind,
            RoundId: "room7");
        var held = new CasinoRoomState(CasinoRoomIds.BingoHall, 1, 5, snapshot, personal);

        Assert.NotNull(BingoCabinet.EntriesFor(held, "room7"));
        Assert.Null(BingoCabinet.EntriesFor(held, "room8"));
        Assert.Null(BingoCabinet.EntriesFor(held, string.Empty));
        Assert.Null(BingoCabinet.EntriesFor(null, "room7"));
    }

    [Fact]
    public void MoreCardsThanTheRoomAllowsAreNeverDrawn()
    {
        var entries = new CasinoRoomEntryDto[BingoRules.MaxCards + 2];
        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = new CasinoRoomEntryDto("e" + index, 0, 20, index);
        }

        Assert.Equal(BingoRules.MaxCards, BingoCabinet.HeldCards(entries));
        Assert.Equal(0, BingoCabinet.HeldCards(null));
    }

    [Fact]
    public void ARefusedPurchaseStillNamesAReasonTheHallCanSpeak()
    {
        const string json = "{\"granted\":false,\"reason\":\"cards_full\",\"roundId\":\"room7\",\"stack\":180}";
        var stake = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomStakeDto);

        Assert.NotNull(stake);
        Assert.False(stake.Granted);
        Assert.Equal(CasinoReasons.CardsFull, stake.Reason);
        Assert.True(CasinoReasons.TryMessage(stake.Reason, out var message));
        Assert.NotEqual(Aetherphone.Core.Localization.L.Casino.ReasonGeneric.Key, message.Key);
    }
}
