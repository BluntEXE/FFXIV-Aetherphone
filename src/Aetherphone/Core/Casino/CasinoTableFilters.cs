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

internal static class CasinoTableKinds
{
    public const int Solo = 0;

    public const int House = 1;

    public const int Private = 2;
}

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
            CasinoTableFilter.Mine => row.Kind == CasinoTableKinds.Private,
            _ => true,
        };
    }

    public static bool HasOpenSeat(CasinoTableRowDto row)
    {
        return row.MaxSeats > 0 && row.SeatedCount < row.MaxSeats;
    }

    public static bool IsPrivate(CasinoTableRowDto row)
    {
        return row.Kind == CasinoTableKinds.Private;
    }

    public static int SpectatorsOf(CasinoTableRowDto row)
    {
        var watching = row.Occupancy - row.SeatedCount;
        return watching > 0 ? watching : 0;
    }
}
