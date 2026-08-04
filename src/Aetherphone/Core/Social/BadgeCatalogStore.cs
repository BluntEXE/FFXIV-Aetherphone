using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;

namespace Aetherphone.Core.Social;

internal sealed class BadgeCatalogStore : IDisposable
{
    private const long RefreshAfterMilliseconds = 600_000;

    private readonly AethernetSession session;
    private readonly AccountClient account;
    private readonly StoreWork work = new("BadgeCatalog");

    private volatile Dictionary<string, BadgeStyle> stylesById = new(StringComparer.Ordinal);
    private long fetchedAtTick;
    private int fetching;

    public BadgeCatalogStore(AethernetSession session, AccountClient account)
    {
        this.session = session;
        this.account = account;
    }

    public BadgeStyle? Find(string badgeId)
    {
        EnsureFresh();
        return stylesById.TryGetValue(badgeId, out var style) ? style : null;
    }

    public void EnsureFresh()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lastFetch = Interlocked.Read(ref fetchedAtTick);
        if (lastFetch != 0 && now - lastFetch < RefreshAfterMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetching, 1) != 0)
        {
            return;
        }

        work.Run("badge catalog refresh", async token =>
        {
            var catalog = await account.BadgeCatalogAsync(token).ConfigureAwait(false);
            if (catalog is null)
            {
                return;
            }

            var next = new Dictionary<string, BadgeStyle>(catalog.Badges.Length, StringComparer.Ordinal);
            for (var index = 0; index < catalog.Badges.Length; index++)
            {
                var style = BadgeStyle.From(catalog.Badges[index]);
                next[style.Id] = style;
            }

            stylesById = next;
            Interlocked.Exchange(ref fetchedAtTick, Environment.TickCount64);
        }, () => Interlocked.Exchange(ref fetching, 0));
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
