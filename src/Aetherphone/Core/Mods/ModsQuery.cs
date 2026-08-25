namespace Aetherphone.Core.Mods;

internal enum ModsSort : byte
{
    Trending,
    Popular,
    Newest,
    Updated,
}

internal readonly record struct ModsFilter(bool Nsfw, bool Nsfl, bool ContentWarnings, bool Paid)
{
    public static readonly ModsFilter Default = new(false, false, true, true);

    public FilterInfo ToInfo() => new()
    {
        Nsfw = Nsfw,
        Nsfl = Nsfl,
        Cw = ContentWarnings,
        Paid = Paid,
    };
}

internal readonly record struct ModsQuery(string Text, string Category, ModsSort Sort, ModsFilter Filter)
{
    public static readonly ModsQuery Default = new(string.Empty, string.Empty, ModsSort.Trending, ModsFilter.Default);

    public const int SortCount = 4;

    public static string Order(ModsSort sort) => sort switch
    {
        ModsSort.Popular => "DOWNLOADS",
        ModsSort.Newest => "CREATED_AT",
        ModsSort.Updated => "UPDATED_AT",
        _ => "DOWNLOADS_LAST_MONTH",
    };

    public SearchInfo ToInfo()
    {
        var trimmed = Text.Trim();
        return new SearchInfo
        {
            Name = trimmed.Length == 0 ? null : trimmed,
            IncludeTags = Category.Length == 0 ? Array.Empty<string>() : new[] { Category },
            Order = Order(Sort),
        };
    }
}
