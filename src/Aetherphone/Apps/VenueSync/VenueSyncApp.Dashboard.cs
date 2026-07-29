using Aetherphone.Core;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp
{
    private const float StatusCardHeight = 90f;
    private const float ActionRowHeight = GroupCard.DefaultRowHeight;

    private void DrawDashboard(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gearReserve = 44f * scale;
        AppHeader.DrawTitleWithReserve(area, "venuesync.dashboard.title", DisplayName, gearReserve, theme.TextStrong,
            scale);
        var gearCenter = new Vector2(area.Max.X - 22f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        if (AppSkin.IconButton(gearCenter, 14f * scale, FontAwesomeIcon.Cog.ToIconString(), theme.TextMuted,
                AppSkin.Transparent, 0.9f, theme))
        {
            router.Push(VenueSyncRoute.Settings);
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            DrawStatusCard(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            DrawActionsCard(scale);
        }
    }

    private void DrawStatusCard(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = StatusCardHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var dl = ImGui.GetWindowDrawList();
        Squircle.Fill(dl, card.Min, card.Max, Metrics.Radius.Card * scale, ImGui.GetColorU32(theme.GroupedCard));

        var pad = Metrics.Space.Lg * scale;
        var activeShift = FindActiveShift();
        if (activeShift is not null)
        {
            var venueLabel = string.IsNullOrEmpty(configuration.VenueSyncSelectedVenueName)
                ? "No venue selected"
                : configuration.VenueSyncSelectedVenueName;
            var buttonWidth = 100f * scale;
            var textLeft = card.Min.X + pad;
            var textTop = card.Min.Y + pad;
            var textMaxWidth = MathF.Max(1f, width - pad * 2f - buttonWidth - Metrics.Space.Md * scale);

            // Independent hover per line, per this codebase's stacked-Marquee bug class — each
            // line calls its own *Auto variant instead of sharing one hover bool.
            Marquee.DrawLeftAuto("venuesync.dashboard.venue", venueLabel, textLeft, textTop, textMaxWidth,
                TextStyles.Headline, theme.TextStrong);
            Marquee.DrawLeftAuto("venuesync.dashboard.status", "ON SHIFT", textLeft, textTop + 24f * scale,
                textMaxWidth, TextStyles.Caption1, theme.Accent);

            var buttonHeight = 32f * scale;
            var button = new Rect(new Vector2(card.Max.X - pad - buttonWidth, card.Center.Y - buttonHeight * 0.5f),
                new Vector2(card.Max.X - pad, card.Center.Y + buttonHeight * 0.5f));

            // Fire-and-forget convenience action only — Task 8's Shifts screen owns the
            // authoritative clock-in/out UI with full error handling, so no inline error
            // display is needed here.
            if (SettingsRow.Action(button, "Clock Out", theme.Danger, theme))
            {
                var shiftId = activeShift.Id;
                _ = ClockOutFireAndForget(shiftId);
            }
        }
        else
        {
            Marquee.DrawCenteredAuto("venuesync.dashboard.offshift", "OFF SHIFT", card.Center.X,
                card.Center.Y - 8f * scale, width - pad * 2f, TextStyles.Headline, theme.TextMuted);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private async Task ClockOutFireAndForget(string shiftId)
    {
        var result = await client.ClockOutAsync(shiftId, CancellationToken.None).ConfigureAwait(false);
        if (result is { Success: true })
        {
            state.EnsureShiftsFresh(true);
        }
    }

    private VenueSyncShift? FindActiveShift()
    {
        var shifts = state.Shifts?.Shifts;
        if (shifts is null)
        {
            return null;
        }

        foreach (var shift in shifts)
        {
            if (shift.Status == "ACTIVE")
            {
                return shift;
            }
        }

        return null;
    }

    private void DrawActionsCard(float scale)
    {
        var openShiftsCount = state.Shifts?.OpenShifts.Count ?? 0;
        var card = GroupCard.Begin(theme, 2, ActionRowHeight);

        var logSaleRow = card.NextRow();
        if (SettingsRow.Disclosure(logSaleRow, "Log a Sale", string.Empty, theme))
        {
            router.Push(VenueSyncRoute.Sales);
        }

        var upcomingRow = card.NextRow();
        if (SettingsRow.Disclosure(upcomingRow, "Upcoming Shifts", openShiftsCount.ToString(), theme))
        {
            router.Push(VenueSyncRoute.Shifts);
        }

        card.End();

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var sessionRow = new Rect(origin, new Vector2(origin.X + width, origin.Y + ActionRowHeight * scale));
        var count = state.SessionSalesCount;
        var sessionValue = count == 0 ? "No sales yet" : $"{count} sale{(count == 1 ? "" : "s")} · {state.SessionSalesTotal:N0}g";
        SettingsRow.Info(sessionRow, "This Session", sessionValue, theme);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ActionRowHeight * scale));
    }
}
