namespace Aetherphone.Core.Casino;

internal static class CasinoWire
{
    public const string SlotsKind = "casino.slots";

    public const string ScratchKind = "casino.scratch";

    public const string BartenderKind = "casino.bartender";

    public const string WheelKind = "casino.wheel";

    public const string BingoKind = "casino.bingo";

    public const string BlackjackKind = "casino.blackjack";

    public const string BlackjackHandEvent = "casino.blackjack.hand";

    public const string DailySpinKind = "casino.dailyspin";

    private const string KindPrefix = "casino.";

    public static string Kind(string gameId)
    {
        return string.Concat(KindPrefix, gameId);
    }
}
