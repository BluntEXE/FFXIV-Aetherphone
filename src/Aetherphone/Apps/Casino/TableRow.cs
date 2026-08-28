using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal readonly record struct TableRowView(
    string Name,
    string Stakes,
    string Seats,
    string Spectators,
    bool Full,
    bool InviteOnly,
    bool Mine,
    bool Draining);

internal static class TableRow
{
    public const float Height = 74f;

    public static bool Draw(ImDrawListPtr drawList, in Rect row, AppSkin ui, in TableRowView view, float scale)
    {
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        ui.Card(drawList, row.Min, row.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (view.Mine)
        {
            Squircle.Stroke(drawList, row.Min, row.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.40f)), 1f * scale);
        }

        var pad = 14f * scale;
        var badgeWidth = DrawBadge(drawList, row, ui, view, scale);
        var textLeft = row.Min.X + pad;
        var textWidth = row.Width - pad * 2f - badgeWidth;
        var name = Typography.FitText(view.Name, textWidth, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 12f * scale), name,
            view.Draining ? ui.MutedInk : ui.TitleInk, TextStyles.SubheadlineEmphasized);

        var stakes = Typography.FitText(view.Stakes, textWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 32f * scale), stakes, ui.MutedInk,
            TextStyles.Footnote);

        var seatsInk = view.Full ? ui.MutedInk : ui.Accent;
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 50f * scale), view.Seats, seatsInk,
            TextStyles.Caption1);
        if (view.Spectators.Length > 0)
        {
            var seatsWidth = Typography.Measure(view.Seats, TextStyles.Caption1).X;
            Typography.Draw(drawList, new Vector2(textLeft + seatsWidth + 10f * scale, row.Min.Y + 50f * scale),
                view.Spectators, ui.MutedInk, TextStyles.Caption1);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static float DrawBadge(ImDrawListPtr drawList, in Rect row, AppSkin ui, in TableRowView view, float scale)
    {
        var label = BadgeLabel(view);
        if (label.Length == 0)
        {
            return 0f;
        }

        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var chipHeight = labelSize.Y + 6f * scale;
        var chipMax = new Vector2(row.Max.X - 14f * scale, row.Min.Y + 12f * scale + chipHeight);
        var chipMin = new Vector2(chipMax.X - labelSize.X - 16f * scale, row.Min.Y + 12f * scale);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f * scale);
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label,
            view.Full || view.Draining ? ui.MutedInk : ui.Accent, TextStyles.Caption1);
        return chipMax.X - chipMin.X + 12f * scale;
    }

    private static string BadgeLabel(in TableRowView view)
    {
        if (view.Draining)
        {
            return Loc.T(L.Casino.TableClosingBadge);
        }

        if (view.Mine)
        {
            return Loc.T(L.Casino.TableYoursBadge);
        }

        if (view.InviteOnly)
        {
            return Loc.T(L.Casino.TablePrivateBadge);
        }

        return view.Full ? Loc.T(L.Casino.TableFullBadge) : string.Empty;
    }
}
