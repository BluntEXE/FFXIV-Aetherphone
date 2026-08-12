using Aetherphone.Core;
using Aetherphone.Core.Onboarding;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class NotificationToggleButton
{
    public static bool Draw(Rect content, float scale, string anchorKey, AlertSuppression suppresses, bool suppressed,
        Vector4 accent, Vector4 strong, Vector4 muted, string tooltipWhenSuppressed, string tooltipWhenActive)
    {
        var center = new Vector2(content.Max.X - 22f * scale, content.Min.Y + AppHeader.Height * scale * 0.5f);
        var radius = 16f * scale;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max);
        var color = suppressed ? accent : hovered ? strong : muted;
        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), center, IconFor(suppresses, suppressed), color, 15f * scale);
        var toggleRect = new Rect(min, max);
        UiAnchors.Report(anchorKey, toggleRect);
        HoverTooltip.Show(toggleRect, suppressed ? tooltipWhenSuppressed : tooltipWhenActive);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static FontAwesomeIcon IconFor(AlertSuppression suppresses, bool suppressed)
    {
        if (suppresses == AlertSuppression.Badge)
        {
            return suppressed ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye;
        }

        return suppressed ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell;
    }
}
