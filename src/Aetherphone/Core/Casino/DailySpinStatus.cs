using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum DailySpinClaim
{
    Available,
    Claimed,
    Denied,
}

// Three sentences the card can say, and there is deliberately no fourth for "we do not know yet".
// The spin has no read route, so a phone that has not asked cannot tell an unspent turn from a
// spent one: it offers the wheel, because a card hidden on a guess costs the player a free spin
// while a card offered on a guess costs one tap that answers itself.
//
// Only an answer that says the day's spin landed closes the button. A refusal the player might be
// able to clear (a paused platform, a frozen wallet, a cap that resets) leaves it pressable, which
// is safe because the round id is derived from the identity and the day: a retry re-reads the same
// wheel rather than spinning a new one.
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

    // A granted spin reports what reached the wallet; a replay reports the same amount the ledger
    // already paid. Either way the number on screen is the server's, never the printed ladder,
    // because an account cap can clip a wedge without changing which wedge it was.
    public static long AwardOf(CasinoDailySpinDto? answer)
    {
        return answer is null ? 0 : answer.Amount;
    }
}
