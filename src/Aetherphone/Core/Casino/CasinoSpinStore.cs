using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;

namespace Aetherphone.Core.Casino;

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
