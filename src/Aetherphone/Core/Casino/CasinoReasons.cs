using System.Collections.Frozen;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Casino;

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
