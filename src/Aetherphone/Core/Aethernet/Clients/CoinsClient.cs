using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class CoinsClient
{
    private readonly AethernetTransport net;

    public CoinsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CoinWalletDto?> WalletAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/coins/", AethernetJsonContext.Default.CoinWalletDto, token, null, onFailure);
    }

    public Task<CoinLedgerPage?> LedgerAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        var path = "/coins/ledger";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.CoinLedgerPage, token, null, onFailure);
    }

    public Task<CoinCatalogDto?> CatalogAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/coins/catalog", AethernetJsonContext.Default.CoinCatalogDto, token, null, onFailure);
    }

    public Task<CoinAwardDto?> CheckInAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, "/coins/checkin", AethernetJsonContext.Default.CoinAwardDto, token,
            null, onFailure);
    }

    public Task<CoinGameSessionDto?> StartGameSessionAsync(string gameId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/coins/games/session", new CoinGameSessionRequest(gameId),
            AethernetJsonContext.Default.CoinGameSessionRequest, AethernetJsonContext.Default.CoinGameSessionDto, token,
            null, onFailure);
    }

    public Task<CoinAwardDto?> EndGameSessionAsync(string sessionId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Post, $"/coins/games/session/{Uri.EscapeDataString(sessionId)}/end",
            AethernetJsonContext.Default.CoinAwardDto, token, null, onFailure);
    }

    public Task<CoinPurchaseResult?> PurchaseAsync(string skuId, long expectedPrice, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/coins/purchase", new CoinPurchaseRequest(skuId, expectedPrice),
            AethernetJsonContext.Default.CoinPurchaseRequest, AethernetJsonContext.Default.CoinPurchaseResult, token,
            null, onFailure);
    }
}
