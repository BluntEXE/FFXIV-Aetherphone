using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.VenueSync;

internal enum ShiftRowAction { None, ClockIn, ClockOut, Claim }

internal static class ShiftRow
{
    public const float Height = 56f;

    public static ShiftRowAction Draw(Rect row, VenueSyncShift shift, bool isOpen, string? roleName,
        PhoneTheme theme, string idSuffix)
    {
        var titleText = isOpen ? (roleName ?? "Open Shift") : shift.Status switch
        {
            "ACTIVE" => "Active Shift",
            "COMPLETED" => "Completed Shift",
            _ => "Upcoming Shift",
        };
        var timeText = FormatTimeRange(shift.ScheduledStart, shift.ScheduledEnd);

        // Independent hover per line — this is the deliberate fix for a known bug class in
        // this codebase where two stacked text lines share one hover-bool and scroll together
        // instead of independently. Never compute one hovered flag and pass it to two Marquee
        // calls — each line here calls its own *Auto variant, which computes hover internally.
        Marquee.DrawLeftAuto($"shiftrow-title-{idSuffix}", titleText, row.Min.X, row.Min.Y, row.Width * 0.65f,
            TextStyles.Body, theme.TextStrong);
        Marquee.DrawLeftAuto($"shiftrow-time-{idSuffix}", timeText, row.Min.X, row.Min.Y + 20f, row.Width * 0.65f,
            TextStyles.Caption1, theme.TextMuted);

        var actionRect = new Rect(new Vector2(row.Max.X - 88f, row.Min.Y + 12f), new Vector2(row.Max.X, row.Min.Y + 40f));
        var label = isOpen ? "Claim" : shift.Status == "ACTIVE" ? "Clock Out" : "Clock In";
        var clicked = !isOpen && shift.Status == "ACTIVE"
            ? AppSkin.DangerPillButton(actionRect, label, theme)
            : AppSkin.PillButton(actionRect, label, filled: true, theme);

        if (!clicked) return ShiftRowAction.None;
        if (isOpen) return ShiftRowAction.Claim;
        return shift.Status == "ACTIVE" ? ShiftRowAction.ClockOut : ShiftRowAction.ClockIn;
    }

    private static string FormatTimeRange(string startIso, string endIso)
    {
        if (!DateTime.TryParse(startIso, out var start) || !DateTime.TryParse(endIso, out var end))
            return "Unknown time";
        return $"{start:ddd h:mm tt} – {end:h:mm tt}";
    }
}
