namespace Aetherphone.Apps.YellowPages;

internal enum YellowPagesScreen : byte
{
    Browse,
    Detail,
    Compose,
}

internal enum YellowPagesTab : byte
{
    Browse,
    Saved,
    Mine,
}

internal readonly record struct YellowPagesRoute(YellowPagesScreen Screen, string? AdId = null)
{
    public static readonly YellowPagesRoute Browse = new(YellowPagesScreen.Browse);
    public static readonly YellowPagesRoute Compose = new(YellowPagesScreen.Compose);

    public static YellowPagesRoute Detail(string adId) => new(YellowPagesScreen.Detail, adId);
}
