namespace Aetherphone.Core.Casino;

// A verbatim mirror of the ratified Wager Wheel table, and the client half of a contract the
// backend wheel engine has to match segment for segment: the cabinet paints the rim from
// Segments and quotes every spot's odds from SegmentCounts, so a layout that drifted from the
// server would draw a wheel that lands somewhere other than where the house says it landed.
//
// The rim is the flatten of A A A A B B C where A is 1,3,1,5,1,3,1,10 and B is 1,3,1,5,1,20 and
// C is 1,3,5,1,3,5. No two neighbours share a spot, which is the whole point of the shape: the
// ones alternate between almost every other segment so a wheel read at phone size never looks
// like one fat wedge, and the two 20x segments sit a third of the rim apart.
internal static class WheelRules
{
    public const int SegmentCount = 50;

    public const int SpotCount = 5;

    public const long MinStakePerSpot = 5;

    public const long MaxStakePerSpot = 50;

    public const long MaxStakePerRound = 200;

    public static readonly int[] Multipliers = { 1, 3, 5, 10, 20 };

    public static readonly int[] SegmentCounts = { 24, 12, 8, 4, 2 };

    public static readonly int[] Segments =
    {
        0, 1, 0, 2, 0, 1, 0, 3,
        0, 1, 0, 2, 0, 1, 0, 3,
        0, 1, 0, 2, 0, 1, 0, 3,
        0, 1, 0, 2, 0, 1, 0, 3,
        0, 1, 0, 2, 0, 4,
        0, 1, 0, 2, 0, 4,
        0, 1, 2, 0, 1, 2,
    };

    public static bool IsSpot(int spot)
    {
        return spot >= 0 && spot < SpotCount;
    }

    public static bool IsSegment(int segment)
    {
        return segment >= 0 && segment < SegmentCount;
    }

    public static int SpotAt(int segment)
    {
        return IsSegment(segment) ? Segments[segment] : -1;
    }

    public static int MultiplierOf(int spot)
    {
        return IsSpot(spot) ? Multipliers[spot] : 0;
    }

    public static int SegmentsOn(int spot)
    {
        return IsSpot(spot) ? SegmentCounts[spot] : 0;
    }

    public static bool Wins(int segment, int spot)
    {
        return IsSpot(spot) && SpotAt(segment) == spot;
    }

    // A winning spot returns the stake alongside the winnings, which is why every quoted return
    // is (multiplier + 1) and not the multiplier: a 1x spot doubles the stake, it does not
    // hand back exactly what it took.
    public static long Returned(int segment, int spot, long stake)
    {
        return Wins(segment, spot) ? stake * (MultiplierOf(spot) + 1) : 0;
    }

    public static bool IsStakeInRange(long amount)
    {
        return amount >= MinStakePerSpot && amount <= MaxStakePerSpot;
    }

    // The room cap is the one the player cannot see coming: the per-spot bound is printed on the
    // composer, but a fifth bet that would cross 200 for the round has to be clamped before the
    // POST leaves rather than bounced by the server with money already committed elsewhere.
    public static long Headroom(long stakedThisRound)
    {
        var left = MaxStakePerRound - stakedThisRound;
        if (left <= 0)
        {
            return 0;
        }

        return left < MaxStakePerSpot ? left : MaxStakePerSpot;
    }

    public static long Clamp(long amount, long stakedThisRound, long stack)
    {
        var ceiling = Headroom(stakedThisRound);
        if (stack < ceiling)
        {
            ceiling = stack;
        }

        if (ceiling < MinStakePerSpot)
        {
            return 0;
        }

        if (amount < MinStakePerSpot)
        {
            return 0;
        }

        return amount > ceiling ? ceiling : amount;
    }
}
