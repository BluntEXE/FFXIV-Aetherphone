using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ListSection
{
    public static void Header(string title, Vector4 ink) => Header(title, ink, null, null);

    public static void Header(string title, Vector4 ink, PhoneTheme? theme, string? hint)
    {
        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var label = Loc.Culture.TextInfo.ToUpper(title);
        var origin = ImGui.GetCursorScreenPos();
        var left = origin.X + Metrics.Space.Lg * scale;
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(left, origin.Y), label, ink,
            TextStyles.FootnoteEmphasized);
        if (theme is not null && hint is not null)
        {
            var iconCenter = new Vector2(left + size.X + Metrics.Size.HintIconHeight * 0.5f * scale
                + Metrics.Space.Sm * scale, origin.Y + size.Y * 0.5f);
            HintIcon.Draw(iconCenter, hint, theme, scale);
        }

        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, size.Y));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }
}
