namespace Aetherphone.Apps.Casino;

internal enum CasinoScreen
{
    Floor,
    Cabinet,
    Tables,
    Table,
    TableDoor,
    History,
    Fairness,
    Limits,
    RoundDetail,
    DailySpin,
}

internal readonly record struct CasinoRoute(
    CasinoScreen Screen,
    string GameId = "",
    string RoundId = "",
    string TableId = "")
{
    public static readonly CasinoRoute Floor = new(CasinoScreen.Floor);
}
