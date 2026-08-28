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
}
