namespace Aetherphone.Core.Casino;

internal static class CasinoWire
{
    public const string SlotsKind = "casino.slots";

    public const string ScratchKind = "casino.scratch";

    public const string BartenderKind = "casino.bartender";

    private const string KindPrefix = "casino.";

    public static string Kind(string gameId)
    {
        return string.Concat(KindPrefix, gameId);
    }
}
