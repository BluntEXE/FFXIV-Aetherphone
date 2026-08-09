using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

// Choreography of a landing the house already made. The segment is settled on the server before
// the rim moves, and everything here only stages how it is revealed: the total sweep is chosen
// so the wheel comes to rest with that exact segment under the pointer, and the curve that eats
// it is monotonic and lands flat. No overshoot, no creep past the wedge and back, no drifting to
// the neighbour for a beat first. A near miss the house never dealt is a lie told with easing.
//
// Angles are radians clockwise from the pointer, which sits at the top. At angle 0 the centre of
// segment 0 is under the pointer, so a wheel resting on segment k sits at -k spans, normalised.
// The starting angle only decides where the sweep begins: the landing is congruent to the
// segment whatever it was, which is what lets a client that cold-opened mid spin (and has no
// idea where the rim was) still stop on the same wedge as one that watched from the lock.
internal static class WheelChoreography
{
    public const float SpinSeconds = 4.2f;

    public const int SpinTurns = 4;

    public const float Tau = MathF.PI * 2f;

    public const float SegmentSpan = Tau / WheelRules.SegmentCount;

    // EaseOutQuint leaves the lock at five times the average sweep rate, so the free run before
    // the deceleration is played at exactly that speed and the two meet without a visible kink.
    private const float HandoffSlope = 5f;

    // A rim is a rim: the daily spin has sixteen segments instead of fifty and the same landing has
    // to be staged for it, so the span aware overloads carry the shape and the wheel's own methods
    // are the fifty segment case of them.
    public static float SpanFor(int segmentCount)
    {
        return Tau / segmentCount;
    }

    public static float RestAngleOf(int segment, int segmentCount)
    {
        return Normalize(-segment * SpanFor(segmentCount));
    }

    public static float SweepFor(float fromAngle, int segment, int segmentCount, int turns)
    {
        var advance = Normalize(RestAngleOf(segment, segmentCount) - fromAngle);
        return turns * Tau + advance;
    }

    public static float RestAngleOf(int segment)
    {
        return Normalize(-segment * SegmentSpan);
    }

    public static int SegmentUnderPointer(float angle)
    {
        var steps = (int)MathF.Round(-Normalize(angle) / SegmentSpan);
        var wrapped = steps % WheelRules.SegmentCount;
        return wrapped < 0 ? wrapped + WheelRules.SegmentCount : wrapped;
    }

    public static float Normalize(float angle)
    {
        var wrapped = angle - Tau * MathF.Floor(angle / Tau);
        return wrapped >= Tau ? 0f : wrapped;
    }

    public static float SweepFor(float fromAngle, int segment, int turns)
    {
        var advance = Normalize(RestAngleOf(segment) - fromAngle);
        return turns * Tau + advance;
    }

    public static float SweepFor(float fromAngle, int segment)
    {
        return SweepFor(fromAngle, segment, SpinTurns);
    }

    public static float Progress(float elapsedSeconds)
    {
        if (elapsedSeconds <= 0f)
        {
            return 0f;
        }

        return elapsedSeconds >= SpinSeconds ? 1f : elapsedSeconds / SpinSeconds;
    }

    // Before the deceleration begins the rim runs free at the speed the curve is about to hand
    // it, so a lock window longer than the spin shows a wheel already in motion instead of a
    // wheel waiting to start.
    public static float AngleAt(float fromAngle, float sweep, float elapsedSeconds)
    {
        if (elapsedSeconds >= SpinSeconds)
        {
            return fromAngle + sweep;
        }

        if (elapsedSeconds <= 0f)
        {
            return fromAngle + elapsedSeconds * (HandoffSlope * sweep / SpinSeconds);
        }

        return fromAngle + sweep * Easing.EaseOutQuint(elapsedSeconds / SpinSeconds);
    }
}
