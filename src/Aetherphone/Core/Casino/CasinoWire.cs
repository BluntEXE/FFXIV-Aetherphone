namespace Aetherphone.Core.Casino;

internal static class CasinoWire
{
    private const string KindPrefix = "casino.";

    public static string Kind(string gameId)
    {
        return string.Concat(KindPrefix, gameId);
    }
}
