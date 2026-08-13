using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SoundsPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Sounds);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.VolumeUp;
    public Vector4 Tint => new(0.95f, 0.40f, 0.65f, 1f);
    private readonly Configuration configuration;
    private readonly SoundService sound;
    private readonly ISettingsNavigator navigator;
    private readonly ISettingsPage ringtonePage;
    private readonly ISettingsPage notificationSoundPage;

    public SoundsPage(Configuration configuration, SoundService sound, ISettingsNavigator navigator,
        ISettingsPage ringtonePage, ISettingsPage notificationSoundPage)
    {
        this.configuration = configuration;
        this.sound = sound;
        this.navigator = navigator;
        this.ringtonePage = ringtonePage;
        this.notificationSoundPage = notificationSoundPage;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * UiScale.Current));
            var card = GroupCard.Begin(theme, 3);
            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.Ringtone),
                    sound.Label(SoundKind.Ringtone, configuration.RingtoneSound), theme))
            {
                navigator.Open(ringtonePage);
            }

            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Settings.NotificationSound),
                    sound.Label(SoundKind.Notification, configuration.NotificationSound), theme))
            {
                navigator.Open(notificationSoundPage);
            }

            var vibration = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.Vibration), configuration.Vibration,
                theme, null, Loc.T(L.Settings.VibrationHint));
            card.End();
            if (vibration != configuration.Vibration)
            {
                configuration.Vibration = vibration;
                configuration.Save();
            }
        }
    }
}
