using System.Collections.Frozen;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Casino;

// The table has to cover every string the server can put on the wire, plus the handful the client
// raises for itself (unreachable is nobody's refusal but this phone's). A reason the server sends
// that is missing here renders as the generic apology however well the rest of the screen is
// worded, which is why the daily spin's vocabulary lives here too: its refusals come out of the
// coin ledger rather than the casino, so already_claimed and rule_cap arrive from a set none of
// the tables ever use.
internal static class CasinoReasons
{
    public const string StakesPaused = "stakes_paused";
    public const string LossLimit = "loss_limit";
    public const string Draining = "draining";
    public const string Cooldown = "cooldown";
    public const string StakeRange = "stake_range";
    public const string BuyInRange = "buyin_range";
    public const string SittingOpen = "sitting_open";
    public const string Insufficient = "insufficient";
    public const string Frozen = "frozen";
    public const string Expired = "expired";
    public const string TableClosed = "table_closed";
    public const string RoundOpen = "round_open";
    public const string CapReached = "cap_reached";
    public const string Closed = "closed";
    public const string Locked = "locked";
    public const string NotRunning = "not_running";
    public const string StakeInvalid = "stake_invalid";
    public const string Pacing = "pacing";
    public const string Unavailable = "unavailable";
    public const string Ended = "ended";
    public const string Restarting = "restarting";
    public const string Unreachable = "unreachable";
    public const string AlreadyClaimed = "already_claimed";
    public const string Paused = "paused";
    public const string DailyCap = "daily_cap";
    public const string RuleCap = "rule_cap";
    public const string CardsFull = "cards_full";

    public static readonly string[] All =
    {
        StakesPaused,
        LossLimit,
        Draining,
        Cooldown,
        StakeRange,
        BuyInRange,
        SittingOpen,
        Insufficient,
        Frozen,
        Expired,
        TableClosed,
        RoundOpen,
        CapReached,
        Closed,
        Locked,
        NotRunning,
        StakeInvalid,
        Pacing,
        Unavailable,
        Ended,
        Restarting,
        Unreachable,
        AlreadyClaimed,
        Paused,
        DailyCap,
        RuleCap,
        CardsFull,
    };

    private static readonly FrozenDictionary<string, LocString> Messages = new Dictionary<string, LocString>
    {
        [StakesPaused] = L.Casino.ReasonStakesPaused,
        [LossLimit] = L.Casino.ReasonLossLimit,
        [Draining] = L.Casino.ReasonDraining,
        [Cooldown] = L.Casino.ReasonCooldown,
        [StakeRange] = L.Casino.ReasonStakeRange,
        [BuyInRange] = L.Casino.ReasonBuyInRange,
        [SittingOpen] = L.Casino.ReasonSittingOpen,
        [Insufficient] = L.Casino.ReasonInsufficient,
        [Frozen] = L.Casino.ReasonFrozen,
        [Expired] = L.Casino.ReasonExpired,
        [TableClosed] = L.Casino.ReasonTableClosed,
        [RoundOpen] = L.Casino.ReasonRoundOpen,
        [CapReached] = L.Casino.ReasonCapReached,
        [Closed] = L.Casino.ReasonClosed,
        [Locked] = L.Casino.ReasonLocked,
        [NotRunning] = L.Casino.ReasonNotRunning,
        [StakeInvalid] = L.Casino.ReasonStakeInvalid,
        [Pacing] = L.Casino.ReasonPacing,
        [Unavailable] = L.Casino.ReasonUnavailable,
        [Ended] = L.Casino.ReasonEnded,
        [Restarting] = L.Casino.ReasonRestarting,
        [Unreachable] = L.Casino.ReasonUnreachable,
        [AlreadyClaimed] = L.Casino.ReasonClaimed,
        [Paused] = L.Casino.ReasonPaused,
        [DailyCap] = L.Casino.ReasonDailyCap,
        [RuleCap] = L.Casino.ReasonRuleCap,
        [CardsFull] = L.Casino.ReasonCardsFull,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool TryMessage(string reason, out LocString message)
    {
        return Messages.TryGetValue(reason, out message);
    }

    public static LocString MessageFor(string reason)
    {
        return Messages.TryGetValue(reason, out var message) ? message : L.Casino.ReasonGeneric;
    }
}
