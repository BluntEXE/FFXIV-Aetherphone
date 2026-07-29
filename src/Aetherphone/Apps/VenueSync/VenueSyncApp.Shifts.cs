using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp
{
    private string? shiftsActionError;

    private void DrawShifts(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), "Shifts", back);

        var cursorY = area.Min.Y + AppHeader.Height * scale;
        var left = area.Min.X + Metrics.Space.Lg * scale;
        var right = area.Max.X - Metrics.Space.Lg * scale;
        var width = MathF.Max(1f, right - left);

        if (shiftsActionError is not null)
        {
            Marquee.DrawLeftAuto("venuesync.shifts.error", $"{shiftsActionError} — tap to retry", left, cursorY,
                width, TextStyles.Caption1, theme.Danger);
            cursorY += 20f * scale + Metrics.Space.Sm * scale;
        }

        if (state.Shifts is null)
        {
            Marquee.DrawCenteredAuto("venuesync.shifts.empty", "No shift data", area.Center.X, cursorY, width,
                TextStyles.Body, theme.TextMuted);
            return;
        }

        var activeShift = state.Shifts.Shifts.FirstOrDefault(s => s.Status == "ACTIVE");
        if (activeShift is not null)
        {
            var row = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + ShiftRow.Height * scale));
            var action = ShiftRow.Draw(row, activeShift, isOpen: false, roleName: null, theme, "active");
            HandleShiftAction(action, activeShift.Id);
            cursorY += ShiftRow.Height * scale + Metrics.Space.Sm * scale;
        }

        cursorY += Metrics.Space.Md * scale;
        Marquee.DrawLeftAuto("venuesync.shifts.open-heading", "OPEN — CLAIM", left, cursorY, width,
            TextStyles.Caption1, theme.TextMuted);
        cursorY += 20f * scale + Metrics.Space.Xs * scale;

        foreach (var open in state.Shifts.OpenShifts)
        {
            var shim = new VenueSyncShift
            {
                Id = open.Id,
                ScheduledStart = open.ScheduledStart,
                ScheduledEnd = open.ScheduledEnd,
                Status = "OPEN",
            };
            var row = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + ShiftRow.Height * scale));
            var action = ShiftRow.Draw(row, shim, isOpen: true, open.RoleName, theme, $"open-{open.Id}");
            HandleShiftAction(action, open.Id);
            cursorY += ShiftRow.Height * scale + Metrics.Space.Sm * scale;
        }

        cursorY += Metrics.Space.Md * scale;
        Marquee.DrawLeftAuto("venuesync.shifts.upcoming-heading", "UPCOMING", left, cursorY, width,
            TextStyles.Caption1, theme.TextMuted);
        cursorY += 20f * scale + Metrics.Space.Xs * scale;

        foreach (var scheduled in state.Shifts.Shifts.Where(s => s.Status == "SCHEDULED"))
        {
            var row = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + ShiftRow.Height * scale));
            var action = ShiftRow.Draw(row, scheduled, isOpen: false, null, theme, $"upcoming-{scheduled.Id}");
            HandleShiftAction(action, scheduled.Id);
            cursorY += ShiftRow.Height * scale + Metrics.Space.Sm * scale;
        }
    }

    private void HandleShiftAction(ShiftRowAction action, string shiftId)
    {
        switch (action)
        {
            case ShiftRowAction.ClockIn:
                _ = RunShiftActionAsync(() => client.ClockInAsync(shiftId, CancellationToken.None),
                    "Failed to clock in.");
                break;
            case ShiftRowAction.ClockOut:
                _ = RunShiftActionAsync(() => client.ClockOutAsync(shiftId, CancellationToken.None),
                    "Failed to clock out.");
                break;
            case ShiftRowAction.Claim:
                _ = RunShiftActionAsync(() => client.ClaimShiftAsync(shiftId, CancellationToken.None),
                    "Failed to claim shift.");
                break;
            case ShiftRowAction.None:
            default:
                break;
        }
    }

    private async Task RunShiftActionAsync(Func<Task<VenueSyncClockResult?>> action, string failureMessage)
    {
        try
        {
            var result = await action().ConfigureAwait(false);
            if (result is { Success: true })
            {
                shiftsActionError = null;
                state.EnsureShiftsFresh(true);
            }
            else
            {
                shiftsActionError = result?.Error ?? failureMessage;
            }
        }
        catch (Exception exception)
        {
            shiftsActionError = exception.Message;
        }
    }
}
