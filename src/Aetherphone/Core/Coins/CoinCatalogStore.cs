using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Coins;

internal sealed class CoinCatalogStore : IDisposable
{
    private const long RefreshAfterMilliseconds = 60_000;
    private const long RefreshOnEnterMilliseconds = 5_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly StoreWork work = new("CoinCatalog");

    private volatile CoinSkuStyle[] skus = Array.Empty<CoinSkuStyle>();
    private volatile bool loadedOnce;
    private long loadedAtTick;
    private long attemptedAtTick;
    private int fetching;
    private string? lastAccountId;

    public CoinCatalogStore(AethernetSession session, CoinsClient coins)
    {
        this.session = session;
        this.coins = coins;
        session.Changed += OnSessionChanged;
    }

    public CoinSkuStyle[] Skus => skus;

    public bool LoadedOnce => loadedOnce;

    public bool Fetching => Volatile.Read(ref fetching) != 0;

    public void EnsureFresh()
    {
        Refresh(RefreshAfterMilliseconds);
    }

    public void RefreshOnEnter()
    {
        Refresh(RefreshOnEnterMilliseconds);
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref loadedAtTick, 0);
        Interlocked.Exchange(ref attemptedAtTick, 0);
        Refresh(0);
    }

    private void Refresh(long refreshAfterMilliseconds)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lastAttempt = Interlocked.Read(ref attemptedAtTick);
        if (lastAttempt != 0 && now - lastAttempt < RetryAfterAttemptMilliseconds)
        {
            return;
        }

        var lastLoad = Interlocked.Read(ref loadedAtTick);
        if (lastLoad != 0 && now - lastLoad < refreshAfterMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetching, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref attemptedAtTick, now);
        work.Run("catalog refresh", async token =>
        {
            var catalog = await coins.CatalogAsync(token).ConfigureAwait(false);
            if (catalog is null)
            {
                return;
            }

            var next = new CoinSkuStyle[catalog.Skus.Length];
            for (var index = 0; index < catalog.Skus.Length; index++)
            {
                next[index] = CoinSkuStyle.From(catalog.Skus[index]);
            }

            skus = next;
            loadedOnce = true;
            Interlocked.Exchange(ref loadedAtTick, Environment.TickCount64);
            Interlocked.Exchange(ref attemptedAtTick, 0);
        }, () => Interlocked.Exchange(ref fetching, 0));
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        skus = Array.Empty<CoinSkuStyle>();
        loadedOnce = false;
        Interlocked.Exchange(ref loadedAtTick, 0);
        Interlocked.Exchange(ref attemptedAtTick, 0);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
