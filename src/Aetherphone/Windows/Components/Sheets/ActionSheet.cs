using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct ActionSheetStyle(
    Vector4 Panel,
    Vector4 Stroke,
    Vector4 Ink,
    Vector4 Danger,
    Vector4 Accent,
    Vector4 Hairline)
{
    public static ActionSheetStyle From(PhoneTheme theme) => new(
        Palette.WithAlpha(Palette.Lighten(theme.AppBackground, 0.10f), 0.92f),
        Palette.WithAlpha(theme.TextStrong, 0.12f),
        theme.TextStrong,
        theme.Danger,
        theme.Accent,
        theme.Hairline);

    public static ActionSheetStyle From(AppSkin ui) => new(
        Palette.WithAlpha(Palette.Lighten(ui.Palette.BackdropTop, 0.10f), 0.92f),
        Palette.WithAlpha(ui.TitleInk, 0.12f),
        ui.TitleInk,
        ui.Theme.Danger,
        ui.Accent,
        ui.Hairline);
}

internal sealed class ActionSheet
{
    public readonly record struct Item(string Label, string Glyph = "", bool Danger = false, bool Selected = false,
        bool Checkable = false);

    private const float RevealSmoothTime = 0.11f;
    private const float MaxDim = 0.45f;
    private const float Margin = 10f;
    private const float Rounding = 20f;
    private const float RowHeight = 50f;
    private const float CancelHeight = 52f;
    private const float CardGap = 8f;
    private const float BottomInset = 12f;
    private const float PadX = 18f;
    private const float GlyphReserve = 30f;
    private const float CheckReserve = 26f;
    private const float HeaderPadY = 13f;
    private const float HeaderInkAlpha = 0.65f;

    private static readonly TextStyle RowStyle = new(1.07f, FontWeight.SemiBold);
    private static readonly TextStyle CancelStyle = new(1.07f, FontWeight.Bold);
    private static readonly TextStyle HeaderStyle = new(0.82f, FontWeight.SemiBold);
    private static readonly Vector4 RowHover = new(1f, 1f, 1f, 0.06f);

    private Spring reveal;
    private bool open;
    private int openedFrame;

    public bool IsOpen => open;

    public bool CapturesPointer => open || !reveal.IsResting(0f, 0.001f, 0.005f);

    public void Open()
    {
        if (open)
        {
            return;
        }

        open = true;
        openedFrame = ImGui.GetFrameCount();
    }

    public void Close() => open = false;

    public void Gate()
    {
        if (open)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public int Draw(Rect screen, in ActionSheetStyle style, ReadOnlySpan<Item> items, string cancelLabel,
        bool keepOpen, string title = "")
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return -1;
        }

        if (items.Length == 0)
        {
            return -1;
        }

        var scale = UiScale.Current;
        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        var slide = Easing.EaseOutQuint(opacity);
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, false);
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
        var margin = Margin * scale;
        var rowHeight = RowHeight * scale;
        var cancelHeight = CancelHeight * scale;
        var gap = CardGap * scale;
        var padX = PadX * scale;
        var headerWidth = screen.Width - margin * 2f - padX * 2f;
        var titleHeight = title.Length > 0 ? Typography.MeasureWrappedBlock(title, HeaderStyle, headerWidth).Y : 0f;
        var headerHeight = titleHeight > 0f ? titleHeight + HeaderPadY * 2f * scale : 0f;
        var cardHeight = headerHeight + items.Length * rowHeight;
        var total = cardHeight + gap + cancelHeight;
        var bottom = screen.Max.Y - BottomInset * scale + total * (1f - slide);
        var left = screen.Min.X + margin;
        var right = screen.Max.X - margin;
        var cancelMin = new Vector2(left, bottom - cancelHeight);
        var cancelMax = new Vector2(right, bottom);
        var cardMax = new Vector2(right, cancelMin.Y - gap);
        var cardMin = new Vector2(left, cardMax.Y - cardHeight);
        var rounding = Rounding * scale;
        var interactive = open && opacity > 0.5f;
        DrawPanel(drawList, cardMin, cardMax, rounding, style, opacity, scale);
        if (headerHeight > 0f)
        {
            var headerInk = Palette.WithAlpha(style.Ink, style.Ink.W * HeaderInkAlpha * opacity);
            Typography.DrawWrappedCentered(drawList,
                new Vector2((cardMin.X + cardMax.X) * 0.5f, cardMin.Y + HeaderPadY * scale + titleHeight * 0.5f),
                title, headerInk, HeaderStyle, headerWidth);
        }

        var anyGlyph = false;
        var anyCheck = false;
        for (var index = 0; index < items.Length; index++)
        {
            anyGlyph |= items[index].Glyph.Length > 0;
            anyCheck |= items[index].Checkable;
        }

        var picked = -1;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var rowMin = new Vector2(cardMin.X, cardMin.Y + headerHeight + index * rowHeight);
            var rowMax = new Vector2(cardMax.X, rowMin.Y + rowHeight);
            var hovered = interactive && UiInteract.HoverWindowOnly(rowMin, rowMax);
            if (hovered)
            {
                var first = index == 0 && headerHeight <= 0f;
                var last = index == items.Length - 1;
                var flags = first && last ? ImDrawFlags.RoundCornersAll
                    : first ? ImDrawFlags.RoundCornersTop
                    : last ? ImDrawFlags.RoundCornersBottom
                    : ImDrawFlags.RoundCornersNone;
                drawList.AddRectFilled(rowMin, rowMax,
                    ImGui.GetColorU32(Palette.WithAlpha(RowHover, RowHover.W * opacity)), rounding, flags);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    picked = index;
                }
            }

            if (index > 0 || headerHeight > 0f)
            {
                drawList.AddLine(new Vector2(rowMin.X + padX, rowMin.Y), new Vector2(rowMax.X - padX, rowMin.Y),
                    ImGui.GetColorU32(Palette.WithAlpha(style.Hairline, style.Hairline.W * opacity)), 1f);
            }

            var ink = item.Danger ? style.Danger : item.Checkable && item.Selected ? style.Accent : style.Ink;
            var faded = Palette.WithAlpha(ink, ink.W * opacity);
            var centerY = (rowMin.Y + rowMax.Y) * 0.5f;
            var textLeft = rowMin.X + padX;
            if (anyGlyph)
            {
                if (item.Glyph.Length > 0)
                {
                    AppSkin.Icon(drawList, new Vector2(textLeft + 9f * scale, centerY), item.Glyph, faded, 0.95f);
                }

                textLeft += GlyphReserve * scale;
            }

            var textRight = rowMax.X - padX - (anyCheck ? CheckReserve * scale : 0f);
            var label = Typography.FitText(item.Label, MathF.Max(1f, textRight - textLeft), RowStyle);
            var labelSize = Typography.Measure(label, RowStyle);
            var labelX = anyGlyph || anyCheck ? textLeft : (rowMin.X + rowMax.X - labelSize.X) * 0.5f;
            Typography.Draw(drawList, new Vector2(labelX, centerY - labelSize.Y * 0.5f), label, faded, RowStyle);
            if (item.Checkable && item.Selected)
            {
                DrawCheck(drawList, new Vector2(rowMax.X - padX - 6f * scale, centerY), style.Accent, opacity, scale);
            }
        }

        DrawPanel(drawList, cancelMin, cancelMax, rounding, style, opacity, scale);
        var cancelHovered = interactive && UiInteract.HoverWindowOnly(cancelMin, cancelMax);
        if (cancelHovered)
        {
            drawList.AddRectFilled(cancelMin, cancelMax,
                ImGui.GetColorU32(Palette.WithAlpha(RowHover, RowHover.W * opacity)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, new Vector2((cancelMin.X + cancelMax.X) * 0.5f,
                (cancelMin.Y + cancelMax.Y) * 0.5f), cancelLabel, Palette.WithAlpha(style.Ink, style.Ink.W * opacity),
            CancelStyle);
        drawList.PopClipRect();

        if (picked >= 0)
        {
            if (!keepOpen)
            {
                Close();
            }

            return picked;
        }

        if (cancelHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            Close();
            return -1;
        }

        if (interactive && ImGui.GetFrameCount() != openedFrame && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !UiInteract.HoverWindowOnly(cardMin, cancelMax, false))
        {
            Close();
        }

        return -1;
    }

    private static void DrawPanel(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding,
        in ActionSheetStyle style, float opacity, float scale)
    {
        Elevation.Floating(drawList, min, max, rounding, scale, opacity);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(style.Panel, style.Panel.W * opacity)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(style.Stroke, style.Stroke.W * opacity)), Metrics.Stroke.Hairline);
    }

    private static void DrawCheck(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float alpha, float scale)
    {
        var color = ImGui.GetColorU32(Palette.WithAlpha(accent, accent.W * alpha));
        var thickness = 2f * scale;
        drawList.AddLine(center + new Vector2(-5f * scale, 0f), center + new Vector2(-1.5f * scale, 3.6f * scale),
            color, thickness);
        drawList.AddLine(center + new Vector2(-1.5f * scale, 3.6f * scale),
            center + new Vector2(5.2f * scale, -4f * scale), color, thickness);
    }
}
