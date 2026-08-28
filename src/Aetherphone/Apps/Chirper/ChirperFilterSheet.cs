using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed class ChirperFilterSheet
{
    public const int PhotosToggle = 0;
    public const int GifsToggle = 1;
    public const int ReplyMediaToggle = 2;
    public const int FirstRegion = 3;

    private const float RevealSmoothTime = 0.11f;
    private const float ToggleSmoothTime = 0.06f;
    private const float MaxDim = 0.45f;
    private const float Rounding = 24f;
    private const float PadX = 18f;
    private const float GrabberWidth = 38f;
    private const float GrabberHeight = 4.5f;
    private const float RowHeight = 52f;
    private const float TrackWidth = 46f;
    private const float TrackHeight = 28f;
    private const float KnobSize = 24f;
    private const float ChipHeight = 36f;
    private const float ChipGap = 8f;
    private const float DoneHeight = 44f;
    private const float BottomPad = 30f;

    private static readonly TextStyle TitleStyle = new(1.13f, FontWeight.Bold);
    private static readonly TextStyle RowStyle = new(1f, FontWeight.Regular);
    private static readonly TextStyle SectionStyle = new(0.83f, FontWeight.Bold);
    private static readonly TextStyle ChipStyle = new(0.9f, FontWeight.Bold);
    private static readonly TextStyle DoneStyle = new(1f, FontWeight.Bold);
    private static readonly Vector4 PanelFill = new(0.086f, 0.110f, 0.157f, 0.94f);
    private static readonly Vector4 ToggleOff = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 ToggleOn = new(0.188f, 0.820f, 0.345f, 1f);
    private static readonly Vector4 ChipOffFill = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 ChipOffStroke = new(1f, 1f, 1f, 0.09f);
    private static readonly Vector4 ChipOnFill = Palette.WithAlpha(ChirperInk.Accent, 0.18f);
    private static readonly Vector4 ChipOnStroke = Palette.WithAlpha(ChirperInk.Accent, 0.5f);
    private static readonly Vector4 GrabberFill = new(1f, 1f, 1f, 0.22f);

    private readonly Spring[] toggles = new Spring[FirstRegion];
    private Spring reveal;
    private bool open;
    private int openedFrame;

    public bool IsOpen => open;

    public bool CapturesPointer => open || !reveal.IsResting(0f, 0.001f, 0.005f);

    public void Open(bool photos, bool gifs, bool replyMedia)
    {
        if (!open)
        {
            toggles[PhotosToggle].SnapTo(photos ? 1f : 0f);
            toggles[GifsToggle].SnapTo(gifs ? 1f : 0f);
            toggles[ReplyMediaToggle].SnapTo(replyMedia ? 1f : 0f);
            openedFrame = ImGui.GetFrameCount();
        }

        open = true;
    }

    public void Close() => open = false;

    public void Gate()
    {
        if (open)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public int Draw(Rect screen, bool photos, bool gifs, bool replyMedia, int regionMask)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return -1;
        }

        var scale = UiScale.Current;
        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        var slide = Easing.EaseOutQuint(opacity);
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, false);
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
        var padX = PadX * scale;
        var rowHeight = RowHeight * scale;
        var titleHeight = Typography.LineHeight(TitleStyle);
        var sectionHeight = Typography.LineHeight(SectionStyle);
        var regionCount = SocialRegion.Codes.Length;
        var panelHeight = (8f + GrabberHeight + 12f) * scale + titleHeight + 8f * scale + rowHeight * FirstRegion
            + 14f * scale + sectionHeight + 8f * scale + ChipHeight * scale + 18f * scale + DoneHeight * scale
            + BottomPad * scale;
        var panelTop = screen.Max.Y - panelHeight + panelHeight * (1f - slide);
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, screen.Max.Y + Rounding * scale);
        var rounding = Rounding * scale;
        Squircle.Fill(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(PanelFill, PanelFill.W * opacity)));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * opacity)), 1f);
        var interactive = open && opacity > 0.5f;
        var picked = -1;

        var grabberMin = new Vector2(screen.Center.X - GrabberWidth * scale * 0.5f, panelTop + 8f * scale);
        drawList.AddRectFilled(grabberMin, grabberMin + new Vector2(GrabberWidth, GrabberHeight) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(GrabberFill, GrabberFill.W * opacity)), GrabberHeight * scale * 0.5f);
        var cursorY = grabberMin.Y + (GrabberHeight + 12f) * scale;
        var ink = Palette.WithAlpha(ChirperInk.TitleInk, opacity);
        var bodyInk = Palette.WithAlpha(ChirperInk.BodyInk, opacity);
        Typography.Draw(drawList, new Vector2(panelMin.X + padX, cursorY), Loc.T(L.Chirper.FeedFilters), ink,
            TitleStyle);
        cursorY += titleHeight + 8f * scale;

        var left = panelMin.X + padX;
        var right = panelMax.X - padX;
        if (DrawToggleRow(drawList, left, right, cursorY, rowHeight, Loc.T(L.Settings.ChirperShowPhotos), photos,
                ref toggles[PhotosToggle], bodyInk, opacity, interactive, delta, scale))
        {
            picked = PhotosToggle;
        }

        cursorY += rowHeight;
        if (DrawToggleRow(drawList, left, right, cursorY, rowHeight, Loc.T(L.Settings.ChirperShowGifs), gifs,
                ref toggles[GifsToggle], bodyInk, opacity, interactive, delta, scale))
        {
            picked = GifsToggle;
        }

        cursorY += rowHeight;
        if (DrawToggleRow(drawList, left, right, cursorY, rowHeight, Loc.T(L.Settings.ChirperShowReplyMedia),
                replyMedia, ref toggles[ReplyMediaToggle], bodyInk, opacity, interactive, delta, scale))
        {
            picked = ReplyMediaToggle;
        }

        cursorY += rowHeight + 14f * scale;
        Typography.Draw(drawList, new Vector2(left, cursorY),
            Loc.Culture.TextInfo.ToUpper(Loc.T(L.Chirper.Regions)), Palette.WithAlpha(ChirperInk.FaintInk, opacity),
            SectionStyle);
        cursorY += sectionHeight + 8f * scale;
        var chipGap = ChipGap * scale;
        var chipWidth = (right - left - chipGap * (regionCount - 1)) / regionCount;
        for (var regionIndex = 0; regionIndex < regionCount; regionIndex++)
        {
            var chipMin = new Vector2(left + regionIndex * (chipWidth + chipGap), cursorY);
            var chipMax = new Vector2(chipMin.X + chipWidth, cursorY + ChipHeight * scale);
            var on = SocialRegion.MaskShows(regionMask, regionIndex);
            var hovered = interactive && UiInteract.HoverWindowOnly(chipMin, chipMax, false);
            var fill = on ? ChipOnFill : hovered ? ChirperInk.ChipHover : ChipOffFill;
            var stroke = on ? ChipOnStroke : ChipOffStroke;
            Squircle.Fill(drawList, chipMin, chipMax, 12f * scale, ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * opacity)));
            Squircle.Stroke(drawList, chipMin, chipMax, 12f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(stroke, stroke.W * opacity)), 1f);
            var chipInk = on ? ChirperInk.MineInk : ChirperInk.QuoteBodyInk;
            Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f,
                Typography.FitText(SocialRegion.Codes[regionIndex], chipWidth - 8f * scale, ChipStyle),
                Palette.WithAlpha(chipInk, opacity), ChipStyle);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    picked = FirstRegion + regionIndex;
                    UiFeedback.Play(UiSound.Tap);
                }
            }
        }

        cursorY += ChipHeight * scale + 18f * scale;
        var doneMin = new Vector2(left, cursorY);
        var doneMax = new Vector2(right, cursorY + DoneHeight * scale);
        var doneHovered = interactive && UiInteract.HoverWindowOnly(doneMin, doneMax, false);
        var doneRounding = DoneHeight * scale * 0.5f;
        ChirperPill.PaintAccent(drawList, doneMin, doneMax, doneRounding, doneHovered, opacity);
        Typography.DrawCentered(drawList, (doneMin + doneMax) * 0.5f, Loc.T(L.Chirper.Done),
            Palette.WithAlpha(ChirperInk.White, opacity), DoneStyle);
        if (doneHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                UiFeedback.Play(UiSound.Tap);
                Close();
            }
        }

        drawList.PopClipRect();
        if (interactive && ImGui.GetFrameCount() != openedFrame && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !UiInteract.HoverWindowOnly(panelMin, panelMax, false))
        {
            Close();
        }

        return picked;
    }

    private static bool DrawToggleRow(ImDrawListPtr drawList, float left, float right, float top, float rowHeight,
        string label, bool value, ref Spring knob, Vector4 ink, float opacity, bool interactive, float delta,
        float scale)
    {
        knob.Step(value ? 1f : 0f, ToggleSmoothTime, delta);
        var centerY = top + rowHeight * 0.5f;
        var trackWidth = TrackWidth * scale;
        var trackHeight = TrackHeight * scale;
        var trackMin = new Vector2(right - trackWidth, centerY - trackHeight * 0.5f);
        var trackMax = new Vector2(right, centerY + trackHeight * 0.5f);
        var labelSize = Typography.Measure(label, RowStyle);
        Typography.Draw(drawList, new Vector2(left, centerY - labelSize.Y * 0.5f),
            Typography.FitText(label, trackMin.X - left - 12f * scale, RowStyle), ink, RowStyle);
        var trackFill = Palette.Mix(ToggleOff, ToggleOn, knob.Value);
        Squircle.Fill(drawList, trackMin, trackMax, trackHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(trackFill, trackFill.W * opacity)));
        var knobRadius = KnobSize * scale * 0.5f;
        var knobX = trackMin.X + 2f * scale + knobRadius + knob.Value * (trackWidth - KnobSize * scale - 4f * scale);
        drawList.AddCircleFilled(new Vector2(knobX, centerY + 1.5f * scale), knobRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f * opacity)), 24);
        drawList.AddCircleFilled(new Vector2(knobX, centerY), knobRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ChirperInk.White, opacity)), 24);
        drawList.AddLine(new Vector2(left, top + rowHeight - 0.5f), new Vector2(right, top + rowHeight - 0.5f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f * opacity)), 1f);
        var rowMin = new Vector2(left, top);
        var rowMax = new Vector2(right, top + rowHeight);
        var hovered = interactive && UiInteract.HoverWindowOnly(rowMin, rowMax, false);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return false;
        }

        UiFeedback.Play(value ? UiSound.ToggleOff : UiSound.ToggleOn);
        return true;
    }
}
