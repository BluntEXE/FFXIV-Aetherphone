namespace Aetherphone.Apps.Coin;

internal enum CoinScreen
{
    Root,
    ShopFolder,
    ShopShelf,
}

internal readonly record struct CoinRoute(CoinScreen Screen, string CategoryId)
{
    public static readonly CoinRoute Root = new(CoinScreen.Root, string.Empty);

    public static CoinRoute Folder(string categoryId) => new(CoinScreen.ShopFolder, categoryId);

    public static CoinRoute Shelf(string categoryId) => new(CoinScreen.ShopShelf, categoryId);
}
