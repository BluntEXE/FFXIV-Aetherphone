namespace Aetherphone.Core.Notifications;

internal sealed class AppNotificationSetting
{
    public bool Enabled { get; set; } = true;
    public string? Sound { get; set; }
}
