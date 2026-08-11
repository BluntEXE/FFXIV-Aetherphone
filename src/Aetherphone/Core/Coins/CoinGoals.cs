using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Coins;

internal static class CoinGoals
{
    public static void Count(CoinRuleStatusDto[] rules, bool weekly, out int done, out int total)
    {
        done = 0;
        total = 0;
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            if (rule.Weekly != weekly || rule.PeriodCap <= 0)
            {
                continue;
            }

            total++;
            if (IsComplete(rule))
            {
                done++;
            }
        }
    }

    public static bool IsComplete(CoinRuleStatusDto rule) =>
        rule.PeriodCap > 0 && rule.EarnedThisPeriod >= rule.PeriodCap;
}
