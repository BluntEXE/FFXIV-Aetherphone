using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoWheelWireContractTests
{
    [Fact]
    public void TheStakeRouteHangsOffTheRoomTheBetBelongsTo()
    {
        Assert.Equal("/casino/rooms/wheel-floor/stake", CasinoClient.RoomStakePath(CasinoRoomIds.WheelFloor));
        Assert.Equal("/casino/rooms/a%2Fb/stake", CasinoClient.RoomStakePath("a/b"));
        Assert.Equal("wheel-floor", CasinoRoomIds.WheelFloor);
        Assert.Equal("casino.wheel", CasinoWire.WheelKind);
    }

    [Fact]
    public void ThePhaseConstantsMirrorTheBackendCycle()
    {
        Assert.Equal(0, CasinoRoomPhases.Open);
        Assert.Equal(1, CasinoRoomPhases.Locked);
        Assert.Equal(2, CasinoRoomPhases.Result);
    }

    [Fact]
    public void TheStakeRequestSerializesTheBackendShape()
    {
        var request = new CasinoRoomStakeRequest("wheel-floor", "r7", "entry1", 3, 25);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoRoomStakeRequest);
        Assert.Equal(
            "{\"roomId\":\"wheel-floor\",\"roundId\":\"r7\",\"clientEntryId\":\"entry1\",\"target\":3,\"amount\":25}",
            json);
    }

    [Fact]
    public void AGrantedStakeCarriesTheStackAndTheFreshBoard()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r7\",\"target\":3,\"amount\":25,"
            + "\"staked\":45,\"stack\":205,"
            + "\"spots\":[{\"spot\":0,\"bettors\":4,\"amount\":180},{\"spot\":3,\"bettors\":2,\"amount\":75}]}";
        var stake = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomStakeDto);

        Assert.NotNull(stake);
        Assert.True(stake.Granted);
        Assert.Equal("r7", stake.RoundId);
        Assert.Equal(3, stake.Target);
        Assert.Equal(25, stake.Amount);
        Assert.Equal(45, stake.Staked);
        Assert.Equal(205, stake.Stack);
        Assert.NotNull(stake.Spots);
        Assert.Equal(2, stake.Spots.Length);
        Assert.Equal(180, stake.Spots[0].Amount);
        Assert.Equal(2, stake.Spots[1].Bettors);
    }

    // Wallet balance never rides a game response: the cabinet reads chips at the table, and the
    // wallet is refreshed through its own store when a stake is refused.
    [Fact]
    public void AStakeResponseCarriesChipsAndNeverAWalletBalance()
    {
        var properties = typeof(CasinoRoomStakeDto).GetProperties();
        var hasStack = false;
        for (var index = 0; index < properties.Length; index++)
        {
            Assert.NotEqual("Balance", properties[index].Name);
            if (string.Equals(properties[index].Name, "Stack", StringComparison.Ordinal))
            {
                hasStack = true;
            }
        }

        Assert.True(hasStack);
    }

    [Fact]
    public void ARefusedStakeStillNamesAReasonTheCabinetCanSpeak()
    {
        const string json = "{\"granted\":false,\"reason\":\"closed\",\"roundId\":\"r7\",\"stack\":205}";
        var stake = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomStakeDto);

        Assert.NotNull(stake);
        Assert.False(stake.Granted);
        Assert.Equal(CasinoReasons.Closed, stake.Reason);
        Assert.True(CasinoReasons.TryMessage(stake.Reason, out _));
    }

    [Fact]
    public void EveryWheelReasonTheBackendCanSendHasItsOwnWords()
    {
        var reasons = new[]
        {
            CasinoReasons.Closed,
            CasinoReasons.Locked,
            CasinoReasons.NotRunning,
            CasinoReasons.StakeInvalid,
            CasinoReasons.Pacing,
            CasinoReasons.Unavailable,
            CasinoReasons.Ended,
            CasinoReasons.Restarting,
            CasinoReasons.Insufficient,
            CasinoReasons.StakesPaused,
            CasinoReasons.LossLimit,
            CasinoReasons.Cooldown,
            CasinoReasons.Frozen,
        };

        for (var index = 0; index < reasons.Length; index++)
        {
            Assert.True(CasinoReasons.TryMessage(reasons[index], out var message), reasons[index]);
            Assert.NotEqual(Aetherphone.Core.Localization.L.Casino.ReasonGeneric.Key, message.Key);
        }

        Assert.Equal("closed", CasinoReasons.Closed);
        Assert.Equal("locked", CasinoReasons.Locked);
        Assert.Equal("not_running", CasinoReasons.NotRunning);
        Assert.Equal("stake_invalid", CasinoReasons.StakeInvalid);
        Assert.Equal("pacing", CasinoReasons.Pacing);
        Assert.Equal("unavailable", CasinoReasons.Unavailable);
        Assert.Equal("ended", CasinoReasons.Ended);
        Assert.Equal("restarting", CasinoReasons.Restarting);
    }

    [Fact]
    public void ARoomSnapshotCarriesTheSpotBoardAndTheDrawnSegment()
    {
        const string json = "{\"roomId\":\"wheel-floor\",\"gameKind\":\"casino.wheel\",\"phase\":2,"
            + "\"roundId\":\"r7\",\"phaseEndsAtUnixMs\":1770000000000,\"playerCount\":9,\"entryCount\":14,"
            + "\"stakeTotal\":640,\"numbers\":[31],"
            + "\"spots\":[{\"spot\":0,\"bettors\":5,\"amount\":220},{\"spot\":4,\"bettors\":1,\"amount\":20}]}";
        var snapshot = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoRoomSnapshotDto);

        Assert.NotNull(snapshot);
        Assert.Equal(CasinoRoomPhases.Result, snapshot.Phase);
        Assert.Equal(9, snapshot.PlayerCount);
        Assert.NotNull(snapshot.Numbers);
        Assert.Equal(31, snapshot.Numbers[0]);
        Assert.NotNull(snapshot.Spots);
        Assert.Equal(2, snapshot.Spots.Length);
        Assert.Equal(220, snapshot.Spots[0].Amount);
        Assert.Equal(4, snapshot.Spots[1].Spot);
    }

    // A spot total is a running figure, so a board that arrived replaces the held one outright.
    // Merging it the way draws merge would double every pot the moment a second bet landed.
    [Fact]
    public void AFreshSpotBoardReplacesTheHeldOneAndAnAbsentOneLeavesIt()
    {
        var held = new[] { new CasinoRoomSpotDto(0, 2, 40) };
        var fresh = new[] { new CasinoRoomSpotDto(0, 3, 65) };

        Assert.Same(fresh, CasinoRoomSession.Replaced(held, fresh, false));
        Assert.Same(fresh, CasinoRoomSession.Replaced(held, fresh, true));
        Assert.Same(held, CasinoRoomSession.Replaced(held, null, false));
        Assert.Null(CasinoRoomSession.Replaced(held, null, true));
        Assert.Null(CasinoRoomSession.Replaced<CasinoRoomSpotDto>(null, null, false));
    }

    [Fact]
    public void AnEventThatOnlyMovesTheBoardLeavesTheRestOfTheRoomAlone()
    {
        var snapshot = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.WheelFloor,
            GameKind: CasinoWire.WheelKind,
            Phase: CasinoRoomPhases.Open,
            RoundId: "r7",
            PhaseEndsAtUnixMs: 1770000000000,
            PlayerCount: 9,
            StakeTotal: 200,
            Spots: new[] { new CasinoRoomSpotDto(0, 2, 40) });
        var held = new CasinoRoomState(CasinoRoomIds.WheelFloor, 1, 5, snapshot, null);

        var applied = CasinoRoomSession.Applied(held, 1, 6, new CasinoRoomEventDto(
            StakeTotal: 265,
            Spots: new[] { new CasinoRoomSpotDto(0, 3, 65) }));

        Assert.Equal(6, applied.Seq);
        Assert.Equal("r7", applied.Snapshot.RoundId);
        Assert.Equal(CasinoRoomPhases.Open, applied.Snapshot.Phase);
        Assert.Equal(1770000000000, applied.Snapshot.PhaseEndsAtUnixMs);
        Assert.Equal(9, applied.Snapshot.PlayerCount);
        Assert.Equal(265, applied.Snapshot.StakeTotal);
        Assert.NotNull(applied.Snapshot.Spots);
        Assert.Equal(65, applied.Snapshot.Spots[0].Amount);
        Assert.Equal(3, applied.Snapshot.Spots[0].Bettors);
    }

    [Fact]
    public void ARoundChangeClearsLastRoundsBoardAndEntries()
    {
        var snapshot = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.WheelFloor,
            GameKind: CasinoWire.WheelKind,
            Phase: CasinoRoomPhases.Result,
            RoundId: "r7",
            Numbers: new[] { 31 },
            Spots: new[] { new CasinoRoomSpotDto(0, 3, 65) });
        var personal = new CasinoRoomPrivateDto("r7", 25, new[] { new CasinoRoomEntryDto("e1", 0, 25, 3) });
        var held = new CasinoRoomState(CasinoRoomIds.WheelFloor, 1, 5, snapshot, personal);

        var applied = CasinoRoomSession.Applied(held, 1, 6,
            new CasinoRoomEventDto(Phase: CasinoRoomPhases.Open, RoundId: "r8"));

        Assert.Equal("r8", applied.Snapshot.RoundId);
        Assert.Null(applied.Snapshot.Spots);
        Assert.Null(applied.Snapshot.Numbers);
        Assert.Null(applied.Private);
    }

    // The drawn wedge rides Numbers, the same field a bingo ball would, so one round carrier
    // serves both games and the wheel simply draws once.
    [Fact]
    public void TheDrawnWedgeSurvivesAnEventThatOnlyReportsPlayerCount()
    {
        var snapshot = new CasinoRoomSnapshotDto(
            RoomId: CasinoRoomIds.WheelFloor,
            GameKind: CasinoWire.WheelKind,
            Phase: CasinoRoomPhases.Locked,
            RoundId: "r7",
            Numbers: new[] { 44 });
        var held = new CasinoRoomState(CasinoRoomIds.WheelFloor, 1, 5, snapshot, null);

        var applied = CasinoRoomSession.Applied(held, 1, 6, new CasinoRoomEventDto(PlayerCount: 11));

        Assert.NotNull(applied.Snapshot.Numbers);
        Assert.Single(applied.Snapshot.Numbers);
        Assert.Equal(44, applied.Snapshot.Numbers[0]);
        Assert.Equal(11, applied.Snapshot.PlayerCount);
    }
}
