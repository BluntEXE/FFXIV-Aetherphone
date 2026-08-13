using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class SettingsSection
{
    public static void Header(string title, PhoneTheme theme, string? hint = null)
    {
        var scale = UiScale.Current;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var label = Loc.Culture.TextInfo.ToUpper(title);
        var origin = ImGui.GetCursorScreenPos();
        var left = origin.X + Metrics.Space.Lg * scale;
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(left, origin.Y), label, theme.TextMuted,
            TextStyles.FootnoteEmphasized);
        if (hint != null)
        {
            var iconCenter = new Vector2(left + size.X + Metrics.Size.HintIconHeight * 0.5f * scale
                + Metrics.Space.Sm * scale, origin.Y + size.Y * 0.5f);
            HintIcon.Draw(iconCenter, hint, theme, scale);
        }

        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, size.Y));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    public static void Hint(string text, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Metrics.Space.Lg * scale);
        using (Plugin.Fonts.Push(TextStyles.Footnote.Scale, TextStyles.Footnote.Weight))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.PushTextWrapPos(0f);
            Typography.Wrapped(text);
            ImGui.PopTextWrapPos();
        }
    }
}
