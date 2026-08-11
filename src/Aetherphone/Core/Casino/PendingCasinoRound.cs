namespace Aetherphone.Core.Casino;

[Serializable]
internal sealed class PendingCasinoRound
{
    public string GameKind { get; set; } = string.Empty;

    public string SittingId { get; set; } = string.Empty;

    public string RoundId { get; set; } = string.Empty;

    public long Stake { get; set; }
}
