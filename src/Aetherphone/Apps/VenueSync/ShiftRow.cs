using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.VenueSync;

internal enum ShiftRowAction { None, ClockIn, ClockOut, Claim }

// Mirrors Jobs' DrawJobRow (Apps/Jobs/JobsApp.cs:351-425): icon tile on the left, a two-tier
// title/subtitle stack, a hover overlay over the row, and an accent-tinted status pill for state
// that isn't already conveyed by the action button's color.
internal static class ShiftRow
{
    public const float Height = 64f;

    public static ShiftRowAction Draw(Rect row, VenueSyncShift shift, bool isOpen, string? roleName,
        PhoneTheme theme, string idSuffix)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();

        var titleText = isOpen ? (roleName ?? "Open Shift") : shift.Status switch
        {
            "ACTIVE" => "Active Shift",
            "COMPLETED" => "Completed Shift",
            _ => "Upcoming Shift",
        };
        var timeText = FormatTimeRange(shift.ScheduledStart, shift.ScheduledEnd);
        var isActive = !isOpen && shift.Status == "ACTIVE";
        var label = isOpen ? "Claim" : isActive ? "Clock Out" : "Clock In";

        var buttonWidth = MathF.Max(72f * scale, Typography.Measure(label, TextStyles.BodyEmphasized).X + 32f * scale);
        var buttonHeight = 28f * scale;
        var actionRect = new Rect(new Vector2(row.Max.X - buttonWidth, row.Center.Y - buttonHeight * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + buttonHeight * 0.5f));

        // Independent hover per interactive region — this is the deliberate fix for a known bug
        // class in this codebase where two stacked elements share one hover-bool. The row overlay
        // excludes the button's own hit-rect so the two hovers never fight each other.
        var overButton = UiInteract.Hover(actionRect.Min, actionRect.Max);
        var rowHovered = !overButton && UiInteract.Hover(row.Min, row.Max);
        if (rowHovered)
        {
            var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0.14f : 0.07f;
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        }

        var iconSize = 42f * scale;
        var iconCenter = new Vector2(row.Min.X + iconSize * 0.5f, row.Center.Y);
        var tileTint = isActive ? theme.Danger : theme.Accent;
        IconTile.Draw(iconCenter, iconSize, IconTile.Surface(tileTint), FontAwesomeIcon.Clock);

        var textLeft = row.Min.X + iconSize + 14f * scale;
        var badgeReserve = isOpen ? Typography.Measure("OPEN", TextStyles.Caption2).X + 16f * scale + 10f * scale : 0f;
        var textRight = actionRect.Min.X - 12f * scale - badgeReserve;
        var textWidth = MathF.Max(1f, textRight - textLeft);

        // Independent hover per stacked line, per this codebase's known Marquee bug class — each
        // line calls its own *Auto variant, which computes hover internally, instead of sharing
        // one hover bool across both lines.
        Marquee.DrawLeftAuto($"shiftrow-title-{idSuffix}", titleText, textLeft, row.Center.Y - 16f * scale, textWidth,
            TextStyles.Headline, theme.TextStrong);
        Marquee.DrawLeftAuto($"shiftrow-time-{idSuffix}", timeText, textLeft, row.Center.Y + 4f * scale, textWidth,
            TextStyles.Footnote, theme.TextMuted);

        if (isOpen)
        {
            DrawOpenBadge(drawList, new Vector2(actionRect.Min.X - 12f * scale, row.Center.Y), theme, scale);
        }

        if (rowHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var clicked = isActive
            ? AppSkin.DangerPillButton(actionRect, label, theme)
            : AppSkin.PillButton(actionRect, label, filled: true, theme);

        if (!clicked) return ShiftRowAction.None;
        if (isOpen) return ShiftRowAction.Claim;
        return isActive ? ShiftRowAction.ClockOut : ShiftRowAction.ClockIn;
    }

    // Mirrors Jobs' DrawActiveBadge (Apps/Jobs/JobsApp.cs:427-439): accent @ ~20% alpha pill with
    // accent-colored text, used here to call out claimable shifts the same way Jobs calls out the
    // active gearset.
    private static void DrawOpenBadge(ImDrawListPtr drawList, Vector2 rightCenter, PhoneTheme theme, float scale)
    {
        const string text = "OPEN";
        var textSize = Typography.Measure(text, TextStyles.Caption2);
        var padX = 8f * scale;
        var padY = 4f * scale;
        var width = textSize.X + padX * 2f;
        var height = textSize.Y + padY * 2f;
        var max = new Vector2(rightCenter.X, rightCenter.Y + height * 0.5f);
        var min = new Vector2(max.X - width, rightCenter.Y - height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.2f)));
        Typography.Draw(drawList, new Vector2(min.X + padX, rightCenter.Y - textSize.Y * 0.5f), text, theme.Accent,
            TextStyles.Caption2);
    }

    private static string FormatTimeRange(string startIso, string endIso)
    {
        if (!DateTime.TryParse(startIso, out var start) || !DateTime.TryParse(endIso, out var end))
            return "Unknown time";
        return $"{start:ddd h:mm tt} – {end:h:mm tt}";
    }
}
