using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum CasinoTableFilter
{
    All,
    OpenSeats,
    LowStakes,
    HighStakes,
    Mine,
}

// Quick seat asks for a band rather than a table, and the band is read off whatever the rail is
// already showing: a player who has narrowed the list to low stakes and then presses quick seat
// plainly means a low stakes table, so asking them again would be a question they have answered.
internal static class CasinoStakeTiers
{
    public const int Any = 0;

    public const int Low = 1;

    public const int High = 2;

    public static int From(CasinoTableFilter filter)
    {
        return filter switch
        {
            CasinoTableFilter.LowStakes => Low,
            CasinoTableFilter.HighStakes => High,
            _ => Any,
        };
    }
}

// One rail, five filters, and every one of them a fact the row already carries. Anything that needed
// a second read to answer would leave the rail lying whenever the directory was a minute old, which
// is why there is no "friends here" chip: the directory ships counts, not rosters.
internal static class CasinoTableFilters
{
    public const long LowStakeCeiling = 25;

    public const long HighStakeFloor = 100;

    public static readonly CasinoTableFilter[] All =
    {
        CasinoTableFilter.All,
        CasinoTableFilter.OpenSeats,
        CasinoTableFilter.LowStakes,
        CasinoTableFilter.HighStakes,
        CasinoTableFilter.Mine,
    };

    public static bool Matches(CasinoTableFilter filter, CasinoTableRowDto row)
    {
        return filter switch
        {
            CasinoTableFilter.OpenSeats => HasOpenSeat(row),
            CasinoTableFilter.LowStakes => row.MinBet > 0 && row.MinBet <= LowStakeCeiling,
            CasinoTableFilter.HighStakes => row.MinBet >= HighStakeFloor,
            CasinoTableFilter.Mine => row.Owner || row.Seated,
            _ => true,
        };
    }

    public static bool HasOpenSeat(CasinoTableRowDto row)
    {
        return row.SeatCount > 0 && row.SeatsTaken < row.SeatCount;
    }
}
