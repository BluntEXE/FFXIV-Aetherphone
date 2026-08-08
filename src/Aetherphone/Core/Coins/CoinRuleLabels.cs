using System.Collections.Frozen;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Coins;

internal static class CoinRuleLabels
{
    private static readonly FrozenDictionary<string, LocString> Labels = new Dictionary<string, LocString>
    {
        ["coin.checkin"] = L.Coin.RuleCheckin,
        ["coin.streak"] = L.Coin.RuleStreak,
        ["coin.welcome"] = L.Coin.RuleWelcome,
        ["call.connected"] = L.Coin.RuleCall,
        ["chat.conversation"] = L.Coin.RuleChat,
        ["game.session"] = L.Coin.RuleGameSession,
        ["game.deep"] = L.Coin.RuleGameDeep,
        ["game.featured"] = L.Coin.RuleGameFeatured,
        ["chirp.survived"] = L.Coin.RuleChirp,
        ["gram.survived"] = L.Coin.RuleGram,
        ["story.survived"] = L.Coin.RuleStory,
        ["comment.survived"] = L.Coin.RuleComment,
        ["purchase"] = L.Coin.RulePurchase,
        ["staff.adjust"] = L.Coin.RuleStaffGrant,
        ["staff.clawback"] = L.Coin.RuleClawback,
        ["carry.forward"] = L.Coin.RuleCarry,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static LocString For(string ruleId)
    {
        return Labels.TryGetValue(ruleId, out var label) ? label : L.Coin.RuleGeneric;
    }
}
