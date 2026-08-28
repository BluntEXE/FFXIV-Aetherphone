using Aetherphone.Core;
using Aetherphone.Core.Onboarding;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal readonly record struct FeedControls(int Selected, bool Refreshed, bool MediaToggled, Rect MediaBounds);

internal static class FeedControlRow
{
    public const float Height = 38f;

    private const float SideMargin = 16f;
    private const float ControlGap = 8f;
    private const float SpinnerRadius = 8f;
    private const int SpinnerSegments = 24;

    public static FeedControls Draw(Rect area, AppSkin ui, Vector4 accent, string leftLabel, string rightLabel,
        int selected, ref float animation, bool loading, bool mediaOn, string refreshTooltip, string mediaTooltip,
        string anchorId = "")
    {
        var scale = UiScale.Current;
        var radius = area.Height * 0.5f;
        var gap = ControlGap * scale;
        var margin = SideMargin * scale;
        var extent = new Vector2(radius, radius);
        var mediaCenter = new Vector2(area.Max.X - margin - radius, area.Center.Y);
        var refreshCenter = new Vector2(mediaCenter.X - radius * 2f - gap, area.Center.Y);
        var tabsRect = new Rect(new Vector2(area.Min.X + margin, area.Min.Y),
            new Vector2(refreshCenter.X - radius - gap, area.Max.Y));
        if (anchorId.Length > 0)
        {
            UiAnchors.Report(anchorId, tabsRect);
        }

        var mediaToggled = ui.IconButton(mediaCenter, radius, IconGlyph.Of(FontAwesomeIcon.Filter),
            mediaOn ? accent : ui.MutedInk, ui.FieldSurface, 1.1f, mediaTooltip, HoverLabelSide.Below);
        var refreshed = false;
        if (loading)
        {
            ImGui.GetWindowDrawList().AddCircleFilled(refreshCenter, radius, ImGui.GetColorU32(ui.FieldSurface),
                SpinnerSegments);
            LoadingPulse.Spinner(refreshCenter, SpinnerRadius * scale, ui.Accent);
        }
        else
        {
            refreshed = ui.IconButton(refreshCenter, radius, IconGlyph.Of(FontAwesomeIcon.Sync), ui.BodyInk,
                ui.FieldSurface, 1.1f, refreshTooltip, HoverLabelSide.Below);
        }

        var picked = SegmentSlider.Draw(tabsRect, leftLabel, rightLabel, selected, ref animation, accent, ui.MutedInk);
        return new FeedControls(picked, refreshed, mediaToggled,
            new Rect(mediaCenter - extent, mediaCenter + extent));
    }
}
