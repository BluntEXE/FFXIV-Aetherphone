using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Coins;

internal sealed class CoinGameSessionTracker : IDisposable
{
    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly StoreWork work = new("CoinGames");

    private volatile string? openSessionId;
    private volatile string? openGameId;
    private CoinAwardDto? lastAward;
    private volatile string? lastAwardGameId;
    private int transitioning;

    public CoinGameSessionTracker(AethernetSession session, CoinsClient coins)
    {
        this.session = session;
        this.coins = coins;
    }

    public string? OpenGameId => openGameId;

    public CoinAwardDto? TakeAward(out string? gameId)
    {
        gameId = lastAwardGameId;
        var award = Interlocked.Exchange(ref lastAward, null);
        if (award is null)
        {
            gameId = null;
        }

        return award;
    }

    public void GameOpened(string gameId)
    {
        if (!session.IsSignedIn || Interlocked.Exchange(ref transitioning, 1) != 0)
        {
            return;
        }

        openGameId = gameId;
        work.Run("session start", async token =>
        {
            var issued = await coins.StartGameSessionAsync(gameId, token).ConfigureAwait(false);
            if (issued is not null && issued.SessionId.Length > 0)
            {
                openSessionId = issued.SessionId;
            }
        }, () => Interlocked.Exchange(ref transitioning, 0));
    }

    public void GameClosed()
    {
        var sessionId = openSessionId;
        var gameId = openGameId;
        openSessionId = null;
        openGameId = null;
        if (sessionId is null || !session.IsSignedIn)
        {
            return;
        }

        work.Run("session end", async token =>
        {
            var award = await coins.EndGameSessionAsync(sessionId, token).ConfigureAwait(false);
            if (award is null)
            {
                return;
            }

            lastAwardGameId = gameId;
            Interlocked.Exchange(ref lastAward, award);
        });
    }

    public void Dispose()
    {
        GameClosed();
        work.Dispose();
    }
}
