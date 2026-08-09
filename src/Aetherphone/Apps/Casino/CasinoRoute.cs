namespace Aetherphone.Apps.Casino;

internal enum CasinoScreen
{
    Floor,
    Cabinet,
    Table,
    History,
    Fairness,
    Limits,
    RoundDetail,
    DailySpin,
}

internal readonly record struct CasinoRoute(CasinoScreen Screen, string GameId = "", string RoundId = "")
{
    public static readonly CasinoRoute Floor = new(CasinoScreen.Floor);
}
