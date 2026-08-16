using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.VenueSync;

internal enum ShiftRowAction { None, ClockIn, ClockOut, Claim }

// Mirrors Jobs' DrawJobRow (Apps/Jobs/JobsApp.cs:351-425): icon tile on the left, a two-tier
// title/subtitle stack, a hover overlay over the row, and an accent-tinted status pill for state
// that isn't already conveyed by the action button's color. The hover overlay itself is drawn via
// GroupCard.DrawHoverHighlight so it's full-bleed to the card edges and corner-rounded to match,
// instead of the plain square AddRectFilled Jobs uses over its own (non-GroupCard) row bounds.
internal static class ShiftRow
{
    public const float Height = 64f;

    public static ShiftRowAction Draw(GroupCard card, Rect row, VenueSyncShift shift, bool isOpen, string? roleName,
        PhoneTheme theme, Vector4 accent, string idSuffix)
    {
        var scale = UiScale.Current;

        var titleText = isOpen ? (roleName ?? Loc.T(L.VenueSync.OpenShift)) : shift.Status switch
        {
            "ACTIVE" => Loc.T(L.VenueSync.ActiveShift),
            "COMPLETED" => Loc.T(L.VenueSync.CompletedShift),
            _ => Loc.T(L.VenueSync.UpcomingShift),
        };
        var timeText = FormatTimeRange(shift.ScheduledStart, shift.ScheduledEnd);
        var isActive = !isOpen && shift.Status == "ACTIVE";
        var label = isOpen ? Loc.T(L.VenueSync.Claim)
            : isActive ? Loc.T(L.VenueSync.ClockOut) : Loc.T(L.VenueSync.ClockIn);

        var buttonWidth = MathF.Max(72f * scale, Typography.Measure(label, TextStyles.BodyEmphasized).X + 32f * scale);
        var buttonHeight = 28f * scale;
        var actionRect = new Rect(new Vector2(row.Max.X - buttonWidth, row.Center.Y - buttonHeight * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + buttonHeight * 0.5f));

        // Independent hover per interactive region: this is the deliberate fix for a known bug
        // class in this codebase where two stacked elements share one hover-bool. The row overlay
        // excludes the button's own hit-rect so the two hovers never fight each other.
        var overButton = UiInteract.Hover(actionRect.Min, actionRect.Max);
        var rowHovered = !overButton && UiInteract.Hover(row.Min, row.Max);
        if (rowHovered)
        {
            var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0.14f : 0.07f;
            card.DrawHoverHighlight(new Vector4(1f, 1f, 1f, alpha));
        }

        var iconSize = 42f * scale;
        var iconCenter = new Vector2(row.Min.X + iconSize * 0.5f, row.Center.Y);
        var tileTint = isActive ? theme.ToggleOn : accent;
        IconTile.Draw(iconCenter, iconSize, IconTile.Surface(tileTint), FontAwesomeIcon.Clock);

        var textLeft = row.Min.X + iconSize + 14f * scale;
        var textRight = actionRect.Min.X - 12f * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);

        // Independent hover per stacked line, per this codebase's known Marquee bug class: each
        // line calls its own *Auto variant, which computes hover internally, instead of sharing
        // one hover bool across both lines.
        Marquee.DrawLeftAuto($"shiftrow-title-{idSuffix}", titleText, textLeft, row.Center.Y - 16f * scale, textWidth,
            TextStyles.Headline, theme.TextStrong);
        Marquee.DrawLeftAuto($"shiftrow-time-{idSuffix}", timeText, textLeft, row.Center.Y + 4f * scale, textWidth,
            TextStyles.Footnote, theme.TextMuted);

        if (rowHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var clicked = isActive
            ? AppSkin.DangerPillButton(actionRect, label, theme)
            : AppSkin.PillButton(actionRect, label, true, accent, theme);

        if (!clicked)
        {
            return ShiftRowAction.None;
        }

        if (isOpen)
        {
            return ShiftRowAction.Claim;
        }

        return isActive ? ShiftRowAction.ClockOut : ShiftRowAction.ClockIn;
    }

    private static string FormatTimeRange(string startIso, string endIso)
    {
        if (!DateTime.TryParse(startIso, out var start) || !DateTime.TryParse(endIso, out var end))
        {
            return Loc.T(L.VenueSync.UnknownTime);
        }

        return $"{start:ddd} {TimeText.Clock(start)} – {TimeText.Clock(end)}";
    }
}
