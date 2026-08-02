using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class HintIcon
{
    internal static void Draw(Vector2 position, string hintText, PhoneTheme theme, float scale)
    {
        var iconSize = Metrics.Size.HintIconHeight * scale;
        var hitRadius = iconSize * 0.65f;
        
        ProgressRing.CenterIcon(position, FontAwesomeIcon.QuestionCircle, theme.TextMuted, iconSize);
        
        var hitRadiusVector = new Vector2(hitRadius, hitRadius);
        var hitRect = new Rect(position - hitRadiusVector, position + hitRadiusVector);
        
        if (UiInteract.Hover(hitRect.Min, hitRect.Max))
        {
            HoverTooltip.Show(hitRect, hintText);
        }
    }
}
