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
    private readonly bool showSeparators;
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
        int rowCount, bool showSeparators)
    {
        this.theme = theme;
        this.scale = scale;
        this.rowHeight = rowHeight;
        this.left = left;
        this.right = right;
        this.startY = startY;
        this.rowCount = rowCount;
        this.showSeparators = showSeparators;
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

    public static GroupCard Begin(PhoneTheme theme, int rowCount, float rowHeight = DefaultRowHeight,
        bool showSeparators = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        origin.Y = MathF.Round(origin.Y);
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var height = MathF.Round(rowCount * rowHeight * scale);
        var cardMax = new Vector2(right, origin.Y + height);
        var dl = ImGui.GetWindowDrawList();
        Squircle.Fill(dl, origin, cardMax, Metrics.Radius.Md * scale, ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(dl, origin, cardMax, Metrics.Radius.Md * scale, scale);
        return new GroupCard(theme, scale, rowHeight, origin.X, right, origin.Y, rowCount, showSeparators);
    }

    public Rect NextRow(int rowSpan = 1)
    {
        var rowTop = MathF.Round(startY + rowIndex * rowHeight * scale);
        var rowBottom = MathF.Round(rowTop + rowSpan * rowHeight * scale);

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
    // no visible gap. Previously this branched on first/last row and used Squircle.FillCap, which
    // draws the flat body and the rounded-corner cap as two separate overlapping fills (a 1.5px
    // double-blend zone, intentional so no seam shows between them) — fine for opaque fills, but a
    // low-alpha translucent hover color painted twice over that strip comes out visibly darker/
    // lighter than the rest of the fill, a faint line at exactly the boundary between the flat body
    // and the rounded cap. Instead, draw the CARD's own true shape once (same min/max/radius Begin
    // used) and clip it to just this row's bounds — the visible slice is naturally correct at outer
    // corners (it's the real card outline) and a perfectly clean cut at row boundaries (clipping
    // hides pixels, it doesn't blend two fills together), so there's nothing left to double-paint.
    public void DrawHoverHighlight(Vector4 color)
    {
        callHovered[lastCallIndex] = true;

        var dl = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        var radius = Metrics.Radius.Md * scale;
        var cardMin = new Vector2(left, startY);
        var cardMax = new Vector2(right, startY + rowCount * rowHeight * scale);
        dl.PushClipRect(new Vector2(left, lastRowTop), new Vector2(right, lastRowBottom), true);
        Squircle.Fill(dl, cardMin, cardMax, radius, packed);
        dl.PopClipRect();
    }

    public void End()
    {
        // Draw every row-boundary separator now, skipping any boundary where either adjacent row
        // was hover-highlighted this frame. For consumers that never call DrawHoverHighlight, no
        // call index is ever marked hovered, so this draws exactly the same separators as before —
        // just later in the frame's draw list, which is visually identical since nothing else
        // renders in between that this reordering could affect.
        if (showSeparators)
        {
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
        }

        ImGui.SetCursorScreenPos(new Vector2(left, startY));
        ImGui.Dummy(new Vector2(right - left, rowCount * rowHeight * scale));
    }
}
