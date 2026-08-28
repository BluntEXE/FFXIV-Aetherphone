using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings;

internal static class ThemeButton
{
    private const float NeutralHeight = 34f;
    private const float PrimaryHeight = 38f;
    private const float GhostHeight = 32f;

    public static bool Draw(string label, PhoneTheme theme, float width = -1f)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, theme.GroupedCard)
                   .Push(ImGuiCol.ButtonHovered, Palette.Mix(theme.GroupedCard, theme.Accent, 0.35f))
                   .Push(ImGuiCol.ButtonActive, theme.Accent).Push(ImGuiCol.Text, theme.TextStrong))
        {
            return ImGui.Button(label, new Vector2(width, NeutralHeight * UiScale.Current));
        }
    }

    public static bool Primary(string label, PhoneTheme theme, bool enabled = true)
    {
        var accent = enabled ? theme.Accent : Palette.WithAlpha(theme.Accent, 0.4f);
        using (ImRaii.PushColor(ImGuiCol.Button, accent)
                   .Push(ImGuiCol.ButtonHovered, enabled ? Palette.Mix(theme.Accent, theme.TextStrong, 0.14f) : accent)
                   .Push(ImGuiCol.ButtonActive,
                       enabled ? Palette.Mix(theme.Accent, new Vector4(0f, 0f, 0f, 1f), 0.18f) : accent)
                   .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, enabled ? 1f : 0.72f)))
        {
            var clicked = ImGui.Button(label, new Vector2(-1f, PrimaryHeight * UiScale.Current));
            return clicked && enabled;
        }
    }

    public static bool Ghost(string label, PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, Palette.WithAlpha(theme.TextStrong, 0f))
                   .Push(ImGuiCol.ButtonHovered, Palette.WithAlpha(theme.TextStrong, 0.08f))
                   .Push(ImGuiCol.ButtonActive, Palette.WithAlpha(theme.TextStrong, 0.14f))
                   .Push(ImGuiCol.Text, theme.TextMuted))
        {
            return ImGui.Button(label, new Vector2(-1f, GhostHeight * UiScale.Current));
        }
    }
}
