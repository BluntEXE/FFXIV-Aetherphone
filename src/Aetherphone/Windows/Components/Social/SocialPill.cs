using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SocialPill
{
    private const float DisabledAlpha = 0.45f;
    private const float DisabledInkAlpha = 0.7f;

    public static bool Accent(ImDrawListPtr drawList, Rect rect, string label, SocialInk ink, in TextStyle style,
        float rounding, bool enabled = true)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        if (enabled)
        {
            AccentPill.Paint(drawList, rect.Min, rect.Max, rounding, hovered, ink.Accent, ink.AccentDeep,
                ink.AccentShadow);
        }
        else
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ink.Accent, DisabledAlpha)));
        }

        var fitted = Typography.FitText(label, MathF.Max(1f, rect.Width - rect.Height * 0.6f), style);
        Typography.DrawCentered(drawList, rect.Center, fitted,
            enabled ? ink.White : Palette.WithAlpha(ink.White, DisabledInkAlpha), style);
        return Finish(rect, hovered);
    }

    public static bool Outline(ImDrawListPtr drawList, Rect rect, string label, SocialInk ink, in TextStyle style,
        float rounding, Vector4 fill)
    {
        var scale = UiScale.Current;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(hovered ? ink.AccentWash : fill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ink.AccentLink), 1.5f * scale);
        var fitted = Typography.FitText(label, MathF.Max(1f, rect.Width - rect.Height * 0.6f), style);
        Typography.DrawCentered(drawList, rect.Center, fitted, ink.AccentLink, style);
        return Finish(rect, hovered);
    }

    public static bool Flat(ImDrawListPtr drawList, Rect rect, string label, Vector4 fill, Vector4 hoverFill,
        Vector4 stroke, Vector4 labelInk, in TextStyle style, float rounding)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(hovered ? hoverFill : fill));
        if (stroke.W > 0f)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(stroke), 1f);
        }

        var fitted = Typography.FitText(label, MathF.Max(1f, rect.Width - rect.Height * 0.6f), style);
        Typography.DrawCentered(drawList, rect.Center, fitted, labelInk, style);
        return Finish(rect, hovered);
    }

    public static bool Icon(ImDrawListPtr drawList, Rect rect, string glyph, string tooltip, Vector4 fill,
        Vector4 hoverFill, Vector4 glyphInk, float iconSize, float rounding)
    {
        var scale = UiScale.Current;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(hovered ? hoverFill : fill));
        PhoneIcon.Draw(drawList, rect.Center, glyph, glyphInk, iconSize * scale);
        HoverTooltip.Show(rect, tooltip, HoverLabelSide.Below);
        return Finish(rect, hovered);
    }

    private static bool Finish(Rect rect, bool hovered)
    {
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
