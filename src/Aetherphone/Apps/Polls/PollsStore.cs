using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Polls;

internal sealed class PollsStore : IDisposable
{
    // poll.ping announces every create/close/reopen, so the timer is only the fallback
    // for a dropped socket; anything shorter just re-downloads an unchanged page.
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(45);

    private readonly AethernetSession session;
    private readonly PollsClient client;
    private readonly AppGate gate;
    private readonly RealtimeSignalBus signals;
    private readonly StoreWork work = new StoreWork("Polls");

    private volatile PollDto[] polls = Array.Empty<PollDto>();
    private volatile string? pollsCursor;
    private volatile bool loadingMore;
    private volatile bool pagedDeeper;
    private volatile bool loading;
    private volatile bool loadedOnce;
    private volatile bool pingRefreshRequested;
    private DateTime lastBackgroundRefreshUtc = DateTime.MinValue;

    public PollsStore(AethernetSession session, PollsClient client, AppGate gate, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.gate = gate;
        this.signals = signals;
        signals.PollsPinged += OnPollsPinged;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    private void OnPollsPinged()
    {
        pingRefreshRequested = true;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public PollDto[] Polls => polls;

    public bool Loading => loading;

    public bool LoadingMore => loadingMore;

    public bool HasMore => pollsCursor is not null;

    public bool LoadedOnce => loadedOnce;

    public int UnvotedCount
    {
        get
        {
            if (!session.IsSignedIn)
            {
                return 0;
            }

            var snapshot = polls;
            var count = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (!snapshot[index].Closed && snapshot[index].MyVote < 0)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public void Refresh()
    {
        if (!session.IsSignedIn || loading)
        {
            return;
        }

        loading = true;
        var lang = Loc.Current.Code;
        work.Run("polls refresh", async token =>
        {
            var page = await client.ListAsync(null, lang, token).ConfigureAwait(false);
            if (page is not null)
            {
                if (pagedDeeper)
                {
                    polls = IdentifiedMerge.MergeById(polls, page.Items, ByNewestFirst);
                }
                else
                {
                    polls = page.Items;
                    pollsCursor = page.NextCursor;
                }

                loadedOnce = true;
            }
        }, () => loading = false);
    }

    public void LoadMore()
    {
        var cursor = pollsCursor;
        if (!session.IsSignedIn || cursor is null || loadingMore || loading)
        {
            return;
        }

        loadingMore = true;
        pagedDeeper = true;
        var lang = Loc.Current.Code;
        work.Run("polls more", async token =>
        {
            var page = await client.ListAsync(cursor, lang, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            polls = IdentifiedMerge.MergeById(polls, page.Items, ByNewestFirst);
            pollsCursor = page.NextCursor;
        }, () => loadingMore = false);
    }

    private static int ByNewestFirst(PollDto left, PollDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    public void Vote(PollDto poll, int optionIndex)
    {
        if (poll.Closed || optionIndex < 0 || optionIndex >= poll.Options.Length)
        {
            return;
        }

        var target = poll.MyVote == optionIndex ? -1 : optionIndex;
        polls = CopyOnWrite.Replace(polls, ApplyVote(poll, target));
        var lang = Loc.Current.Code;
        work.Run("vote", async token =>
        {
            var result = target < 0
                ? await client.ClearVoteAsync(poll.Id, lang, token).ConfigureAwait(false)
                : await client.VoteAsync(poll.Id, target, lang, token).ConfigureAwait(false);
            if (result is not null)
            {
                polls = CopyOnWrite.Replace(polls, result);
            }
        });
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn || !gate.Open)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!pingRefreshRequested && now - lastBackgroundRefreshUtc < BackgroundRefreshInterval)
        {
            return;
        }

        pingRefreshRequested = false;
        lastBackgroundRefreshUtc = now;
        Refresh();
    }

    private static PollDto ApplyVote(PollDto poll, int newVote)
    {
        var counts = (int[])poll.VoteCounts.Clone();
        if (poll.MyVote >= 0 && poll.MyVote < counts.Length && counts[poll.MyVote] > 0)
        {
            counts[poll.MyVote]--;
        }

        if (newVote >= 0 && newVote < counts.Length)
        {
            counts[newVote]++;
        }

        var total = 0;
        for (var index = 0; index < counts.Length; index++)
        {
            total += counts[index];
        }

        return poll with { VoteCounts = counts, TotalVotes = total, MyVote = newVote };
    }

    public void Dispose()
    {
        signals.PollsPinged -= OnPollsPinged;
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
    }
}
