using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;

namespace Aetherphone.Core.Casino;

// The daily spin is the only casino surface with no sitting, no chips and no room, so it keeps its
// own tiny store rather than renting space in one built for tables. It mints coins, which means
// the claim is a money path: one claim in flight at a time, one client round id per attempt so a
// lost response is retried as the same spin rather than a second one, and the wallet is told the
// new balance the moment the server names it so the coin surfaces move without waiting a minute
// for the next poll.
internal sealed class CasinoSpinStore : IDisposable
{
    private const long StateRefreshMilliseconds = 120_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CoinStore coins;
    private readonly StoreWork work = new("CasinoSpin");

    private volatile CasinoDailySpinStateDto? state;
    private volatile bool loaded;
    private volatile bool claiming;
    private CasinoDailySpinDto? claimResult;
    private string unansweredClaimId = string.Empty;
    private long stateLoadedAtTick;
    private long stateAttemptedAtTick;
    private int fetchingState;
    private int claimFailed;
    private string? lastAccountId;

    public CasinoSpinStore(AethernetSession session, CasinoClient casino, CoinStore coins)
    {
        this.session = session;
        this.casino = casino;
        this.coins = coins;
        session.Changed += OnSessionChanged;
    }

    public CasinoDailySpinStateDto? State => state;

    public bool Loaded => loaded;

    public bool Claiming => claiming;

    public CasinoDailySpinDto? TakeClaimResult()
    {
        return Interlocked.Exchange(ref claimResult, null);
    }

    public bool TakeClaimFailure()
    {
        return Interlocked.Exchange(ref claimFailed, 0) != 0;
    }

    public void EnsureFresh()
    {
        Refresh(StateRefreshMilliseconds);
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref stateLoadedAtTick, 0);
        Interlocked.Exchange(ref stateAttemptedAtTick, 0);
        Refresh(0);
    }

    public void Claim()
    {
        if (claiming || !session.IsSignedIn || !DailySpinStatus.CanClaim(loaded, state, false))
        {
            return;
        }

        var clientRoundId = unansweredClaimId.Length > 0 ? unansweredClaimId : Guid.NewGuid().ToString("N");
        unansweredClaimId = clientRoundId;
        claiming = true;
        work.Run("daily spin", async token =>
        {
            var result = await casino.ClaimDailySpinAsync(clientRoundId, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref claimFailed, 1);
                return;
            }

            unansweredClaimId = string.Empty;
            state = Settled(result);
            Interlocked.Exchange(ref claimResult, result);
            loaded = true;
            Interlocked.Exchange(ref stateLoadedAtTick, Environment.TickCount64);
            if (result.Granted && result.Balance > 0)
            {
                coins.AbsorbLocalAward(result.Balance);
            }
        }, () => claiming = false);
    }

    // A granted spin already told the client everything the state endpoint would: what it landed
    // on, what it paid, and when the next one opens. Folding it into the held state means the card
    // reads "claimed" the instant the wheel stops rather than at the next poll, and a refusal is
    // folded the same way so the reason it named is the reason the card shows.
    internal static CasinoDailySpinStateDto Settled(CasinoDailySpinDto result)
    {
        if (!result.Granted)
        {
            return new CasinoDailySpinStateDto(
                Available: false,
                Reason: result.Reason.Length > 0 ? result.Reason : CasinoReasons.Claimed,
                NextClaimAtUnix: result.NextClaimAtUnix,
                Balance: result.Balance);
        }

        return new CasinoDailySpinStateDto(
            Available: false,
            Reason: CasinoReasons.Claimed,
            NextClaimAtUnix: result.NextClaimAtUnix,
            Segment: result.Segment,
            Amount: result.Amount,
            RoundId: result.RoundId,
            Balance: result.Balance);
    }

    internal static bool CoolingDown(long attemptedAtTick, long nowTick)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < RetryAfterAttemptMilliseconds;
    }

    private void Refresh(long refreshAfterMilliseconds)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (CoolingDown(Interlocked.Read(ref stateAttemptedAtTick), now))
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
        work.Run("daily spin state", async token =>
        {
            var fresh = await casino.DailySpinStateAsync(token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            Interlocked.Exchange(ref stateAttemptedAtTick, 0);
            Interlocked.Exchange(ref stateLoadedAtTick, Environment.TickCount64);
            state = fresh;
            loaded = true;
        }, () => Interlocked.Exchange(ref fetchingState, 0));
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        state = null;
        loaded = false;
        unansweredClaimId = string.Empty;
        Interlocked.Exchange(ref claimResult, null);
        Interlocked.Exchange(ref claimFailed, 0);
        Interlocked.Exchange(ref stateLoadedAtTick, 0);
        Interlocked.Exchange(ref stateAttemptedAtTick, 0);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
