using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal struct GroupCard
{
    public const float DefaultRowHeight = Metrics.Size.Row;
    private readonly PhoneTheme theme;
    private readonly float scale;
    private readonly float rowHeight;
    private readonly float left;
    private readonly float right;
    private readonly float startY;
    private readonly int rowCount;
    private readonly float[] callTops;
    private readonly float[] callBottoms;
    private readonly bool[] callHovered;
    private int rowIndex;
    private int callCount;
    private float lastRowTop;
    private float lastRowBottom;
    private int lastRowIndex;
    private int lastRowSpan;
    private int lastCallIndex;

    private GroupCard(PhoneTheme theme, float scale, float rowHeight, float left, float right, float startY,
        int rowCount)
    {
        this.theme = theme;
        this.scale = scale;
        this.rowHeight = rowHeight;
        this.left = left;
        this.right = right;
        this.startY = startY;
        this.rowCount = rowCount;
        // Sized to rowCount: worst case every NextRow() call has rowSpan == 1, so the number of
        // calls (and thus boundaries to track) never exceeds rowCount.
        callTops = new float[rowCount];
        callBottoms = new float[rowCount];
        callHovered = new bool[rowCount];
        rowIndex = 0;
        callCount = 0;
        lastRowTop = startY;
        lastRowBottom = startY;
        lastRowIndex = 0;
        lastRowSpan = 0;
        lastCallIndex = 0;
    }

    public static GroupCard Begin(PhoneTheme theme, int rowCount, float rowHeight = DefaultRowHeight)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var height = rowCount * rowHeight * scale;
        var cardMax = new Vector2(right, origin.Y + height);
        var dl = ImGui.GetWindowDrawList();
        Squircle.Fill(dl, origin, cardMax, Metrics.Radius.Md * scale, ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(dl, origin, cardMax, Metrics.Radius.Md * scale, scale);
        return new GroupCard(theme, scale, rowHeight, origin.X, right, origin.Y, rowCount);
    }

    public Rect NextRow(int rowSpan = 1)
    {
        var rowTop = startY + rowIndex * rowHeight * scale;
        var rowBottom = rowTop + rowSpan * rowHeight * scale;

        // Separators are no longer drawn here. A separator sitting between two rows can never be
        // made to line up pixel-perfectly with whichever adjacent row's hover fill happens to be
        // drawn (rounded-corner "cap" fills go through Squircle's AA convex-fill path, which feathers
        // its edges by roughly half a pixel — enough for the hairline beneath/above it to peek
        // through no matter the draw order). Instead we record this row's bounds and defer every
        // boundary separator to End(), where we skip drawing it if either adjacent row was hovered
        // this frame — so a separator and a hover fill can never visually coexist at a boundary.
        callTops[callCount] = rowTop;
        callBottoms[callCount] = rowBottom;
        lastCallIndex = callCount;
        callCount++;

        lastRowIndex = rowIndex;
        lastRowSpan = rowSpan;
        lastRowTop = rowTop;
        lastRowBottom = rowBottom;
        rowIndex += rowSpan;
        var padding = Metrics.Space.Lg * scale;
        return new Rect(new Vector2(left + padding, rowTop), new Vector2(right - padding, rowBottom));
    }

    // Draws the hover/press overlay for the row most recently returned by NextRow, full-bleed to
    // the card's own edges (not inset by row padding) so the highlight covers the entire row with
    // no visible gap — and rounded to match the card's corner radius only where the row actually
    // touches the card's rounded top/bottom edge, so a square corner never pokes past the card's
    // squircle outline on the first/last row.
    public void DrawHoverHighlight(Vector4 color)
    {
        callHovered[lastCallIndex] = true;

        var dl = ImGui.GetWindowDrawList();
        var min = new Vector2(left, lastRowTop);
        var max = new Vector2(right, lastRowBottom);
        var packed = ImGui.GetColorU32(color);
        var radius = Metrics.Radius.Md * scale;
        var isFirst = lastRowIndex == 0;
        var isLast = lastRowIndex + lastRowSpan >= rowCount;
        if (isFirst && isLast)
        {
            Squircle.Fill(dl, min, max, radius, packed);
        }
        else if (isFirst)
        {
            Squircle.FillCap(dl, min, max, radius, packed, top: true);
        }
        else if (isLast)
        {
            Squircle.FillCap(dl, min, max, radius, packed, top: false);
        }
        else
        {
            dl.AddRectFilled(min, max, packed);
        }
    }

    public void End()
    {
        // Draw every row-boundary separator now, skipping any boundary where either adjacent row
        // was hover-highlighted this frame. For consumers that never call DrawHoverHighlight, no
        // call index is ever marked hovered, so this draws exactly the same separators as before —
        // just later in the frame's draw list, which is visually identical since nothing else
        // renders in between that this reordering could affect.
        var dl = ImGui.GetWindowDrawList();
        var separatorX = left + Metrics.Space.Lg * scale;
        var separatorColor = ImGui.GetColorU32(theme.Separator);
        for (var i = 0; i < callCount - 1; i++)
        {
            if (callHovered[i] || callHovered[i + 1])
            {
                continue;
            }

            var boundaryY = callBottoms[i];
            dl.AddLine(new Vector2(separatorX, boundaryY), new Vector2(right, boundaryY), separatorColor,
                Metrics.Stroke.Hairline);
        }

        ImGui.SetCursorScreenPos(new Vector2(left, startY));
        ImGui.Dummy(new Vector2(right - left, rowCount * rowHeight * scale));
    }
}
