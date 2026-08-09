namespace Aetherphone.Apps.Casino;

internal enum CasinoScreen
{
    Floor,
    Cabinet,
    History,
    Fairness,
    Limits,
}

internal readonly record struct CasinoRoute(CasinoScreen Screen, string GameId = "")
{
    public static readonly CasinoRoute Floor = new(CasinoScreen.Floor);
}
