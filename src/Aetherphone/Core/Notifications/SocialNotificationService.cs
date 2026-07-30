using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class SocialNotificationService : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);

    private static readonly string[] ServedApps =
    {
        SocialActivity.ChirperApp,
        SocialActivity.AethergramApp,
        SocialActivity.VelvetApp,
        SocialActivity.YellowPagesApp,
        SocialActivity.MessageApp,
    };

    private readonly AethernetSession session;
    private readonly AppInstaller installer;
    private readonly AccountClient client;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence cadence;
    private readonly CancellationTokenSource cancellation = new();
    private readonly HashSet<string> seenIds = new();
    private volatile NotificationDto[] latest = Array.Empty<NotificationDto>();
    private volatile Dictionary<string, int>? serverUnread;
    private volatile bool polling;
    private volatile bool primed;
    private string? lastAccountId;

    public SocialNotificationService(AethernetSession session, AccountClient client, NotificationService notifications,
        Configuration configuration, IFramework framework, PhoneVisibility visibility, RealtimeSignalBus signals,
        AppInstaller installer)
    {
        this.session = session;
        this.installer = installer;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        this.framework = framework;
        this.signals = signals;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        signals.SocialPinged += cadence.RequestImmediate;
        signals.ConnectedChanged += OnRealtimeConnected;
        session.Changed += OnSessionChanged;
        framework.Update += OnFrameworkTick;
    }

    private void OnRealtimeConnected(bool active)
    {
        // Every ping sent while the socket was down is gone; resync instead of
        // waiting out the backstop interval.
        if (active)
        {
            cadence.RequestImmediate();
        }
    }

    private void OnSessionChanged()
    {
        // A sign-out blip (relog, AFK logout, transient 401) must not reset the
        // seen state, or everything that arrived during the blip is absorbed
        // silently; only a real account switch starts fresh.
        var accountId = session.CurrentUser?.Id;
        if (accountId is null || string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        latest = Array.Empty<NotificationDto>();
        serverUnread = null;
        seenIds.Clear();
        primed = false;
        cadence.RequestImmediate();
    }

    public NotificationDto[] Latest => latest;

    public int CountFor(string app)
    {
        var items = latest;
        var count = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].App == app)
            {
                count++;
            }
        }

        return count;
    }

    public int UnseenCount(string app)
    {
        var counts = serverUnread;
        if (counts is not null)
        {
            return counts.GetValueOrDefault(app, 0);
        }

        var items = latest;
        var seenUnix = SeenUnix(app);
        var count = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].App == app && items[index].CreatedAtUnix > seenUnix)
            {
                count++;
            }
        }

        return count;
    }

    public void MarkSeen(string app)
    {
        var items = latest;
        var newest = 0L;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].App == app && items[index].CreatedAtUnix > newest)
            {
                newest = items[index].CreatedAtUnix;
            }
        }

        notifications.RemoveSocial(app);
        var counts = serverUnread;
        if (counts is not null && counts.GetValueOrDefault(app, 0) > 0)
        {
            var updated = new Dictionary<string, int>(counts);
            updated[app] = 0;
            serverUnread = updated;
        }

        if (newest <= SeenUnix(app))
        {
            return;
        }

        configuration.SocialActivitySeenUnix[SeenKey(app)] = newest;
        configuration.Save();
        AcknowledgeRead(newest, app);
    }

    public void AcknowledgeUpTo(string app, long upToUnix)
    {
        if (upToUnix <= 0)
        {
            return;
        }

        if (upToUnix > SeenUnix(app))
        {
            configuration.SocialActivitySeenUnix[SeenKey(app)] = upToUnix;
            configuration.Save();
        }

        var counts = serverUnread;
        if (counts is not null)
        {
            var items = latest;
            var remaining = 0;
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index].App == app && !items[index].Read && items[index].CreatedAtUnix > upToUnix)
                {
                    remaining++;
                }
            }

            var updated = new Dictionary<string, int>(counts);
            updated[app] = remaining;
            serverUnread = updated;
        }

        AcknowledgeRead(upToUnix, app);
    }

    private string SeenKey(string app)
    {
        var accountId = lastAccountId;
        return string.IsNullOrEmpty(accountId) ? app : accountId + ":" + app;
    }

    private long SeenUnix(string app)
    {
        if (configuration.SocialActivitySeenUnix.TryGetValue(SeenKey(app), out var value))
        {
            return value;
        }

        return configuration.SocialActivitySeenUnix.GetValueOrDefault(app, 0L);
    }

    private void AcknowledgeRead(long upToUnix, string? app)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await client.MarkNotificationsReadAsync(upToUnix, app, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Notifications] read ack failed: {exception.Message}");
            }
        });
    }

    public void RefreshNow()
    {
        if (session.IsSignedIn && AnyServedAppInstalled())
        {
            cadence.RequestImmediate();
        }
    }

    private bool AnyServedAppInstalled()
    {
        for (var index = 0; index < ServedApps.Length; index++)
        {
            if (installer.IsInstalled(ServedApps[index]))
            {
                return true;
            }
        }

        return false;
    }

    private void OnFrameworkTick(IFramework _)
    {
        if (!session.IsSignedIn || !AnyServedAppInstalled())
        {
            return;
        }

        // Checking the in-flight guard before Due keeps a ping that lands during
        // a poll pending instead of silently consuming it.
        if (polling || !cadence.Due(DateTime.UtcNow))
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
                var page = await client.NotificationsAsync(token).ConfigureAwait(false);
                if (page is not null)
                {
                    serverUnread = page.UnreadByApp;
                    Ingest(page.Items);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Notifications] poll failed: {exception.Message}");
            }
            finally
            {
                polling = false;
            }
        });
    }

    private void Ingest(NotificationDto[] items)
    {
        var wasPrimed = primed;
        for (var index = items.Length - 1; index >= 0; index--)
        {
            var item = items[index];
            if (SocialActivity.IsModerationNotice(item.Type) || !installer.IsInstalled(item.App))
            {
                continue;
            }

            if (wasPrimed && !item.Read && !seenIds.Contains(item.Id))
            {
                Present(item);
            }
        }

        seenIds.Clear();
        for (var index = 0; index < items.Length; index++)
        {
            seenIds.Add(items[index].Id);
        }

        latest = KeepInstalled(items);
        primed = true;
    }

    private NotificationDto[] KeepInstalled(NotificationDto[] items)
    {
        var installedCount = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (installer.IsInstalled(items[index].App))
            {
                installedCount++;
            }
        }

        if (installedCount == items.Length)
        {
            return items;
        }

        var kept = new NotificationDto[installedCount];
        var next = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (installer.IsInstalled(items[index].App))
            {
                kept[next++] = items[index];
            }
        }

        return kept;
    }

    private void Present(NotificationDto item)
    {
        var body = SocialActivity.Body(item);
        if (body.Length == 0)
        {
            return;
        }

        var title = AdTitle(item) ?? SocialActivity.ActorLabel(item);
        notifications.Notify(new PhoneNotification(item.App, title, body, DateTime.Now,
            AccentFor(item.App), GroupKeyFor(item))
        {
            ActorId = item.ActorId,
            PostId = item.PostId,
            SocialType = item.Type,
            CreatedAtUnix = item.CreatedAtUnix,
            ChannelId = item.Type == SocialActivity.TypeMissedCall ? NotificationChannels.PhoneChannel : null,
        });
    }

    private static string GroupKeyFor(NotificationDto item)
    {
        if (item.App == SocialActivity.YellowPagesApp)
        {
            return item.PostId ?? item.Id;
        }

        if (item.Type == SocialActivity.TypeMissedCall)
        {
            return "call:" + item.ActorId;
        }

        // Activity on one post stacks together, so the router's tap-to-open
        // clears exactly the group whose target the user is now viewing.
        if (!string.IsNullOrEmpty(item.PostId))
        {
            return item.App + ":post:" + item.PostId;
        }

        return item.App + ":" + item.Type + ":" + item.ActorId;
    }

    private static string? AdTitle(NotificationDto item)
    {
        return item.Type switch
        {
            SocialActivity.TypeAdExpiring => Loc.T(L.YellowPages.NotifExpiringTitle),
            SocialActivity.TypeAdHidden => Loc.T(L.YellowPages.NotifHiddenTitle),
            SocialActivity.TypeAdOpened => Loc.T(L.YellowPages.NotifOpenedTitle),
            SocialActivity.TypeAdInquiry => Loc.T(L.YellowPages.NotifInquiryTitle),
            _ => null,
        };
    }

    private static Vector4 AccentFor(string app)
    {
        return app switch
        {
            SocialActivity.AethergramApp => AppAccents.For(SocialActivity.AethergramApp),
            SocialActivity.VelvetApp => AppAccents.For(SocialActivity.VelvetApp),
            SocialActivity.YellowPagesApp => AppAccents.For(SocialActivity.YellowPagesApp),
            SocialActivity.MessageApp => AppAccents.For(SocialActivity.MessageApp),
            _ => AppAccents.For(SocialActivity.ChirperApp),
        };
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        signals.SocialPinged -= cadence.RequestImmediate;
        signals.ConnectedChanged -= OnRealtimeConnected;
        framework.Update -= OnFrameworkTick;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
