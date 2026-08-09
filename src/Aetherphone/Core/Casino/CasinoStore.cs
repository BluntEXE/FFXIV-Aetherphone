using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoStore : IDisposable
{
    private const long StateRefreshMilliseconds = 60_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CoinStore coins;
    private readonly StoreWork work = new("Casino");

    private volatile CasinoStateDto? state;
    private volatile bool openingSitting;
    private volatile bool toppingUp;
    private volatile bool closingSitting;
    private volatile bool savingLimits;
    private CasinoSittingDto? sittingResult;
    private CasinoSittingDto? closeResult;
    private CasinoStateDto? limitsResult;
    private long stateLoadedAtTick;
    private long stateAttemptedAtTick;
    private int fetchingState;
    private string? lastAccountId;

    public CasinoStore(Configuration configuration, AethernetSession session, CasinoClient casino, CoinStore coins)
    {
        this.configuration = configuration;
        this.session = session;
        this.casino = casino;
        this.coins = coins;
        session.Changed += OnSessionChanged;
    }

    public CasinoStateDto? State => state;

    public bool OpeningSitting => openingSitting;

    public bool ToppingUp => toppingUp;

    public bool ClosingSitting => closingSitting;

    public bool SavingLimits => savingLimits;

    public bool MovingMoney => openingSitting || toppingUp || closingSitting;

    public string PendingSittingId
    {
        get
        {
            var contentId = session.ActiveContentId;
            if (contentId == 0)
            {
                return string.Empty;
            }

            return configuration.PendingCasinoSittings.TryGetValue(contentId, out var sittingId)
                ? sittingId
                : string.Empty;
        }
    }

    public void EnsureFresh()
    {
        RefreshState(StateRefreshMilliseconds);
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref stateLoadedAtTick, 0);
        Interlocked.Exchange(ref stateAttemptedAtTick, 0);
        RefreshState(0);
    }

    public CasinoSittingDto? TakeSittingResult()
    {
        return Interlocked.Exchange(ref sittingResult, null);
    }

    public CasinoSittingDto? TakeCloseResult()
    {
        return Interlocked.Exchange(ref closeResult, null);
    }

    public CasinoStateDto? TakeLimitsResult()
    {
        return Interlocked.Exchange(ref limitsResult, null);
    }

    public void OpenSitting(string gameKind, long amount)
    {
        if (MovingMoney || !session.IsSignedIn || amount <= 0 || gameKind.Length == 0)
        {
            return;
        }

        openingSitting = true;
        var clientSittingId = Guid.NewGuid().ToString("N");
        RememberPendingSitting(clientSittingId);
        work.Run("open sitting", async token =>
        {
            var result = await casino.OpenSittingAsync(clientSittingId, gameKind, amount, token)
                .ConfigureAwait(false);
            Interlocked.Exchange(ref sittingResult, result);
            AbsorbSitting(result);
        }, () => openingSitting = false);
    }

    public void TopUp(long amount)
    {
        var sittingId = state?.SittingId ?? string.Empty;
        if (MovingMoney || !session.IsSignedIn || amount <= 0 || sittingId.Length == 0)
        {
            return;
        }

        toppingUp = true;
        var actionId = Guid.NewGuid().ToString("N");
        work.Run("top up", async token =>
        {
            var result = await casino.TopUpAsync(sittingId, actionId, amount, token).ConfigureAwait(false);
            Interlocked.Exchange(ref sittingResult, result);
            AbsorbSitting(result);
        }, () => toppingUp = false);
    }

    public void CloseSitting()
    {
        var sittingId = state?.SittingId ?? string.Empty;
        if (sittingId.Length == 0)
        {
            sittingId = PendingSittingId;
        }

        if (MovingMoney || !session.IsSignedIn || sittingId.Length == 0)
        {
            return;
        }

        closingSitting = true;
        var actionId = Guid.NewGuid().ToString("N");
        work.Run("close sitting", async token =>
        {
            var result = await casino.CloseSittingAsync(sittingId, actionId, token).ConfigureAwait(false);
            Interlocked.Exchange(ref closeResult, result);
            AbsorbSitting(result);
        }, () => closingSitting = false);
    }

    public void SetLimits(long selfLossLimit)
    {
        if (savingLimits || !session.IsSignedIn)
        {
            return;
        }

        savingLimits = true;
        work.Run("set limits", async token =>
        {
            var result = await casino.SetLimitsAsync(selfLossLimit, token).ConfigureAwait(false);
            Interlocked.Exchange(ref limitsResult, result);
            if (result is null || result.Reason.Length > 0)
            {
                return;
            }

            state = result;
            Interlocked.Exchange(ref stateLoadedAtTick, Environment.TickCount64);
            ReconcilePendingSitting(result);
        }, () => savingLimits = false);
    }

    private void AbsorbSitting(CasinoSittingDto? result)
    {
        if (result is null)
        {
            return;
        }

        coins.AbsorbLocalAward(result.Balance);
        RefreshNow();
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (!string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            lastAccountId = accountId;
            state = null;
            Interlocked.Exchange(ref sittingResult, null);
            Interlocked.Exchange(ref closeResult, null);
            Interlocked.Exchange(ref limitsResult, null);
            Interlocked.Exchange(ref stateLoadedAtTick, 0);
            Interlocked.Exchange(ref stateAttemptedAtTick, 0);
            if (session.IsSignedIn)
            {
                RefreshState(0);
            }

            return;
        }

        if (session.IsSignedIn && PendingSittingId.Length > 0 && state is null)
        {
            RefreshState(0);
        }
    }

    private void RefreshState(long refreshAfterMilliseconds)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        var lastAttempt = Interlocked.Read(ref stateAttemptedAtTick);
        if (lastAttempt != 0 && now - lastAttempt < RetryAfterAttemptMilliseconds)
        {
            return;
        }

        var lastLoad = Interlocked.Read(ref stateLoadedAtTick);
        if (lastLoad != 0 && now - lastLoad < refreshAfterMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetchingState, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref stateAttemptedAtTick, now);
        work.Run("state refresh", async token =>
        {
            var fresh = await casino.GetStateAsync(token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            state = fresh;
            Interlocked.Exchange(ref stateLoadedAtTick, Environment.TickCount64);
            ReconcilePendingSitting(fresh);
        }, () => Interlocked.Exchange(ref fetchingState, 0));
    }

    private void ReconcilePendingSitting(CasinoStateDto fresh)
    {
        if (openingSitting)
        {
            return;
        }

        if (fresh.SittingId.Length > 0)
        {
            RememberPendingSitting(fresh.SittingId);
        }
        else
        {
            ClearPendingSitting();
        }
    }

    private void RememberPendingSitting(string sittingId)
    {
        var contentId = session.ActiveContentId;
        if (contentId == 0)
        {
            return;
        }

        if (configuration.PendingCasinoSittings.TryGetValue(contentId, out var known) &&
            string.Equals(known, sittingId, StringComparison.Ordinal))
        {
            return;
        }

        configuration.PendingCasinoSittings[contentId] = sittingId;
        configuration.Save();
    }

    private void ClearPendingSitting()
    {
        var contentId = session.ActiveContentId;
        if (contentId == 0 || !configuration.PendingCasinoSittings.Remove(contentId))
        {
            return;
        }

        configuration.Save();
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
