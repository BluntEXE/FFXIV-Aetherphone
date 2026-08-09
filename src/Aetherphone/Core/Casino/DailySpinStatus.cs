using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum DailySpinClaim
{
    Available,
    Claimed,
    Denied,
}

internal static class DailySpinStatus
{
    public static DailySpinClaim Of(CasinoDailySpinDto? answer)
    {
        if (answer is null)
        {
            return DailySpinClaim.Available;
        }

        if (answer.Granted)
        {
            return DailySpinClaim.Claimed;
        }

        if (answer.Reason.Length == 0
            || string.Equals(answer.Reason, CasinoReasons.AlreadyClaimed, StringComparison.Ordinal))
        {
            return DailySpinClaim.Claimed;
        }

        return DailySpinClaim.Denied;
    }

    public static bool CanClaim(CasinoDailySpinDto? answer, bool inFlight)
    {
        return !inFlight && Of(answer) != DailySpinClaim.Claimed;
    }

    public static bool ShowsReset(DailySpinClaim claim)
    {
        return claim == DailySpinClaim.Claimed;
    }

    public static long AwardOf(CasinoDailySpinDto? answer)
    {
        return answer is null ? 0 : answer.Amount;
    }
}
