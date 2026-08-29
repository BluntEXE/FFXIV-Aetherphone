using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct HeaderActions
{
    public const float GlyphScale = 1.2f;

    private const float SlotPitch = 34f;
    private const float SlotRadius = 16f;
    private const float EdgeInset = 20f;
    private const float TitleGap = 10f;

    private readonly float rightSlotX;
    private readonly float pitch;

    public HeaderActions(Rect area, float scale, int slotCount)
    {
        var radius = SlotRadius * scale;
        var slotPitch = SlotPitch * scale;
        var rightmost = area.Max.X - EdgeInset * scale;
        rightSlotX = rightmost;
        pitch = slotPitch;
        RowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Radius = radius;
        TitleLimit = slotCount <= 0
            ? rightmost + radius
            : rightmost - (slotCount - 1) * slotPitch - radius - TitleGap * scale;
    }

    public float RowCenterY { get; }

    public float Radius { get; }

    public float TitleLimit { get; }

    public Vector2 Slot(int index) => new(rightSlotX - index * pitch, RowCenterY);

    public Rect Bounds(int index)
    {
        var center = Slot(index);
        var extent = new Vector2(Radius, Radius);
        return new Rect(center - extent, center + extent);
    }
}

internal static class HeaderTitle
{
    private const float PaddingX = 8f;
    private const float PaddingY = 6f;

    private static readonly TextStyle Style = new(1.3f, FontWeight.Bold);

    public static bool Draw(string id, string text, float left, in HeaderActions actions, Vector4 ink, float scale) =>
        Draw(id, text, left, actions, ink, scale, Style);

    public static bool Draw(string id, string text, float left, in HeaderActions actions, Vector4 ink, float scale,
        in TextStyle style)
    {
        var maxWidth = MathF.Max(1f, actions.TitleLimit - left);
        var size = Typography.Measure(text, style);
        var clampedWidth = MathF.Min(size.X, maxWidth);
        var padding = new Vector2(PaddingX * scale, PaddingY * scale);
        var top = actions.RowCenterY - size.Y * 0.5f;
        var min = new Vector2(left, top) - padding;
        var max = new Vector2(left + clampedWidth, top + size.Y) + padding;
        UiInteract.HoverHighlight(ImGui.GetWindowDrawList(), min, max, (max.Y - min.Y) * 0.5f);
        Marquee.DrawLeftAuto(id, text, left, top, maxWidth, style, ink);
        return UiInteract.HoverClick(min, max);
    }
}
