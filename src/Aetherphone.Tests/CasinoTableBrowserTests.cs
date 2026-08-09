using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

// The browser is a filter over a directory and a token parser, and both are places where a quiet
// mistake reads as a missing table rather than as a bug. These pin the filter as a pure function of
// the row and the token as a reference that accepts only what a table id can be.
public sealed class CasinoTableBrowserTests
{
    [Fact]
    public void TheAllFilterKeepsEveryRow()
    {
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.All, Row(seatsTaken: 5, seatCount: 5)));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.All, Row(inviteOnly: true)));
    }

    [Fact]
    public void OpenSeatsMeansTheTableCanStillTakeSomebody()
    {
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.OpenSeats, Row(seatsTaken: 3, seatCount: 5)));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.OpenSeats, Row(seatsTaken: 5, seatCount: 5)));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.OpenSeats, Row(seatsTaken: 0, seatCount: 0)));
    }

    [Fact]
    public void TheStakeBandsReadTheTableMinimum()
    {
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.LowStakes, Row(minBet: 10)));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.LowStakes,
            Row(minBet: CasinoTableFilters.LowStakeCeiling)));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.LowStakes,
            Row(minBet: CasinoTableFilters.LowStakeCeiling + 1)));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.HighStakes,
            Row(minBet: CasinoTableFilters.HighStakeFloor)));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.HighStakes,
            Row(minBet: CasinoTableFilters.HighStakeFloor - 1)));
    }

    // A table with no minimum at all is a table the directory could not describe, and it must not
    // land in the low band by arithmetic accident.
    [Fact]
    public void ATableWithoutAMinimumIsNotALowStakesTable()
    {
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.LowStakes, Row(minBet: 0)));
    }

    [Fact]
    public void MineIsATableIHostOrAmSittingAt()
    {
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Mine, Row(owner: true)));
        Assert.True(CasinoTableFilters.Matches(CasinoTableFilter.Mine, Row(seated: true)));
        Assert.False(CasinoTableFilters.Matches(CasinoTableFilter.Mine, Row()));
    }

    [Fact]
    public void QuickSeatInheritsTheBandTheRailIsShowing()
    {
        Assert.Equal(CasinoStakeTiers.Low, CasinoStakeTiers.From(CasinoTableFilter.LowStakes));
        Assert.Equal(CasinoStakeTiers.High, CasinoStakeTiers.From(CasinoTableFilter.HighStakes));
        Assert.Equal(CasinoStakeTiers.Any, CasinoStakeTiers.From(CasinoTableFilter.All));
        Assert.Equal(CasinoStakeTiers.Any, CasinoStakeTiers.From(CasinoTableFilter.OpenSeats));
        Assert.Equal(CasinoStakeTiers.Any, CasinoStakeTiers.From(CasinoTableFilter.Mine));
    }

    [Fact]
    public void EveryFilterOnTheRailIsListedOnce()
    {
        Assert.Equal(5, CasinoTableFilters.All.Length);
        Assert.Equal(CasinoTableFilter.All, CasinoTableFilters.All[0]);
    }

    [Fact]
    public void AnInviteTokenRoundTrips()
    {
        var token = CasinoShare.Compose("blackjack-table");
        Assert.Equal("[aep.casino.v1:blackjack-table]", token);
        Assert.True(CasinoShare.TryParse(token, out var tableId));
        Assert.Equal("blackjack-table", tableId);
        Assert.True(CasinoShare.IsToken("  " + token + "  "));
    }

    [Fact]
    public void ABareTableIdPastedWithoutItsWrapperStillNamesTheTable()
    {
        Assert.True(CasinoShare.TryParse("private-4f2a", out var tableId));
        Assert.Equal("private-4f2a", tableId);
    }

    [Fact]
    public void RubbishIsNotATable()
    {
        Assert.False(CasinoShare.TryParse(null, out _));
        Assert.False(CasinoShare.TryParse(string.Empty, out _));
        Assert.False(CasinoShare.TryParse("[aep.casino.v1:]", out _));
        Assert.False(CasinoShare.TryParse("[aep.muster.v1:abc]", out _));
        Assert.False(CasinoShare.TryParse("table id with spaces", out _));
        Assert.False(CasinoShare.TryParse("../../etc/passwd", out _));
        Assert.False(CasinoShare.TryParse(new string('a', CasinoShare.MaxIdLength + 1), out _));
    }

    [Fact]
    public void TheTableRoutesAreWhereTheBackendMustPutThem()
    {
        Assert.Equal("/casino/tables", CasinoClient.TablesPath);
        Assert.Equal("/casino/tables/quickseat", CasinoClient.QuickSeatPath);
        Assert.Equal("/casino/tables", CasinoClient.TablesPagePath(string.Empty));
        Assert.Equal("/casino/tables?game=casino.blackjack",
            CasinoClient.TablesPagePath(CasinoWire.BlackjackKind));
        Assert.Equal("/casino/tables/blackjack-table/sit",
            CasinoClient.TablePath(CasinoRoomIds.BlackjackTable, "sit"));
        Assert.Equal("/casino/tables/blackjack-table/claim",
            CasinoClient.TablePath(CasinoRoomIds.BlackjackTable, "claim"));
        Assert.Equal("/casino/tables/a%20b/door", CasinoClient.TablePath("a b", "door"));
    }

    // A refusal at the door is a question, not a wall, so the table screen has to offer the knock
    // for exactly the reasons that a knock can answer and for no others.
    [Fact]
    public void OnlyADoorRefusalOffersTheKnock()
    {
        Assert.True(BlackjackTable.AsksToJoin(CasinoReasons.InviteOnly));
        Assert.True(BlackjackTable.AsksToJoin(CasinoReasons.NotMember));
        Assert.True(BlackjackTable.AsksToJoin(CasinoReasons.Denied));
        Assert.False(BlackjackTable.AsksToJoin(CasinoReasons.BannedFromTable));
        Assert.False(BlackjackTable.AsksToJoin(CasinoReasons.Ended));
        Assert.False(BlackjackTable.AsksToJoin(CasinoReasons.Full));
    }

    private static CasinoTableRowDto Row(long minBet = 10, int seatsTaken = 1, int seatCount = 5,
        bool inviteOnly = false, bool owner = false, bool seated = false)
    {
        return new CasinoTableRowDto(
            RoomId: "blackjack-table",
            GameKind: CasinoWire.BlackjackKind,
            Name: "Emerald room",
            MinBet: minBet,
            MaxBet: 500,
            SeatCount: seatCount,
            SeatsTaken: seatsTaken,
            InviteOnly: inviteOnly,
            Owner: owner,
            Seated: seated);
    }
}
