namespace Aetherphone.Core.Casino;

// The one wheel on the floor that pays coins instead of chips, and the only Saucer surface that
// mints rather than moves: a cash-out returns money the player already owned, so it stays quiet,
// while this hands out coins that did not exist a moment ago, so it celebrates.
//
// Sixteen segments, no stake, once per coin day. Every other award sits on a five, which is what
// keeps the rim readable at phone size and stops the wheel from looking like one fat wedge, and
// the eight paying segments climb 12, 20, 12, 24, 12, 20, 12, 40 with the best two a half turn
// apart. The table sums to 192 for an expected twelve coins a day, comfortably under the wallet's
// own daily cap, so the spin can never be the thing that closes a player's earning day on its own.
internal static class DailySpinRules
{
    public const int SegmentCount = 16;

    public const long TotalAward = 192;

    public const long ExpectedAward = TotalAward / SegmentCount;

    public const long TopAward = 40;

    public static readonly long[] Awards =
    {
        5, 12, 5, 20,
        5, 12, 5, 24,
        5, 12, 5, 20,
        5, 12, 5, 40,
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
