using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Coins;

internal sealed class CoinGameSessionTracker : IDisposable
{
    private const int UnloadEndTimeoutMilliseconds = 2000;

    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly StoreWork work = new("CoinGames");

    private volatile string? openSessionId;
    private volatile string? openGameId;
    private CoinAwardDto? lastAward;
    private volatile string? lastAwardGameId;
    private int transitioning;
    private int resolvingPending;
    private long openStartTicks;
    private volatile int openMinSeconds = 180;
    private volatile int openDeepSeconds = 900;

    public CoinGameSessionTracker(Configuration configuration, AethernetSession session, CoinsClient coins)
    {
        this.configuration = configuration;
        this.session = session;
        this.coins = coins;
        session.Changed += OnSessionChanged;
        OnSessionChanged();
    }

    public string? OpenGameId => openGameId;

    public int OpenMinSeconds => openMinSeconds;

    public int OpenDeepSeconds => openDeepSeconds;

    public int OpenSessionSeconds
    {
        get
        {
            if (openSessionId is null)
            {
                return -1;
            }

            var start = Volatile.Read(ref openStartTicks);
            return start == 0 ? -1 : (int)((Environment.TickCount64 - start) / 1000);
        }
    }

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
            await EndPendingAsync(token).ConfigureAwait(false);
            var issued = await coins.StartGameSessionAsync(gameId, token).ConfigureAwait(false);
            if (issued is not null && issued.SessionId.Length > 0)
            {
                var startTicks = Environment.TickCount64;
                if (issued.StartedAtUnix > 0)
                {
                    var alreadyElapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issued.StartedAtUnix;
                    if (alreadyElapsed > 0)
                    {
                        startTicks -= alreadyElapsed * 1000;
                    }
                }

                openMinSeconds = issued.MinSeconds > 0 ? issued.MinSeconds : openMinSeconds;
                openDeepSeconds = issued.DeepSeconds > 0 ? issued.DeepSeconds : openDeepSeconds;
                Volatile.Write(ref openStartTicks, startTicks);
                openSessionId = issued.SessionId;
                RememberPending(issued.SessionId);
            }
        }, () => Interlocked.Exchange(ref transitioning, 0));
    }

    public void GameClosed()
    {
        var gameId = openGameId;
        openSessionId = null;
        openGameId = null;
        Volatile.Write(ref openStartTicks, 0);
        if (!session.IsSignedIn || configuration.PendingCoinGameSession.Length == 0)
        {
            return;
        }

        lastAwardGameId = gameId;
        work.Run("session end", EndPendingAsync);
    }

    private void OnSessionChanged()
    {
        if (!session.IsSignedIn || configuration.PendingCoinGameSession.Length == 0 || openSessionId is not null)
        {
            return;
        }

        work.Run("session recover", EndPendingAsync);
    }

    private async Task EndPendingAsync(CancellationToken token)
    {
        if (Interlocked.Exchange(ref resolvingPending, 1) != 0)
        {
            return;
        }

        try
        {
            var pending = configuration.PendingCoinGameSession;
            if (pending.Length == 0)
            {
                return;
            }

            var award = await coins.EndGameSessionAsync(pending, token).ConfigureAwait(false);
            if (award is null)
            {
                return;
            }

            ClearPending(pending);
            if (award.Granted)
            {
                Interlocked.Exchange(ref lastAward, award);
            }
        }
        finally
        {
            Interlocked.Exchange(ref resolvingPending, 0);
        }
    }

    private void RememberPending(string sessionId)
    {
        configuration.PendingCoinGameSession = sessionId;
        configuration.Save();
    }

    private void ClearPending(string sessionId)
    {
        if (!string.Equals(configuration.PendingCoinGameSession, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        configuration.PendingCoinGameSession = string.Empty;
        configuration.Save();
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
        openSessionId = null;
        openGameId = null;
        var pending = configuration.PendingCoinGameSession;
        if (pending.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(UnloadEndTimeoutMilliseconds);
            var award = coins.EndGameSessionAsync(pending, timeout.Token).GetAwaiter().GetResult();
            if (award is not null)
            {
                ClearPending(pending);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[CoinGames] session end on unload failed");
        }
    }
}
