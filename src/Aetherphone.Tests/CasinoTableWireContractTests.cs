using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

// The shapes the backend has to implement, written the way the client will read them. Every one of
// these is round tripped through the real source generated context, because a DTO that is not
// registered there deserializes to nothing at runtime and the screen simply stays empty.
public sealed class CasinoTableWireContractTests
{
    [Fact]
    public void TheDirectoryCarriesSeatsSpectatorsAndStakes()
    {
        const string json = """
        {
          "tables": [
            {
              "roomId": "blackjack-table",
              "gameKind": "casino.blackjack",
              "name": "Emerald room",
              "stakeTier": 1,
              "minBet": 10,
              "maxBet": 500,
              "minBuyIn": 100,
              "maxBuyIn": 2000,
              "seatCount": 5,
              "seatsTaken": 3,
              "spectators": 7,
              "inviteOnly": false,
              "owner": false,
              "seated": true,
              "draining": false,
              "phase": 0,
              "phaseEndsAtUnixMs": 1750000000000
            }
          ],
          "serverNowUnixMs": 1749999999000
        }
        """;

        var directory = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoTableListDto);
        Assert.NotNull(directory);
        Assert.Single(directory!.Tables!);
        var row = directory.Tables![0];
        Assert.Equal("blackjack-table", row.RoomId);
        Assert.Equal(CasinoWire.BlackjackKind, row.GameKind);
        Assert.Equal(3, row.SeatsTaken);
        Assert.Equal(5, row.SeatCount);
        Assert.Equal(7, row.Spectators);
        Assert.True(row.Seated);
        Assert.Equal(1749999999000, directory.ServerNowUnixMs);
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

    // A blob without the new block still has to load, because a table on an older server is a table
    // with no care surfaces rather than a table that fails to render.
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
}
