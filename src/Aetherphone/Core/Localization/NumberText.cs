using System.Collections.Concurrent;

namespace Aetherphone.Core.Localization;

internal static class NumberText
{
    private const int CacheLimit = 512;

    private static readonly ConcurrentDictionary<long, string> GroupCache = new();
    private static LanguageInfo? cachedLanguage;

    public static string Group(long value)
    {
        if (!ReferenceEquals(Loc.Current, cachedLanguage))
        {
            cachedLanguage = Loc.Current;
            GroupCache.Clear();
        }

        if (GroupCache.TryGetValue(value, out var cached))
        {
            return cached;
        }

        if (GroupCache.Count >= CacheLimit)
        {
            GroupCache.Clear();
        }

        var text = value.ToString("N0", Loc.Culture);
        GroupCache[value] = text;
        return text;
    }

    public static string Compact(long value)
    {
        if (value >= 1_000_000)
        {
            var millions = value / 1_000_000f;
            return millions.ToString(millions >= 10f ? "0" : "0.#", Loc.Culture) + "M";
        }

        if (value >= 1_000)
        {
            var thousands = value / 1_000f;
            return thousands.ToString(thousands >= 10f ? "0" : "0.#", Loc.Culture) + "K";
        }

        return value.ToString(Loc.Culture);
    }
}
