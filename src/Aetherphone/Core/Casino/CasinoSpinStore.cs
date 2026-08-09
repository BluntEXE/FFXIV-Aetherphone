using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;

namespace Aetherphone.Core.Casino;

// The daily spin is the only casino surface with no sitting, no chips and no room, so it keeps its
// own tiny store rather than renting space in one built for tables.
//
// There is no read route and there must not be one that this store invents: the single post either
// takes today's spin or replays the one already taken, so a speculative poll on app open would
// spend the player's turn for them. The store therefore never asks anything until the player taps,
// and until it holds an answer the wheel is offered rather than hidden. Being wrong that way costs
// one tap that comes back "already taken"; being wrong the other way hides a free spin all day.
//
// The answer is the whole state. A granted spin carries the segment, what reached the wallet and
// when the next one opens; a replay carries the same facts with Granted false, which is why the
// reason is read rather than inferred from the flag.
internal sealed class CasinoSpinStore : IDisposable
{
    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CoinStore coins;
    private readonly StoreWork work = new("CasinoSpin");

    private volatile CasinoDailySpinDto? answer;
    private volatile bool claiming;
    private CasinoDailySpinDto? claimResult;
    private int claimFailed;
    private string? lastAccountId;

    public CasinoSpinStore(AethernetSession session, CasinoClient casino, CoinStore coins)
    {
        this.session = session;
        this.casino = casino;
        this.coins = coins;
        session.Changed += OnSessionChanged;
    }

    public CasinoDailySpinDto? Answer => answer;

    public bool Claiming => claiming;

    public CasinoDailySpinDto? TakeClaimResult()
    {
        return Interlocked.Exchange(ref claimResult, null);
    }

    public bool TakeClaimFailure()
    {
        return Interlocked.Exchange(ref claimFailed, 0) != 0;
    }

    public void Claim()
    {
        if (claiming || !session.IsSignedIn || !DailySpinStatus.CanClaim(answer, false))
        {
            return;
        }

        claiming = true;
        work.Run("daily spin", async token =>
        {
            var result = await casino.ClaimDailySpinAsync(token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref claimFailed, 1);
                return;
            }

            answer = result;
            Interlocked.Exchange(ref claimResult, result);
            if (result.Granted && result.Balance > 0)
            {
                coins.AbsorbLocalAward(result.Balance);
            }
        }, () => claiming = false);
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        answer = null;
        Interlocked.Exchange(ref claimResult, null);
        Interlocked.Exchange(ref claimFailed, 0);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
