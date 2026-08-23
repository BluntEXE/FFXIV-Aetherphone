using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Globalization;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NotificationsPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Notifications);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Bell;
    public Vector4 Tint => new(0.98f, 0.27f, 0.25f, 1f);
    private readonly Configuration configuration;
    private readonly ISettingsNavigator navigator;
    private readonly AppNotificationPage appPage;
    private readonly AppInstaller installer;
    private readonly NotificationChannel[] sortedChannels = BuildChannelsArray();
    private LanguageInfo? sortedLanguage;

    public NotificationsPage(Configuration configuration, ISettingsNavigator navigator, AppNotificationPage appPage,
        AppInstaller installer)
    {
        this.configuration = configuration;
        this.navigator = navigator;
        this.appPage = appPage;
        this.installer = installer;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            var doNotDisturb = configuration.DoNotDisturb;
            var alerts = GroupCard.Begin(theme, 2);
            var quietWhileBusy = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.QuietWhileBusy),
                configuration.QuietWhileBusy, theme, null, Loc.T(L.Settings.QuietWhileBusyHint),
                dimmed: doNotDisturb);
            var showNotificationBanner = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.ShowNotificationBanner),
                configuration.ShowNotificationBanner, theme, null, Loc.T(L.Settings.ShowNotificationBannerHint),
                dimmed: doNotDisturb);
            alerts.End();
            if (quietWhileBusy != configuration.QuietWhileBusy)
            {
                configuration.QuietWhileBusy = quietWhileBusy;
                configuration.Save();
            }

            if (showNotificationBanner != configuration.ShowNotificationBanner)
            {
                configuration.ShowNotificationBanner = showNotificationBanner;
                configuration.Save();
            }

            EnsureSortedChannels();
            SettingsSection.Header(Loc.T(L.Settings.NotificationApps), theme);
            var apps = GroupCard.Begin(theme, CountInstalled(NotificationChannels.All));
            for (var index = 0; index < sortedChannels.Length; index++)
            {
                var channel = sortedChannels[index];
                if (!installer.IsInstalled(channel.AppId))
                {
                    continue;
                }

                if (SettingsRow.AppLink(apps.NextRow(), channel.AppId, channel.Accent, Loc.T(channel.Name),
                        Summarize(channel.AppId), theme))
                {
                    appPage.Show(channel);
                    navigator.Open(appPage);
                }
            }

            apps.End();
        }
    }

    private void EnsureSortedChannels()
    {
        if (ReferenceEquals(sortedLanguage, Loc.Current))
        {
            return;
        }

        Array.Sort(sortedChannels, static (left, right) =>
        {
            var primary = Loc.Culture.CompareInfo.Compare(Loc.T(left.Name), Loc.T(right.Name),
                CompareOptions.IgnoreCase);
            return primary != 0 ? primary : string.CompareOrdinal(left.AppId, right.AppId);
        });
        sortedLanguage = Loc.Current;
    }

    private static NotificationChannel[] BuildChannelsArray()
    {
        var channels = NotificationChannels.All;
        var array = new NotificationChannel[channels.Count];
        for (var index = 0; index < channels.Count; index++)
        {
            array[index] = channels[index];
        }

        return array;
    }

    private int CountInstalled(IReadOnlyList<NotificationChannel> channels)
    {
        var count = 0;
        for (var index = 0; index < channels.Count; index++)
        {
            if (installer.IsInstalled(channels[index].AppId))
            {
                count++;
            }
        }

        return count;
    }

    private string Summarize(string appId) =>
        configuration.IsAppNotificationEnabled(appId) ? string.Empty : Loc.T(L.Settings.NotificationsOff);
}
