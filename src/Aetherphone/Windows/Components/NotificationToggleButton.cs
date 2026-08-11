using Aetherphone.Core;
using Aetherphone.Core.Onboarding;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class NotificationToggleButton
{
    public static bool Draw(Rect content, float scale, string anchorKey, bool paused, Vector4 accent,
        Vector4 strong, Vector4 muted, string tooltipWhenPaused, string tooltipWhenActive)
    {
        var center = new Vector2(content.Max.X - 22f * scale, content.Min.Y + AppHeader.Height * scale * 0.5f);
        var radius = 16f * scale;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max);
        var color = paused ? accent : hovered ? strong : muted;
        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), center,
            paused ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell, color, 15f * scale);
        var toggleRect = new Rect(min, max);
        UiAnchors.Report(anchorKey, toggleRect);
        HoverTooltip.Show(toggleRect, paused ? tooltipWhenPaused : tooltipWhenActive);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
