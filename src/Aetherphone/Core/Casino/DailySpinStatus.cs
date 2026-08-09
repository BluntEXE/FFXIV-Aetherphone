using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum DailySpinClaim
{
    Unknown,
    Available,
    Claimed,
    Denied,
}

// Three sentences the card can say and one it must not. A spin that is available offers itself, a
// spin already taken shows when the next one lands, and anything else names the wall it hit in
// words. The state that must never appear is a dead pill: an unavailable spin with no explanation
// reads as a broken button, so a refusal the server did not bother to name is treated as the one
// benign explanation there is, which is that today's spin is already spent.
internal static class DailySpinStatus
{
    public static DailySpinClaim Of(bool loaded, CasinoDailySpinStateDto? state)
    {
        if (!loaded || state is null)
        {
            return DailySpinClaim.Unknown;
        }

        if (state.Available)
        {
            return DailySpinClaim.Available;
        }

        if (state.Reason.Length == 0 || string.Equals(state.Reason, CasinoReasons.Claimed, StringComparison.Ordinal))
        {
            return DailySpinClaim.Claimed;
        }

        return DailySpinClaim.Denied;
    }

    public static bool CanClaim(bool loaded, CasinoDailySpinStateDto? state, bool inFlight)
    {
        return !inFlight && Of(loaded, state) == DailySpinClaim.Available;
    }

    public static bool ShowsReset(DailySpinClaim claim)
    {
        return claim == DailySpinClaim.Claimed || claim == DailySpinClaim.Denied;
    }
}
