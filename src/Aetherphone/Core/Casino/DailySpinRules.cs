namespace Aetherphone.Core.Casino;

// The one wheel on the floor that pays coins instead of chips, and the only Saucer surface that
// mints rather than moves: a cash-out returns money the player already owned, so it stays quiet,
// while this hands out coins that did not exist a moment ago, so it celebrates.
//
// The ladder is a mirror of DailySpinEngine.Awards on the server and has to stay one, because the
// rim art paints AwardOf on every wedge while the pointer rests on the segment the server drew:
// a table that differs by one number puts a wedge under the pointer that contradicts the banner
// beneath it. Sixteen equally likely segments, no stake, once per coin day, summing to 280 for an
// expected 17.5 coins, deliberately under the 20 coin daily check-in so a second character is
// never worth more for the spin alone.
internal static class DailySpinRules
{
    public const int SegmentCount = 16;

    public const long TotalAward = 280;

    public const long TopAward = 60;

    public static readonly long[] Awards =
    {
        5, 15, 10, 30,
        5, 20, 10, 45,
        5, 15, 10, 60,
        5, 20, 10, 15,
    };

    public static bool IsSegment(int segment)
    {
        return segment >= 0 && segment < SegmentCount;
    }

    public static long AwardOf(int segment)
    {
        return IsSegment(segment) ? Awards[segment] : 0;
    }

    public static bool IsTopAward(int segment)
    {
        return AwardOf(segment) >= TopAward;
    }
}
