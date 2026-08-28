using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal sealed class TileRail
{
    private const float DragSlop = 5f;

    private float offset;
    private float maxOffset;
    private bool dragging;
    private float dragTravel;
    private float lastMouseX;

    public float Offset => offset;

    public bool TapAllowed => dragTravel <= DragSlop * UiScale.Current;

    public bool Begin(ImDrawListPtr drawList, Rect row, float contentWidth)
    {
        maxOffset = MathF.Max(0f, contentWidth - row.Width);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            UiInteract.ReportGestureSurface();
        }

        HandleDrag(row, hovered);
        drawList.PushClipRect(row.Min, row.Max, true);
        return hovered;
    }

    public static void End(ImDrawListPtr drawList) => drawList.PopClipRect();

    public void Reset()
    {
        offset = 0f;
        dragging = false;
        dragTravel = 0f;
    }

    private void HandleDrag(Rect row, bool hovered)
    {
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            dragTravel = 0f;
            lastMouseX = ImGui.GetIO().MousePos.X;
        }

        if (dragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var mouseX = ImGui.GetIO().MousePos.X;
            var travel = mouseX - lastMouseX;
            lastMouseX = mouseX;
            dragTravel += MathF.Abs(travel);
            if (dragTravel > DragSlop * UiScale.Current)
            {
                offset -= travel;
            }
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            dragging = false;
        }

        offset = Math.Clamp(offset, 0f, maxOffset);
    }
}
