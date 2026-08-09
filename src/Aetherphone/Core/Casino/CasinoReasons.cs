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
