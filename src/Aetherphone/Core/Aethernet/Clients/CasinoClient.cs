using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class CasinoClient
{
    private readonly AethernetTransport net;

    public CasinoClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CasinoStateDto?> GetStateAsync(CancellationToken token)
    {
        return net.GetAsync("/casino/me", AethernetJsonContext.Default.CasinoStateDto, token);
    }

    public Task<CasinoSittingDto?> OpenSittingAsync(string clientSittingId, string gameKind, long amount,
        CancellationToken token)
    {
        return net.PostAsync("/casino/sittings", new CasinoOpenSittingRequest(clientSittingId, gameKind, amount),
            AethernetJsonContext.Default.CasinoOpenSittingRequest, AethernetJsonContext.Default.CasinoSittingDto,
            token);
    }

    public Task<CasinoSittingDto?> TopUpAsync(string sittingId, string actionId, long amount, CancellationToken token)
    {
        return net.PostAsync($"/casino/sittings/{Uri.EscapeDataString(sittingId)}/topup",
            new CasinoTopUpRequest(actionId, amount), AethernetJsonContext.Default.CasinoTopUpRequest,
            AethernetJsonContext.Default.CasinoSittingDto, token);
    }

    public Task<CasinoSittingDto?> CloseSittingAsync(string sittingId, string actionId, CancellationToken token)
    {
        return net.PostAsync($"/casino/sittings/{Uri.EscapeDataString(sittingId)}/cashout",
            new CasinoCloseSittingRequest(actionId), AethernetJsonContext.Default.CasinoCloseSittingRequest,
            AethernetJsonContext.Default.CasinoSittingDto, token);
    }

    public Task<CasinoStateDto?> SetLimitsAsync(long selfLossLimit, CancellationToken token)
    {
        return net.PostAsync("/casino/limits", new CasinoLimitsRequest(selfLossLimit),
            AethernetJsonContext.Default.CasinoLimitsRequest, AethernetJsonContext.Default.CasinoStateDto, token);
    }
}
