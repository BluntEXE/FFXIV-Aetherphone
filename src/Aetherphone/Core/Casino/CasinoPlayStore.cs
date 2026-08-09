using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

// The round lifecycle lives apart from CasinoStore on purpose: CasinoStore is the hardened
// money-motion path (buy-in, top-up, cash-out) while this store owns game rounds. The client
// mints every round id, persists the intent per character before the POST leaves, and clears
// it only once a response lands, so a crash mid-round recovers by re-issuing the idempotent
// POST and reconciling from the byte-identical replay. Barkeep is the one open-ended round:
// its intent survives the granted start and clears only when the finish settles, so a crash
// mid-shift can still recover the script and settle against the server clock.
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
    private CasinoScratchCardDto? scratchResult;
    private CasinoBarkeepStartDto? barkeepStartResult;
    private CasinoBarkeepFinishDto? barkeepFinishResult;
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

    public CasinoScratchCardDto? TakeScratchResult()
    {
        return Interlocked.Exchange(ref scratchResult, null);
    }

    public CasinoBarkeepStartDto? TakeBarkeepStart()
    {
        return Interlocked.Exchange(ref barkeepStartResult, null);
    }

    public CasinoBarkeepFinishDto? TakeBarkeepFinish()
    {
        return Interlocked.Exchange(ref barkeepFinishResult, null);
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

    public void BuyScratch(int tier)
    {
        var sittingId = store.State?.Sitting?.Id ?? string.Empty;
        if (roundInFlight || !session.IsSignedIn || sittingId.Length == 0 || !ScratchRules.IsValidTier(tier))
        {
            return;
        }

        roundInFlight = true;
        var roundId = Guid.NewGuid().ToString("N");
        RememberPendingRound(new PendingCasinoRound
        {
            GameKind = CasinoWire.ScratchKind,
            SittingId = sittingId,
            RoundId = roundId,
            Stake = ScratchRules.Prices[tier],
        });
        IssueScratch(sittingId, roundId, tier);
    }

    public void StartBarkeep()
    {
        var sittingId = store.State?.Sitting?.Id ?? string.Empty;
        if (roundInFlight || !session.IsSignedIn || sittingId.Length == 0)
        {
            return;
        }

        roundInFlight = true;
        var roundId = Guid.NewGuid().ToString("N");
        RememberPendingRound(new PendingCasinoRound
        {
            GameKind = CasinoWire.BartenderKind,
            SittingId = sittingId,
            RoundId = roundId,
            Stake = BarkeepRules.EntryChips,
        });
        IssueBarkeepStart(sittingId, roundId);
    }

    public void FinishBarkeep(string roundId, CasinoBarkeepOrderRequest[] orders)
    {
        if (roundInFlight || !session.IsSignedIn || roundId.Length == 0)
        {
            return;
        }

        roundInFlight = true;
        IssueBarkeepFinish(roundId, orders);
    }

    public void RecoverPendingRound()
    {
        if (roundInFlight || !session.IsSignedIn)
        {
            return;
        }

        var pending = PendingRound;
        if (pending is null)
        {
            return;
        }

        if (string.Equals(pending.GameKind, CasinoWire.SlotsKind, StringComparison.Ordinal))
        {
            roundInFlight = true;
            IssueSpin(pending.SittingId, pending.RoundId, pending.Stake);
            return;
        }

        if (string.Equals(pending.GameKind, CasinoWire.ScratchKind, StringComparison.Ordinal))
        {
            var tier = ScratchRules.TierForPrice(pending.Stake);
            if (tier < 0)
            {
                ClearPendingRound(pending.RoundId);
                return;
            }

            roundInFlight = true;
            IssueScratch(pending.SittingId, pending.RoundId, tier);
            return;
        }

        if (string.Equals(pending.GameKind, CasinoWire.BartenderKind, StringComparison.Ordinal))
        {
            roundInFlight = true;
            IssueBarkeepStart(pending.SittingId, pending.RoundId);
        }
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

    private void IssueScratch(string sittingId, string roundId, int tier)
    {
        work.Run("scratch buy", async token =>
        {
            var result = await casino.BuyScratchAsync(sittingId, roundId, tier, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref roundFailed, 1);
                return;
            }

            ClearPendingRound(roundId);
            Interlocked.Exchange(ref scratchResult, result);
            if (result.Granted)
            {
                store.AbsorbStack(StackWithPrizeStillHidden(result));
            }
            else
            {
                store.RefreshNow();
            }
        }, () => roundInFlight = false);
    }

    // The card is settled at purchase, but the cabinet only celebrates once the foil comes off:
    // the stack the player sees holds the prize back until the reveal commits it.
    internal static long StackWithPrizeStillHidden(CasinoScratchCardDto card)
    {
        return card.Stack - card.Prize;
    }

    private void IssueBarkeepStart(string sittingId, string roundId)
    {
        work.Run("barkeep start", async token =>
        {
            var result = await casino.StartBarkeepAsync(sittingId, roundId, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref roundFailed, 1);
                return;
            }

            if (result.Granted)
            {
                store.AbsorbStack(result.Stack);
            }
            else
            {
                ClearPendingRound(roundId);
                store.RefreshNow();
            }

            Interlocked.Exchange(ref barkeepStartResult, result);
        }, () => roundInFlight = false);
    }

    private void IssueBarkeepFinish(string roundId, CasinoBarkeepOrderRequest[] orders)
    {
        work.Run("barkeep finish", async token =>
        {
            var result = await casino.FinishBarkeepAsync(roundId, orders, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref roundFailed, 1);
                return;
            }

            if (!string.Equals(result.Reason, CasinoReasons.Cooldown, StringComparison.Ordinal))
            {
                ClearPendingRound(roundId);
            }

            Interlocked.Exchange(ref barkeepFinishResult, result);
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
