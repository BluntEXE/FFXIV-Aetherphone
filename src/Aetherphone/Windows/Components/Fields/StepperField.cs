using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class StepperField
{
    public static void Draw(PhoneTheme theme, Rect rect, string valueText, float scale, Action onDecrement,
        Action onIncrement)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale, scale);
        var chevronWidth = MathF.Min(34f * scale, rect.Width * 0.3f);
        var leftRect = new Rect(rect.Min, new Vector2(rect.Min.X + chevronWidth, rect.Max.Y));
        var rightRect = new Rect(new Vector2(rect.Max.X - chevronWidth, rect.Min.Y), rect.Max);
        if (DrawChevron(theme, drawList, leftRect, "<", scale))
        {
            onDecrement();
        }

        if (DrawChevron(theme, drawList, rightRect, ">", scale))
        {
            onIncrement();
        }

        Typography.DrawCentered(drawList, rect.Center, valueText, theme.TextStrong, TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
    }

    private static bool DrawChevron(PhoneTheme theme, ImDrawListPtr drawList, Rect rect, string chevron, float scale)
    {
        if (UiInteract.Hover(rect.Min, rect.Max))
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f)));
        }

        Typography.DrawCentered(drawList, rect.Center, chevron, theme.TextMuted, TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
        return UiInteract.HoverClick(rect.Min, rect.Max);
    }

    public static void Draw(AppSkin ui, Rect rect, string valueText, float scale, Action onDecrement,
        Action onIncrement)
    {
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale);
        var chevronWidth = MathF.Min(34f * scale, rect.Width * 0.3f);
        var leftRect = new Rect(rect.Min, new Vector2(rect.Min.X + chevronWidth, rect.Max.Y));
        var rightRect = new Rect(new Vector2(rect.Max.X - chevronWidth, rect.Min.Y), rect.Max);
        if (DrawChevron(ui, drawList, leftRect, "<", scale))
        {
            onDecrement();
        }

        if (DrawChevron(ui, drawList, rightRect, ">", scale))
        {
            onIncrement();
        }

        Typography.DrawCentered(drawList, rect.Center, valueText, ui.TitleInk, TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
    }

    private static bool DrawChevron(AppSkin ui, ImDrawListPtr drawList, Rect rect, string chevron, float scale)
    {
        if (UiInteract.Hover(rect.Min, rect.Max))
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.HoverTint));
        }

        Typography.DrawCentered(drawList, rect.Center, chevron, ui.MutedInk, TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
        return UiInteract.HoverClick(rect.Min, rect.Max);
    }
}
