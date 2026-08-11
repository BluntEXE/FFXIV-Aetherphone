using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoTableWireContractTests
{
    [Fact]
    public void TheDirectoryReadsTheShapeTheServerActuallySends()
    {
        const string json = """
        {
          "tables": [
            {
              "tableId": "blackjack-pit",
              "gameKind": "casino.blackjack",
              "kind": 1,
              "stakeTier": 0,
              "ownerUserId": "",
              "ownerName": "",
              "minBet": 5,
              "maxBet": 25,
              "minBuyIn": 100,
              "maxBuyIn": 2000,
              "maxSeats": 5,
              "seatedCount": 3,
              "occupancy": 7,
              "admitted": true,
              "reason": "",
              "inviteToken": "[aep.casino.v1:blackjack-pit]"
            }
          ],
          "serverNowUnixMs": 1749999999000
        }
        """;

        var directory = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableListDto);
        Assert.NotNull(directory);
        var row = Assert.Single(directory!.Tables!);
        Assert.Equal("blackjack-pit", row.TableId);
        Assert.Equal(CasinoWire.BlackjackKind, row.GameKind);
        Assert.Equal(5, row.MaxSeats);
        Assert.Equal(3, row.SeatedCount);
        Assert.Equal(100, row.MinBuyIn);
        Assert.Equal(2000, row.MaxBuyIn);
        Assert.True(row.Admitted);
        Assert.Equal(1749999999000, directory.ServerNowUnixMs);

        Assert.True(CasinoTableFilters.HasOpenSeat(row));
        Assert.Equal(4, CasinoTableFilters.SpectatorsOf(row));
        Assert.False(CasinoTableFilters.IsPrivate(row));
    }

    [Fact]
    public void AFullTableIsOnlyOneWhoseSeatsAreActuallyTaken()
    {
        var open = new CasinoTableRowDto(TableId: "t", MaxSeats: 5, SeatedCount: 3);
        var full = new CasinoTableRowDto(TableId: "t", MaxSeats: 5, SeatedCount: 5);
        var unseeded = new CasinoTableRowDto(TableId: "t");

        Assert.True(CasinoTableFilters.HasOpenSeat(open));
        Assert.False(CasinoTableFilters.HasOpenSeat(full));
        Assert.False(CasinoTableFilters.HasOpenSeat(unseeded));
    }

    [Fact]
    public void QuickSeatAnswersWithTheTableAndTheBuyIn()
    {
        const string json = """
        {
          "granted": true,
          "roomId": "blackjack-table",
          "name": "Emerald room",
          "minBuyIn": 100,
          "maxBuyIn": 2000,
          "suggestedBuyIn": 500,
          "minBet": 10,
          "maxBet": 500,
          "seatIndex": 2
        }
        """;

        var answer = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoQuickSeatDto);
        Assert.NotNull(answer);
        Assert.True(answer!.Granted);
        Assert.Equal("blackjack-table", answer.RoomId);
        Assert.Equal(500, answer.SuggestedBuyIn);
        Assert.Equal(2, answer.SeatIndex);
    }

    [Fact]
    public void ARefusedQuickSeatNamesItsReasonAndTheClientKnowsThatOne()
    {
        const string json = """{"granted":false,"reason":"no_tables"}""";
        var answer = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoQuickSeatDto);
        Assert.NotNull(answer);
        Assert.False(answer!.Granted);
        Assert.True(CasinoReasons.TryMessage(answer.Reason, out _));
    }

    [Fact]
    public void SittingAnswersWithTheWaitAndTheBinding()
    {
        const string json = """
        {
          "granted": true,
          "roomId": "blackjack-table",
          "seatIndex": 2,
          "joinsNextHand": true,
          "boundElsewhere": false,
          "seatHeldUntilUnixMs": 1750000060000,
          "stack": 480
        }
        """;

        var answer = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoSeatDto);
        Assert.NotNull(answer);
        Assert.True(answer!.JoinsNextHand);
        Assert.Equal(2, answer.SeatIndex);
        Assert.Equal(1750000060000, answer.SeatHeldUntilUnixMs);
        Assert.Equal(480, answer.Stack);
    }

    [Fact]
    public void StandingMidHandComesBackQueuedRatherThanRefused()
    {
        const string json = """{"granted":true,"roomId":"blackjack-table","atHandEnd":true,"balance":1480}""";
        var answer = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoStandDto);
        Assert.NotNull(answer);
        Assert.True(answer!.Granted);
        Assert.True(answer.AtHandEnd);
        Assert.Equal(1480, answer.Balance);
    }

    [Fact]
    public void TheDoorCarriesKnocksAndSeatsButNeverACrowd()
    {
        const string json = """
        {
          "roomId": "private-4f2a",
          "owner": true,
          "inviteToken": "private-4f2a",
          "knocks": [{"userId":"u1","displayName":"Tataru","handle":"@tataru","knockedAtUnix":1750000000}],
          "seated": [{"userId":"u2","displayName":"Hildibrand","handle":"@hildy"}]
        }
        """;

        var door = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableDoorDto);
        Assert.NotNull(door);
        Assert.True(door!.Owner);
        Assert.Single(door.Knocks!);
        Assert.Equal("Tataru", door.Knocks![0].DisplayName);
        Assert.Single(door.Seated!);
        Assert.Equal("@hildy", door.Seated![0].Handle);
    }

    [Fact]
    public void CreatingATableComesBackWithSomethingShareable()
    {
        const string json = """
        {"granted":true,"roomId":"private-4f2a","name":"Tataru's table","inviteToken":"private-4f2a","inviteOnly":true,"owner":true}
        """;

        var table = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableDto);
        Assert.NotNull(table);
        Assert.True(table!.InviteOnly);
        Assert.True(CasinoShare.TryParse(CasinoShare.Compose(table.InviteToken), out var parsed));
        Assert.Equal("private-4f2a", parsed);
    }

    [Fact]
    public void TheBlackjackBlobCarriesTheCareSurfaces()
    {
        const string json = """
        {
          "roundIndex": 12,
          "handId": "hand-12",
          "mySeat": 1,
          "tableName": "Emerald room",
          "spectators": 4,
          "boundElsewhere": true,
          "seatHeldUntilUnixMs": 1750000060000,
          "joinsNextHand": true,
          "draining": true,
          "inviteOnly": false,
          "owner": false
        }
        """;

        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBlackjackRoomStateDto);
        Assert.NotNull(board);
        Assert.Equal(4, board!.Spectators);
        Assert.True(board.BoundElsewhere);
        Assert.True(board.JoinsNextHand);
        Assert.True(board.Draining);
        Assert.Equal(1750000060000, board.SeatHeldUntilUnixMs);
        Assert.Equal("Emerald room", board.TableName);
    }

    [Fact]
    public void ABlobWithoutTheCareBlockStillLoads()
    {
        const string json = """{"roundIndex":1,"handId":"hand-1","mySeat":-1}""";
        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBlackjackRoomStateDto);
        Assert.NotNull(board);
        Assert.Equal(0, board!.Spectators);
        Assert.False(board.BoundElsewhere);
        Assert.Equal(0, board.SeatHeldUntilUnixMs);
    }

    [Fact]
    public void ATurnAlertGroupsOnItsTableAndTheRouterReadsItBack()
    {
        var notification = new PhoneNotification(CasinoTurnNotifier.AppId, "Your turn", "The table is waiting",
            System.DateTime.Now, default,
            string.Concat(CasinoTurnNotifier.GroupPrefix, CasinoRoomIds.BlackjackTable));
        Assert.Equal("casino:blackjack-table", notification.GroupKey);
        Assert.Equal("casino:blackjack-table", notification.StackKey);
        Assert.Equal("casino", notification.SettingsKey);

        var launcher = new CasinoLauncher();
        launcher.RequestTable(notification.GroupKey![CasinoTurnNotifier.GroupPrefix.Length..]);
        Assert.True(launcher.TryConsume(out var launch));
        Assert.Equal(CasinoLaunchKind.Table, launch.Kind);
        Assert.Equal(CasinoRoomIds.BlackjackTable, launch.TableId);
        Assert.False(launcher.TryConsume(out _));
    }

    [Fact]
    public void OneTurnKeyPerHandPerSplitSoAResyncCannotRingTwice()
    {
        var first = CasinoTurnNotifier.TurnKeyFor("hand-12", 1, 0);
        Assert.Equal(first, CasinoTurnNotifier.TurnKeyFor("hand-12", 1, 0));
        Assert.NotEqual(first, CasinoTurnNotifier.TurnKeyFor("hand-12", 1, 1));
        Assert.NotEqual(first, CasinoTurnNotifier.TurnKeyFor("hand-13", 1, 0));
        Assert.NotEqual(first, CasinoTurnNotifier.TurnKeyFor("hand-12", 2, 0));
    }

    [Fact]
    public void ATurnAlertStaysQuietWhileTheTableIsBeingWatched()
    {
        Assert.True(CasinoTurnNotifier.Watching(1_000, 1_200));
        Assert.False(CasinoTurnNotifier.Watching(1_000, 9_000));
        Assert.False(CasinoTurnNotifier.Watching(0, 9_000));
    }

    [Fact]
    public void TheHeldSeatCountdownRoundsUpSoItNeverShowsZeroWhileItIsStillHeld()
    {
        Assert.Equal(1, ReconnectVeil.SecondsOf(1));
        Assert.Equal(1, ReconnectVeil.SecondsOf(1_000));
        Assert.Equal(2, ReconnectVeil.SecondsOf(1_001));
        Assert.Equal(0, ReconnectVeil.SecondsOf(0));
    }

    [Fact]
    public void TheHandReadCarriesTheSameVersionPairTheSocketFrameDoes()
    {
        const string json = """
        {
          "roomId": "blackjack-table",
          "epoch": 3,
          "seq": 41,
          "roundIndex": 7,
          "seatIndex": 2,
          "hands": [[40, 41], [12]]
        }
        """;
        var hand = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoBlackjackHandReadDto);
        Assert.NotNull(hand);
        Assert.Equal("blackjack-table", hand!.RoomId);
        Assert.Equal(3, hand.Epoch);
        Assert.Equal(41, hand.Seq);
        Assert.Equal(7, hand.RoundIndex);
        Assert.Equal(2, hand.SeatIndex);
        Assert.Equal(new[] { 40, 41 }, hand.Hands![0]);
        Assert.Equal(new[] { 12 }, hand.Hands![1]);

        Assert.Equal("/casino/blackjack/blackjack-table/hand",
            Aetherphone.Core.Aethernet.Clients.CasinoClient.BlackjackMyHandPath(CasinoRoomIds.BlackjackTable));
        Assert.Equal("/casino/blackjack/a%20b/hand",
            Aetherphone.Core.Aethernet.Clients.CasinoClient.BlackjackMyHandPath("a b"));
    }

    [Fact]
    public void ABurnedSeatIdIsNeverCarriedToADifferentSeatOrBuyIn()
    {
        Assert.True(CasinoTablesStore.ReusesSeat(2, 500, 2, 500));
        Assert.False(CasinoTablesStore.ReusesSeat(2, 500, 4, 500));
        Assert.False(CasinoTablesStore.ReusesSeat(2, 500, 2, 200));
        Assert.False(CasinoTablesStore.ReusesSeat(-1, -1, 0, 0));

        Assert.True(CasinoTablesStore.ReusesCreate(1, 1));
        Assert.False(CasinoTablesStore.ReusesCreate(1, 2));
        Assert.False(CasinoTablesStore.ReusesCreate(int.MinValue, 0));
    }
}
