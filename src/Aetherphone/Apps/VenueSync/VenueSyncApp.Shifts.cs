using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp
{
    private string? shiftsActionError;

    private void DrawShifts(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), "Shifts", back);

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            if (shiftsActionError is not null)
            {
                var errorOrigin = ImGui.GetCursorScreenPos();
                var errorWidth = ImGui.GetContentRegionAvail().X;
                Marquee.DrawLeftAuto("venuesync.shifts.error", $"{shiftsActionError} — tap to retry", errorOrigin.X,
                    errorOrigin.Y, errorWidth, TextStyles.Caption1, theme.Danger);
                ImGui.Dummy(new Vector2(0f, 20f * scale + Metrics.Space.Sm * scale));
            }

            if (state.Shifts is null)
            {
                var emptyOrigin = ImGui.GetCursorScreenPos();
                var emptyWidth = ImGui.GetContentRegionAvail().X;
                Marquee.DrawCenteredAuto("venuesync.shifts.empty", "No shift data",
                    emptyOrigin.X + emptyWidth * 0.5f, emptyOrigin.Y, emptyWidth, TextStyles.Body, theme.TextMuted);
                return;
            }

            var activeShift = state.Shifts.Shifts.FirstOrDefault(s => s.Status == "ACTIVE");
            var upcomingShifts = state.Shifts.Shifts.Where(s => s.Status == "SCHEDULED").ToList();
            if (activeShift is null && state.Shifts.OpenShifts.Count == 0 && upcomingShifts.Count == 0)
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Clock, "No Shifts", "Nothing scheduled right now.");
                return;
            }

            if (activeShift is not null)
            {
                var activeCard = GroupCard.Begin(theme, 1, ShiftRow.Height, showSeparators: false);
                var activeRow = activeCard.NextRow();
                var action = ShiftRow.Draw(activeCard, activeRow, activeShift, isOpen: false, roleName: null, theme,
                    "active");
                HandleShiftAction(action, activeShift.Id);
                activeCard.End();
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            }

            if (state.Shifts.OpenShifts.Count > 0)
            {
                ui.SectionHeading("OPEN — CLAIM");
                var openCard = GroupCard.Begin(theme, state.Shifts.OpenShifts.Count, ShiftRow.Height, showSeparators: false);
                foreach (var open in state.Shifts.OpenShifts)
                {
                    var shim = new VenueSyncShift
                    {
                        Id = open.Id,
                        ScheduledStart = open.ScheduledStart,
                        ScheduledEnd = open.ScheduledEnd,
                        Status = "OPEN",
                    };
                    var openRow = openCard.NextRow();
                    var action = ShiftRow.Draw(openCard, openRow, shim, isOpen: true, open.RoleName, theme,
                        $"open-{open.Id}");
                    HandleShiftAction(action, open.Id);
                }

                openCard.End();
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            }

            if (upcomingShifts.Count > 0)
            {
                ui.SectionHeading("UPCOMING");
                var upcomingCard = GroupCard.Begin(theme, upcomingShifts.Count, ShiftRow.Height, showSeparators: false);
                foreach (var scheduled in upcomingShifts)
                {
                    var upcomingRow = upcomingCard.NextRow();
                    var action = ShiftRow.Draw(upcomingCard, upcomingRow, scheduled, isOpen: false, null, theme,
                        $"upcoming-{scheduled.Id}");
                    HandleShiftAction(action, scheduled.Id);
                }

                upcomingCard.End();
            }
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
