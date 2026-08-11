using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class CoinsClient
{
    private readonly AethernetTransport net;

    public CoinsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CoinWalletDto?> WalletAsync(CancellationToken token)
    {
        return net.GetAsync("/coins/", AethernetJsonContext.Default.CoinWalletDto, token);
    }

    public Task<CoinLedgerPage?> LedgerAsync(string? cursor, CancellationToken token)
    {
        var path = "/coins/ledger";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.CoinLedgerPage, token);
    }

    public Task<CoinCatalogDto?> CatalogAsync(CancellationToken token)
    {
        return net.GetAsync("/coins/catalog", AethernetJsonContext.Default.CoinCatalogDto, token);
    }

    public Task<CoinAwardDto?> CheckInAsync(CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, "/coins/checkin", AethernetJsonContext.Default.CoinAwardDto, token);
    }

    public Task<CoinGameSessionDto?> StartGameSessionAsync(string gameId, CancellationToken token)
    {
        return net.PostAsync("/coins/games/session", new CoinGameSessionRequest(gameId),
            AethernetJsonContext.Default.CoinGameSessionRequest, AethernetJsonContext.Default.CoinGameSessionDto, token);
    }

    public Task<CoinAwardDto?> EndGameSessionAsync(string sessionId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, $"/coins/games/session/{Uri.EscapeDataString(sessionId)}/end",
            AethernetJsonContext.Default.CoinAwardDto, token);
    }

    public Task<CoinPurchaseResult?> PurchaseAsync(string skuId, long expectedPrice, CancellationToken token)
    {
        return net.PostAsync("/coins/purchase", new CoinPurchaseRequest(skuId, expectedPrice),
            AethernetJsonContext.Default.CoinPurchaseRequest, AethernetJsonContext.Default.CoinPurchaseResult, token);
    }
}
