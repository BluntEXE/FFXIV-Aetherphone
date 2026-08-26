using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class ComposeFab
{
    private const float DefaultRadius = 26f;
    private const float GlowReach = 14f;
    private const int GlowRings = 10;
    private const float GlowRingAlpha = 0.035f;
    private const float HoverGrow = 1.07f;
    private const float PressShrink = 0.95f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static bool Draw(Rect area, string childId, Vector4 accent, string glyph, string tooltip,
        string? anchorKey = null, Vector4? gradientBottom = null, float radiusUnscaled = DefaultRadius,
        VectorIcon? vectorGlyph = null)
    {
        var scale = UiScale.Current;
        var radius = radiusUnscaled * scale;
        var margin = 18f * scale;
        var glowPad = gradientBottom is null ? 0f : GlowReach * scale;
        var boxSize = radius * 2f + margin + glowPad;
        var boxMin = new Vector2(area.Max.X - boxSize, area.Max.Y - boxSize);
        ImGui.SetCursorScreenPos(boxMin);
        using var overlay = ImRaii.Child(childId, new Vector2(boxSize, boxSize), false,
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var center = new Vector2(area.Max.X - radius - margin, area.Max.Y - radius - margin);
        var fabRect = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        if (anchorKey is not null)
        {
            UiAnchors.Report(anchorKey, fabRect);
        }

        var drawList = ImGui.GetWindowDrawList();
        var hovered = !InputShield.Active && UiInteract.HoverOverlay(fabRect);
        if (gradientBottom is { } deep)
        {
            var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var drawRadius = radius * (pressed ? PressShrink : hovered ? HoverGrow : 1f);
            DrawGradientBody(drawList, center, drawRadius, hovered ? Palette.Mix(accent, White, 0.08f) : accent, deep,
                scale);
        }
        else
        {
            drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), radius,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 32);
            drawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32(hovered ? Palette.Mix(accent, White, 0.12f) : accent), 32);
        }

        if (vectorGlyph is not null)
        {
            vectorGlyph.Stroke(drawList, center, radius * 0.82f, ImGui.GetColorU32(White), 1.9f);
        }
        else
        {
            AppSkin.Icon(center, glyph, White, 1.1f);
        }

        HoverTooltip.Show(fabRect, tooltip, HoverLabelSide.Above);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawGradientBody(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 top,
        Vector4 bottom, float scale)
    {
        for (var ring = GlowRings; ring >= 1; ring--)
        {
            var reach = GlowReach * scale * ring / GlowRings;
            drawList.AddCircleFilled(center, radius + reach, ImGui.GetColorU32(Palette.WithAlpha(top, GlowRingAlpha)),
                48);
        }
        Squircle.FillCircleVerticalGradient(drawList, center, radius, ImGui.GetColorU32(top),
            ImGui.GetColorU32(bottom));
        drawList.AddCircleFilled(center - new Vector2(0f, radius * 0.42f), radius * 0.55f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 32);
    }
}
