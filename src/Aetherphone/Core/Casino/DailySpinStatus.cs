using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum DailySpinClaim
{
    Unknown,
    Available,
    Claimed,
    Denied,
}

// Granted answers a claim, Claimed answers the day, and the difference is the whole point of this
// type: an answer we have not fetched yet is Unknown rather than Available, because a card that
// assumes the wheel is free every time the plugin reloads promises a spin the server then refuses.
internal static class DailySpinStatus
{
    public static DailySpinClaim Of(CasinoDailySpinDto? answer)
    {
        if (answer is null)
        {
            return DailySpinClaim.Unknown;
        }

        if (answer.Granted
            || answer.Claimed
            || string.Equals(answer.Reason, CasinoReasons.AlreadyClaimed, StringComparison.Ordinal))
        {
            return DailySpinClaim.Claimed;
        }

        return answer.Reason.Length == 0 ? DailySpinClaim.Available : DailySpinClaim.Denied;
    }

    // Unknown leaves the pill live on purpose. The status read is the only thing that closes that
    // window, so a read that never lands must fall back to letting the player ask the server rather
    // than to a button that can never be pressed.
    public static bool CanClaim(CasinoDailySpinDto? answer, bool inFlight)
    {
        return !inFlight && Of(answer) != DailySpinClaim.Claimed;
    }

    public static bool ShowsReset(DailySpinClaim claim)
    {
        return claim == DailySpinClaim.Claimed;
    }

    public static bool OffersWheel(DailySpinClaim claim)
    {
        return claim == DailySpinClaim.Available;
    }

    public static long AwardOf(CasinoDailySpinDto? answer)
    {
        return answer is null ? 0 : answer.Amount;
    }
}
