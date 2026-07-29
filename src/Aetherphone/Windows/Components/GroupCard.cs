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
    private int rowIndex;
    private float lastRowTop;
    private float lastRowBottom;
    private int lastRowIndex;
    private int lastRowSpan;

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
        rowIndex = 0;
        lastRowTop = startY;
        lastRowBottom = startY;
        lastRowIndex = 0;
        lastRowSpan = 0;
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
        if (rowIndex > 0)
        {
            var separatorX = left + Metrics.Space.Lg * scale;
            ImGui.GetWindowDrawList().AddLine(new Vector2(separatorX, rowTop), new Vector2(right, rowTop),
                ImGui.GetColorU32(theme.Separator), Metrics.Stroke.Hairline);
        }

        lastRowIndex = rowIndex;
        lastRowSpan = rowSpan;
        lastRowTop = rowTop;
        lastRowBottom = rowTop + rowSpan * rowHeight * scale;
        rowIndex += rowSpan;
        var padding = Metrics.Space.Lg * scale;
        return new Rect(new Vector2(left + padding, rowTop),
            new Vector2(right - padding, rowTop + rowSpan * rowHeight * scale));
    }

    // Draws the hover/press overlay for the row most recently returned by NextRow, full-bleed to
    // the card's own edges (not inset by row padding) so the highlight covers the entire row with
    // no visible gap — and rounded to match the card's corner radius only where the row actually
    // touches the card's rounded top/bottom edge, so a square corner never pokes past the card's
    // squircle outline on the first/last row.
    public void DrawHoverHighlight(Vector4 color)
    {
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
        ImGui.SetCursorScreenPos(new Vector2(left, startY));
        ImGui.Dummy(new Vector2(right - left, rowCount * rowHeight * scale));
    }
}
