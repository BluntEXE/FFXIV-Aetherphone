using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class CasinoClient
{
    internal const string StatePath = "/casino";
    internal const string OpenSittingPath = "/casino/sittings";
    internal const string TopUpPath = "/casino/sittings/topup";
    internal const string CloseSittingPath = "/casino/sittings/close";
    internal const string LimitsPath = "/casino/limits";

    internal const int SoloTableKind = 0;

    private readonly AethernetTransport net;

    public CasinoClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CasinoStateDto?> GetStateAsync(CancellationToken token)
    {
        return net.GetAsync(StatePath, AethernetJsonContext.Default.CasinoStateDto, token);
    }

    public Task<CasinoSittingResultDto?> OpenSittingAsync(string clientSittingId, string clientActionId,
        string gameKind, long amount, CancellationToken token)
    {
        return net.PostAsync(OpenSittingPath,
            new CasinoOpenSittingRequest(clientSittingId, clientActionId, gameKind, SoloTableKind, amount),
            AethernetJsonContext.Default.CasinoOpenSittingRequest,
            AethernetJsonContext.Default.CasinoSittingResultDto, token);
    }

    public Task<CasinoSittingResultDto?> TopUpAsync(string sittingId, string clientActionId, long amount,
        CancellationToken token)
    {
        return net.PostAsync(TopUpPath, new CasinoTopUpRequest(sittingId, clientActionId, amount),
            AethernetJsonContext.Default.CasinoTopUpRequest,
            AethernetJsonContext.Default.CasinoSittingResultDto, token);
    }

    public Task<CasinoSittingResultDto?> CloseSittingAsync(string sittingId, CancellationToken token)
    {
        return net.PostAsync(CloseSittingPath, new CasinoCloseSittingRequest(sittingId),
            AethernetJsonContext.Default.CasinoCloseSittingRequest,
            AethernetJsonContext.Default.CasinoSittingResultDto, token);
    }

    public Task<CasinoLimitsDto?> SetLimitsAsync(long? selfLossLimit, CancellationToken token)
    {
        return net.PostAsync(LimitsPath, new CasinoLimitRequest(selfLossLimit),
            AethernetJsonContext.Default.CasinoLimitRequest,
            AethernetJsonContext.Default.CasinoLimitsDto, token);
    }
}
