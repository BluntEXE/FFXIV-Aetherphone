using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Moderation;

internal sealed class ModerationNoticeService : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(300);

    private readonly AethernetSession session;
    private readonly AccountClient client;
    private readonly IFramework framework;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence cadence;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<string, byte> acknowledging = new(StringComparer.Ordinal);
    private volatile ModerationNoticeDto[] pending = Array.Empty<ModerationNoticeDto>();
    private volatile bool polling;
    private string? lastAccountId;

    public ModerationNoticeService(AethernetSession session, AccountClient client, IFramework framework,
        PhoneVisibility visibility, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.framework = framework;
        this.signals = signals;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        signals.SocialPinged += cadence.RequestImmediate;
        session.Changed += OnSessionChanged;
        framework.Update += OnFrameworkTick;
    }

    public ModerationNoticeDto[] Pending => pending;

    public event Action? Changed;

    public void RefreshNow()
    {
        if (session.IsSignedIn)
        {
            cadence.RequestImmediate();
        }
    }

    public void Acknowledge(ModerationNoticeDto notice)
    {
        if (!acknowledging.TryAdd(notice.Id, 0))
        {
            return;
        }

        Drop(notice.Id);
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await client.AcknowledgeNoticeAsync(notice.Id, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Notices] ack failed for {notice.Id}: {exception.Message}");
            }
            finally
            {
                acknowledging.TryRemove(notice.Id, out _);
            }
        });
    }

    private void Drop(string noticeId)
    {
        var current = pending;
        var kept = new List<ModerationNoticeDto>(current.Length);
        for (var index = 0; index < current.Length; index++)
        {
            if (!string.Equals(current[index].Id, noticeId, StringComparison.Ordinal))
            {
                kept.Add(current[index]);
            }
        }

        if (kept.Count == current.Length)
        {
            return;
        }

        pending = kept.ToArray();
        Changed?.Invoke();
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        pending = Array.Empty<ModerationNoticeDto>();
        cadence.RequestImmediate();
        Changed?.Invoke();
    }

    private void OnFrameworkTick(IFramework _)
    {
        if (!session.IsSignedIn)
        {
            if (pending.Length > 0)
            {
                pending = Array.Empty<ModerationNoticeDto>();
                Changed?.Invoke();
            }

            return;
        }

        if (!cadence.Due(DateTime.UtcNow))
        {
            return;
        }

        Poll();
    }

    private void Poll()
    {
        if (polling)
        {
            return;
        }

        polling = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var page = await client.NoticesAsync(null, token).ConfigureAwait(false);
                if (page is not null)
                {
                    Ingest(page.Items);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Notices] poll failed: {exception.Message}");
            }
            finally
            {
                polling = false;
            }
        });
    }

    private void Ingest(ModerationNoticeDto[] items)
    {
        var unread = new List<ModerationNoticeDto>(items.Length);
        for (var index = items.Length - 1; index >= 0; index--)
        {
            var item = items[index];
            if (!item.Acknowledged && !acknowledging.ContainsKey(item.Id))
            {
                unread.Add(item);
            }
        }

        if (Matches(unread))
        {
            return;
        }

        pending = unread.ToArray();
        _ = framework.RunOnFrameworkThread(() => Changed?.Invoke());
    }

    private bool Matches(List<ModerationNoticeDto> candidate)
    {
        var current = pending;
        if (current.Length != candidate.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Length; index++)
        {
            if (!string.Equals(current[index].Id, candidate[index].Id, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        signals.SocialPinged -= cadence.RequestImmediate;
        framework.Update -= OnFrameworkTick;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
