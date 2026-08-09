using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

// The round lifecycle lives apart from CasinoStore on purpose: CasinoStore is the hardened
// money-motion path (buy-in, top-up, cash-out) while this store owns game rounds. The client
// mints every round id, persists the intent per character before the POST leaves, and clears
// it only once a response lands, so a crash mid-round recovers by re-issuing the idempotent
// POST and reconciling from the byte-identical replay.
internal sealed class CasinoPlayStore : IDisposable
{
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore store;
    private readonly StoreWork work = new("CasinoPlay");
    private readonly object pendingRoundsSwapLock = new();

    private volatile bool roundInFlight;
    private CasinoSlotsSpinDto? spinResult;
    private int roundFailed;

    public CasinoPlayStore(Configuration configuration, AethernetSession session, CasinoClient casino,
        CasinoStore store)
    {
        this.configuration = configuration;
        this.session = session;
        this.casino = casino;
        this.store = store;
    }

    public bool RoundInFlight => roundInFlight;

    public PendingCasinoRound? PendingRound
    {
        get
        {
            var contentId = session.ActiveContentId;
            if (contentId == 0)
            {
                return null;
            }

            var snapshot = configuration.PendingCasinoRounds;
            return snapshot.TryGetValue(contentId, out var pending) ? pending : null;
        }
    }

    public CasinoSlotsSpinDto? TakeSpinResult()
    {
        return Interlocked.Exchange(ref spinResult, null);
    }

    public bool TakeRoundFailure()
    {
        return Interlocked.Exchange(ref roundFailed, 0) != 0;
    }

    public void SpinSlots(long stake)
    {
        var sittingId = store.State?.Sitting?.Id ?? string.Empty;
        if (roundInFlight || !session.IsSignedIn || sittingId.Length == 0 || !SlotsRules.IsStakeTier(stake))
        {
            return;
        }

        roundInFlight = true;
        var roundId = Guid.NewGuid().ToString("N");
        RememberPendingRound(new PendingCasinoRound
        {
            GameKind = CasinoWire.SlotsKind,
            SittingId = sittingId,
            RoundId = roundId,
            Stake = stake,
        });
        IssueSpin(sittingId, roundId, stake);
    }

    public void RecoverPendingRound()
    {
        if (roundInFlight || !session.IsSignedIn)
        {
            return;
        }

        var pending = PendingRound;
        if (pending is null || !string.Equals(pending.GameKind, CasinoWire.SlotsKind, StringComparison.Ordinal))
        {
            return;
        }

        roundInFlight = true;
        IssueSpin(pending.SittingId, pending.RoundId, pending.Stake);
    }

    private void IssueSpin(string sittingId, string roundId, long stake)
    {
        work.Run("slots spin", async token =>
        {
            var result = await casino.SpinSlotsAsync(sittingId, roundId, stake, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref roundFailed, 1);
                return;
            }

            ClearPendingRound(roundId);
            Interlocked.Exchange(ref spinResult, result);
            if (result.Granted)
            {
                store.AbsorbStack(result.Stack);
            }
            else
            {
                store.RefreshNow();
            }
        }, () => roundInFlight = false);
    }

    internal static Dictionary<ulong, PendingCasinoRound>? RememberRound(
        Dictionary<ulong, PendingCasinoRound> snapshot, ulong contentId, PendingCasinoRound round)
    {
        if (snapshot.TryGetValue(contentId, out var known)
            && string.Equals(known.RoundId, round.RoundId, StringComparison.Ordinal))
        {
            return null;
        }

        var next = new Dictionary<ulong, PendingCasinoRound>(snapshot);
        next[contentId] = round;
        return next;
    }

    internal static Dictionary<ulong, PendingCasinoRound>? ClearRound(
        Dictionary<ulong, PendingCasinoRound> snapshot, ulong contentId, string roundId)
    {
        if (!snapshot.TryGetValue(contentId, out var known)
            || !string.Equals(known.RoundId, roundId, StringComparison.Ordinal))
        {
            return null;
        }

        var next = new Dictionary<ulong, PendingCasinoRound>(snapshot);
        next.Remove(contentId);
        return next;
    }

    private void RememberPendingRound(PendingCasinoRound round)
    {
        var contentId = session.ActiveContentId;
        if (contentId == 0)
        {
            return;
        }

        lock (pendingRoundsSwapLock)
        {
            var next = RememberRound(configuration.PendingCasinoRounds, contentId, round);
            if (next is null)
            {
                return;
            }

            configuration.PendingCasinoRounds = next;
        }

        configuration.Save();
    }

    private void ClearPendingRound(string roundId)
    {
        var contentId = session.ActiveContentId;
        if (contentId == 0)
        {
            return;
        }

        lock (pendingRoundsSwapLock)
        {
            var next = ClearRound(configuration.PendingCasinoRounds, contentId, roundId);
            if (next is null)
            {
                return;
            }

            configuration.PendingCasinoRounds = next;
        }

        configuration.Save();
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
