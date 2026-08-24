using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float TheaterAspect = (float)VideoEngine.ScreenWidth / VideoEngine.ScreenHeight;
    private const float TheaterFadeTime = 0.14f;
    private const float TheaterScrimHeight = 78f;
    private const float TheaterHeaderY = 50f;
    private const float TheaterProgressY = 40f;

    private static readonly Vector4 TheaterBackdrop = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 TheaterButtonBacking = new(0f, 0f, 0f, 0.42f);

    private Spring theaterFade;
    private bool theaterScrubbing;
    private float theaterScrubValue;

    private bool TheaterActive => AppLandscape.Held(Id);

    private void ExitTheater()
    {
        theaterScrubbing = false;
        AppLandscape.Release(Id);
    }

    private void DrawTheater(Rect area, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(TheaterBackdrop), theme.ScreenRounding * scale);
        DrawTheaterFrame(drawList, TheaterFrameRect(area), scale);

        var delta = ImGui.GetIO().DeltaTime;
        var loading = video.State == VideoPlaybackState.Loading;
        if (loading)
        {
            LoadingPulse.Draw(area.Center - new Vector2(0f, 10f * scale), 16f * scale, ui.Accent, WhiteInk,
                Loc.T(L.AetherStream.LoadingVideo), 1f, 0.8f, drawList);
        }
        else if (TryDescribeFailure(out var failureTitle, out var failureBody))
        {
            DrawTheaterFailure(drawList, area, scale, failureTitle, failureBody);
        }

        DrawTheaterSuggestionsPill(drawList, area, scale, delta);

        var hovered = UiInteract.Hover(area.Min, area.Max);
        var eased = Math.Clamp(theaterFade.Step(hovered ? 1f : 0f, TheaterFadeTime, delta), 0f, 1f);
        if (eased <= 0.01f)
        {
            return;
        }

        DrawTheaterScrims(drawList, area, scale, eased);
        DrawTheaterHeader(drawList, area, scale, eased, delta);
        if (!loading)
        {
            DrawTheaterTransport(drawList, area, scale, eased, delta);
        }

        DrawTheaterProgress(drawList, area, scale, eased);
    }

    private void DrawTheaterFailure(ImDrawListPtr drawList, Rect area, float scale, string title, string body)
    {
        var maxWidth = Math.Min(area.Width * 0.7f, 420f * scale);
        var titleStyle = TextStyles.BodyEmphasized;
        var titleCenter = area.Center - new Vector2(0f, Typography.LineHeight(titleStyle));
        Typography.DrawCentered(drawList, titleCenter, title, WhiteInk, titleStyle.Scale, titleStyle.Weight);
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Footnote, Palette.WithAlpha(WhiteInk, 0.8f),
            area.Center, maxWidth);
    }

    private void DrawTheaterSuggestionsPill(ImDrawListPtr drawList, Rect area, float scale, float delta)
    {
        if (!watchAlong.IsHosting || watchAlong.PendingQueueSuggestions.Count == 0)
        {
            return;
        }

        var radius = 16f * scale;
        var center = new Vector2(area.Min.X + Metrics.Space.Lg * scale + radius,
            area.Min.Y + TheaterHeaderY * scale);
        if (HoverButton.Circle(drawList, "aetherstream.theater.suggestions", center, radius, FontAwesomeIcon.ListUl,
                TheaterButtonBacking, WhiteInk, delta, 1f, true, Loc.T(L.AetherStream.QueueSuggestionsHeader)))
        {
            ExitTheater();
            upNextSheet.Open();
            return;
        }

        AppBadge.Draw(new Vector2(center.X + radius * 0.72f, center.Y - radius * 0.72f),
            watchAlong.PendingQueueSuggestions.Count, theme, scale);
    }

    private static Rect TheaterFrameRect(Rect area)
    {
        var width = area.Width;
        var height = width / TheaterAspect;
        if (height > area.Height)
        {
            height = area.Height;
            width = height * TheaterAspect;
        }

        var half = new Vector2(width * 0.5f, height * 0.5f);
        return new Rect(area.Center - half, area.Center + half);
    }

    private void DrawTheaterFrame(ImDrawListPtr drawList, Rect frame, float scale)
    {
        var rounding = Metrics.Radius.Sm * scale;
        var liveHandle = screen.Engine.ScreenViewHandle;
        if (liveHandle != nint.Zero && video.HasMedia && video.FrameVersion > 0)
        {
            drawList.AddImageRounded(new ImTextureID(liveHandle), frame.Min, frame.Max, Vector2.Zero, Vector2.One,
                0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
            return;
        }

        var current = CurrentEntry;
        var thumbnail = VideoThumbnailResolver.Get(remoteImages, http, current?.Url, current?.ThumbnailUrl);
        if (thumbnail is not null)
        {
            drawList.AddImageRounded(thumbnail.Handle, frame.Min, frame.Max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                rounding, ImDrawFlags.RoundCornersAll);
            return;
        }

        AppSkin.Icon(drawList, frame.Center, FontAwesomeIcon.Tv.ToIconString(), ui.MutedInk, 1.8f);
    }

    private static void DrawTheaterScrims(ImDrawListPtr drawList, Rect area, float scale, float eased)
    {
        var height = TheaterScrimHeight * scale;
        var ink = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.6f * eased));
        var clear = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f));
        drawList.AddRectFilledMultiColor(area.Min, new Vector2(area.Max.X, area.Min.Y + height), ink, ink, clear,
            clear);
        drawList.AddRectFilledMultiColor(new Vector2(area.Min.X, area.Max.Y - height), area.Max, clear, clear, ink,
            ink);
    }

    private void DrawTheaterHeader(ImDrawListPtr drawList, Rect area, float scale, float eased, float delta)
    {
        var radius = 16f * scale;
        var centerY = area.Min.Y + TheaterHeaderY * scale;
        var exitCenter = new Vector2(area.Max.X - Metrics.Space.Lg * scale - radius, centerY);
        if (HoverButton.Circle(drawList, "aetherstream.theater.exit", exitCenter, radius, FontAwesomeIcon.Compress,
                TheaterButtonBacking, WhiteInk, delta, eased, true, Loc.T(L.AetherStream.ExitFullscreen)))
        {
            ExitTheater();
            return;
        }

        var windowCenter = new Vector2(exitCenter.X - radius * 2f - Metrics.Space.Sm * scale, centerY);
        if (HoverButton.Circle(drawList, "aetherstream.theater.window", windowCenter, radius,
                FontAwesomeIcon.WindowRestore, TheaterButtonBacking, WhiteInk, delta, eased, true,
                Loc.T(L.AetherStream.OpenScreenWindow)))
        {
            screenWindow.IsOpen = true;
        }

        if (CurrentEntry is not { } current)
        {
            return;
        }

        var titleLeft = area.Min.X + Metrics.Space.Lg * scale;
        if (watchAlong.IsHosting && watchAlong.PendingQueueSuggestions.Count > 0)
        {
            titleLeft += 32f * scale + Metrics.Space.Md * scale;
        }

        var titleWidth = windowCenter.X - radius - Metrics.Space.Md * scale - titleLeft;
        if (titleWidth <= 0f)
        {
            return;
        }

        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        Marquee.DrawLeftAuto("aetherstream.theater.title", current.Title, titleLeft, centerY - titleHeight * 0.5f,
            titleWidth, TextStyles.Headline, Palette.WithAlpha(WhiteInk, eased));
    }

    private void DrawTheaterTransport(ImDrawListPtr drawList, Rect area, float scale, float eased, float delta)
    {
        var interactive = !watchAlong.IsViewing;
        var alpha = eased * (interactive ? 1f : 0.45f);
        var progress = video.Progress;
        var position = progress.Position;
        var center = area.Center;
        var playRadius = 30f * scale;
        var trackRadius = 20f * scale;
        var seekRadius = 18f * scale;
        var trackOffset = 78f * scale;
        var seekOffset = 142f * scale;

        if (interactive && HoverButton.Circle(drawList, "aetherstream.theater.seekBack",
                new Vector2(center.X - seekOffset, center.Y), seekRadius, FontAwesomeIcon.UndoAlt,
                AppSkin.Transparent, WhiteInk, delta, alpha, true))
        {
            video.Seek(MathF.Max(0f, position - 10f));
        }

        if (TransportButton.Draw(new Vector2(center.X - trackOffset, center.Y), trackRadius,
                TransportAction.Previous, WhiteInk, WhiteInk, alpha, interactive) && interactive)
        {
            queue.Restart();
        }

        drawList.AddCircleFilled(center, playRadius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, alpha)), 40);
        var playAction = progress.Paused || !video.HasMedia ? TransportAction.Play : TransportAction.Pause;
        if (TransportButton.Draw(center, playRadius, playAction, ui.Accent, WhiteInk, alpha, interactive) &&
            interactive)
        {
            TogglePlayback(progress.Paused);
        }

        var canAdvance = interactive && queue.HasNext;
        if (TransportButton.Draw(new Vector2(center.X + trackOffset, center.Y), trackRadius, TransportAction.Next,
                WhiteInk, WhiteInk, canAdvance ? alpha : eased * 0.35f, canAdvance) && canAdvance)
        {
            queue.Advance();
        }

        if (interactive && HoverButton.Circle(drawList, "aetherstream.theater.seekForward",
                new Vector2(center.X + seekOffset, center.Y), seekRadius, FontAwesomeIcon.RedoAlt,
                AppSkin.Transparent, WhiteInk, delta, alpha, true))
        {
            video.Seek(position + 10f);
        }
    }

    private void DrawTheaterProgress(ImDrawListPtr drawList, Rect area, float scale, float eased)
    {
        var progress = video.Progress;
        var duration = progress.Duration;
        var shown = theaterScrubbing ? theaterScrubValue * duration : progress.Position;
        var rowCenterY = area.Max.Y - TheaterProgressY * scale;
        var pad = Metrics.Space.Lg * scale;
        var ink = Palette.WithAlpha(WhiteInk, eased);
        var elapsedText = TimeText.MinutesSeconds((int)shown);
        var remainingText = $"-{TimeText.MinutesSeconds((int)MathF.Max(0f, duration - shown))}";
        var elapsedSize = Typography.Measure(elapsedText, TextStyles.Caption1);
        var remainingSize = Typography.Measure(remainingText, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(area.Min.X + pad, rowCenterY - elapsedSize.Y * 0.5f), elapsedText, ink,
            TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(area.Max.X - pad - remainingSize.X, rowCenterY - remainingSize.Y * 0.5f),
            remainingText, ink, TextStyles.Caption1);

        var trackLeft = area.Min.X + pad + elapsedSize.X + Metrics.Space.Md * scale;
        var trackRight = area.Max.X - pad - remainingSize.X - Metrics.Space.Md * scale;
        if (trackRight <= trackLeft)
        {
            return;
        }

        var track = new Rect(new Vector2(trackLeft, rowCenterY - 2f * scale),
            new Vector2(trackRight, rowCenterY + 2f * scale));
        var normalized = theaterScrubbing ? theaterScrubValue : progress.Fraction;
        var updated = Scrubber.Draw(track, normalized, ui.Accent, Palette.WithAlpha(WhiteInk, 0.28f), eased);
        if (watchAlong.IsViewing || duration <= 0f)
        {
            return;
        }

        if (Scrubber.IsHovered(track) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            theaterScrubbing = true;
            theaterScrubValue = updated;
            return;
        }

        if (!theaterScrubbing)
        {
            return;
        }

        theaterScrubbing = false;
        video.Seek(theaterScrubValue * duration);
    }
}
