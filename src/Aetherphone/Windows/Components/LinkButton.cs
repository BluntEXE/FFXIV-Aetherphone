using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class LinkButton
{
    private const float ButtonHeight = 46f;
    private const float SideInset = 14f;
    private const float IconGap = 11f;

    public static bool Draw(FontAwesomeIcon icon, string label, Vector4 color)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var inset = SideInset * scale;
        var slotOrigin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail().X;
        var origin = new Vector2(slotOrigin.X + inset, slotOrigin.Y);
        var size = new Vector2(available - inset * 2f, ButtonHeight * scale);
        var end = origin + size;
        var hovered = UiInteract.Hover(origin, end);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var rounding = size.Y * 0.5f;

        var fill = (hovered ? Palette.Lighten(color, 0.12f) : color) with { W = 1f };
        if (pressed)
        {
            fill = Palette.Darken(fill, 0.08f);
        }

        Squircle.Fill(drawList, origin, end, rounding, ImGui.GetColorU32(fill));
        drawList.AddLine(new Vector2(origin.X + rounding, origin.Y + 1.5f * scale),
            new Vector2(end.X - rounding, origin.Y + 1.5f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.20f)), 1f);
        Squircle.Stroke(drawList, origin, end, rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, hovered ? 0.38f : 0.16f)), 1f * scale);
        DrawContent(drawList, origin, size, icon, label, scale);

        ImGui.SetCursorScreenPos(slotOrigin);
        ImGui.Dummy(new Vector2(available, size.Y));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(origin, end, hovered);
    }

    private static void DrawContent(ImDrawListPtr drawList, Vector2 origin, Vector2 size, FontAwesomeIcon icon,
        string label, float scale)
    {
        var ink = new Vector4(1f, 1f, 1f, 1f);
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            iconSize = ImGui.CalcTextSize(IconGlyph.Of(icon));
        }

        var labelSize = Typography.Measure(label, TextStyles.Headline);
        var innerGap = IconGap * scale;
        var contentWidth = iconSize.X + innerGap + labelSize.X;
        var startX = origin.X + (size.X - contentWidth) * 0.5f;
        var midY = origin.Y + size.Y * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(startX + iconSize.X * 0.5f, midY), icon, ink, iconSize.Y);
        Typography.Draw(drawList, new Vector2(startX + iconSize.X + innerGap, midY - labelSize.Y * 0.5f), label, ink,
            TextStyles.Headline);
    }
}
