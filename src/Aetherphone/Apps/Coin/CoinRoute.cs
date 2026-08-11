namespace Aetherphone.Apps.Coin;

internal enum CoinScreen
{
    Root,
}

internal readonly record struct CoinRoute(CoinScreen Screen)
{
    public static readonly CoinRoute Root = new(CoinScreen.Root);
}
