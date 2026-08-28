using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float QueueDragThreshold = 7f;
    private const float QueueRowHeight = 56f;
    private const float SuggestionRowHeight = 44f;

    private int queueDragIndex = -1;
    private Vector2 queueDragStart;
    private float queueDragY;
    private bool queueDragActive;
    private bool suggestionNoticesCleared;

    private void DrawUpNextContent(Rect body, float scale)
    {
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var width = ScrollLayout.StableContentWidth();

            if (watchAlong.IsViewing)
            {
                DrawHostQueueList(width, scale, body);
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
                return;
            }

            if (watchAlong.IsHosting)
            {
                suggestionNotifier.StampAttention();
                if (!suggestionNoticesCleared)
                {
                    suggestionNotifier.ClearShellNotices();
                    suggestionNoticesCleared = true;
                }
            }

            DrawNowPlayingRow(width, scale);

            if (watchAlong.IsHosting && watchAlong.PendingQueueSuggestions.Count > 0)
            {
                DrawQueueSuggestions(width, scale);
            }

            var entries = queue.Entries;
            if (entries.Count == 0)
            {
                queueDragIndex = -1;
                queueDragActive = false;
                var origin = ImGui.GetCursorScreenPos();
                var emptyHeight = MathF.Max(200f * scale, body.Max.Y - origin.Y - Metrics.Space.Lg * scale);
                EmptyState.Draw(new Rect(origin, origin + new Vector2(width, emptyHeight)), ui,
                    FontAwesomeIcon.Film, Loc.T(L.AetherStream.UpNextEmpty),
                    Loc.T(L.AetherStream.UpNextEmptyHint));
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, emptyHeight));
                return;
            }

            DrawQueueHeader(width, scale);
            DrawQueueList(scale, entries);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawHostQueueList(float width, float scale, Rect body)
    {
        var items = watchAlong.HostQueue;
        if (items.Count == 0)
        {
            var origin = ImGui.GetCursorScreenPos();
            var emptyHeight = MathF.Max(200f * scale, body.Max.Y - origin.Y - Metrics.Space.Lg * scale);
            EmptyState.Draw(new Rect(origin, origin + new Vector2(width, emptyHeight)), ui, FontAwesomeIcon.Film,
                Loc.T(L.AetherStream.UpNextEmpty), Loc.T(L.AetherStream.SuggestHint));
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, emptyHeight));
            return;
        }

        ListSection.Label(ui, Loc.T(L.AetherStream.UpNextHostQueue));

        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = QueueRowHeight * scale;
        for (var index = 0; index < items.Count; index++)
        {
            var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash, interactive: false);
            DrawHostQueueRow(cell.Bounds, items[index], scale);
            FeedCell.End(drawList, cell, ui.Hairline);
        }
    }

    private void DrawHostQueueRow(Rect row, HostQueueItem item, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var artSize = row.Height - 12f * scale;
        var artMin = new Vector2(row.Min.X + FeedCell.PadX * scale, row.Min.Y + 6f * scale);
        var artMax = artMin + new Vector2(artSize, artSize);
        Squircle.Fill(drawList, artMin, artMax, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.FieldSurface));
        var thumbnail = VideoThumbnailResolver.Get(remoteImages, http, item.Url, null);
        if (thumbnail is not null)
        {
            drawList.AddImageRounded(thumbnail.Handle, artMin, artMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                Metrics.Radius.Sm * scale, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            AppSkin.Icon((artMin + artMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Play), ui.MutedInk, 0.7f);
        }

        var textLeft = artMax.X + Metrics.Space.Md * scale;
        var textWidth = row.Max.X - FeedCell.PadX * scale - textLeft;
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - Typography.LineHeight(TextStyles.Body) * 0.5f),
            Typography.FitText(item.Title, textWidth, TextStyles.Body), ui.TitleInk, TextStyles.Body);
    }

    private void DrawNowPlayingRow(float width, float scale)
    {
        if (queue.Current is not { } current)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        ListSection.Label(ui, Loc.T(L.AetherStream.NowPlayingHeader));

        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, QueueRowHeight * scale, ui.HoverWash, interactive: false);
        var row = cell.Bounds;
        var artSize = row.Height - 12f * scale;
        var artMin = new Vector2(row.Min.X + FeedCell.PadX * scale, row.Min.Y + 6f * scale);
        var artMax = artMin + new Vector2(artSize, artSize);
        Squircle.Fill(drawList, artMin, artMax, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.FieldSurface));
        var thumbnail = VideoThumbnailResolver.Get(remoteImages, http, current.Url, current.ThumbnailUrl);
        if (thumbnail is not null)
        {
            drawList.AddImageRounded(thumbnail.Handle, artMin, artMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                Metrics.Radius.Sm * scale, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            AppSkin.Icon((artMin + artMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Play), ui.MutedInk, 0.7f);
        }

        var textLeft = artMax.X + Metrics.Space.Md * scale;
        var textWidth = row.Max.X - FeedCell.PadX * scale - textLeft;
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 8f * scale),
            Typography.FitText(current.Title, textWidth, TextStyles.Body), ui.TitleInk, TextStyles.Body);

        var secondLineY = row.Min.Y + 30f * scale;
        if (video.State == VideoPlaybackState.Loading)
        {
            var loadingLabel = Loc.T(L.AetherStream.LoadingVideo);
            Typography.Draw(drawList, new Vector2(textLeft, secondLineY),
                Typography.FitText(loadingLabel, textWidth, TextStyles.Footnote), ui.Accent, TextStyles.Footnote);
            var loadingLabelWidth = Typography.Measure(loadingLabel, TextStyles.Footnote).X;
            LoadingPulse.Dots(new Vector2(textLeft + loadingLabelWidth + 9f * scale,
                    secondLineY + Typography.LineHeight(TextStyles.Footnote) * 0.5f), 8f * scale, 2.3f * scale,
                ui.Accent, 1f, drawList);
        }
        else
        {
            var secondLine = current.Duration is { } duration
                ? $"{current.Source}  ·  {TimeText.MinutesSeconds((int)duration.TotalSeconds)}"
                : current.Source;
            Typography.Draw(drawList, new Vector2(textLeft, secondLineY),
                Typography.FitText(secondLine, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void DrawQueueHeader(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var origin = ImGui.GetCursorScreenPos();
        var inset = FeedCell.PadX * scale;
        var rowHeight = 28f * scale;
        var label = Loc.Culture.TextInfo.ToUpper(Loc.T(L.AetherStream.UpNext));
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(origin.X + inset, origin.Y + rowHeight * 0.5f - labelHeight * 0.5f), label,
            ui.Palette.HeaderInk, TextStyles.FootnoteEmphasized);

        var clearLabel = Loc.T(L.AetherStream.ClearQueue);
        var clearHalfWidth = Typography.Measure(clearLabel, TextStyles.Subheadline).X * 0.5f + 12f * scale;
        var clearCenter = new Vector2(origin.X + width - inset - clearHalfWidth, origin.Y + rowHeight * 0.5f);
        if (TextButton.Draw(clearCenter, clearLabel, theme.Danger, scale))
        {
            confirm.Ask(new ConfirmRequest
            {
                Message = Loc.T(L.AetherStream.ClearQueueConfirm),
                ConfirmLabel = Loc.T(L.AetherStream.Stop),
                CancelLabel = Loc.T(L.AetherStream.Keep),
                Sheet = true,
                Confirm = () => queue.Clear(),
            });
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Xs * scale));
    }

    private void DrawQueueSuggestions(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        ListSection.Label(ui, Loc.T(L.AetherStream.QueueSuggestionsHeader));

        var suggestions = watchAlong.PendingQueueSuggestions;
        for (var index = 0; index < suggestions.Count; index++)
        {
            DrawQueueSuggestionRow(scale, suggestions[index]);
        }
    }

    private void DrawQueueSuggestionRow(float scale, QueueSuggestion suggestion)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, SuggestionRowHeight * scale, ui.HoverWash, interactive: false);
        var row = cell.Bounds;
        var inset = FeedCell.PadX * scale;

        var delta = ImGui.GetIO().DeltaTime;
        var circleRadius = 14f * scale;
        var denyCenter = new Vector2(row.Max.X - inset - circleRadius, row.Center.Y);
        var approveCenter = new Vector2(denyCenter.X - circleRadius * 2f - Metrics.Space.Sm * scale, row.Center.Y);

        var textLeft = row.Min.X + inset;
        var textRight = approveCenter.X - circleRadius - Metrics.Space.Md * scale;
        var textWidth = textRight - textLeft;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        Marquee.DrawLeft(drawList, "aetherstream.suggestion.name." + suggestion.SuggestionId,
            suggestion.DisplayName, textLeft, row.Center.Y - 16f * scale, textWidth, TextStyles.Body, ui.TitleInk,
            hovered);
        Marquee.DrawLeft(drawList, "aetherstream.suggestion.url." + suggestion.SuggestionId, suggestion.Url,
            textLeft, row.Center.Y + 2f * scale, textWidth, TextStyles.Caption1, ui.MutedInk, hovered);

        if (HoverButton.Circle(drawList, "aetherstream.suggestion.approve." + suggestion.SuggestionId,
                approveCenter, circleRadius, FontAwesomeIcon.Check, Palette.WithAlpha(ui.Accent, 0.16f), ui.Accent,
                delta, 1f, true, Loc.T(L.AetherStream.QueueSuggestionAdd)))
        {
            watchAlong.ApproveQueueSuggestion(suggestion.SuggestionId);
        }

        if (HoverButton.Circle(drawList, "aetherstream.suggestion.deny." + suggestion.SuggestionId, denyCenter,
                circleRadius, FontAwesomeIcon.Times, Palette.WithAlpha(theme.Danger, 0.14f), theme.Danger, delta,
                1f, true, Loc.T(L.AetherStream.QueueSuggestionDismiss)))
        {
            watchAlong.DenyQueueSuggestion(suggestion.SuggestionId);
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void DrawQueueList(float scale, IReadOnlyList<VideoQueueEntry> entries)
    {
        var rowHeight = QueueRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        UpdateDrag(entries.Count, rowHeight + ImGui.GetStyle().ItemSpacing.Y);

        for (var index = 0; index < entries.Count; index++)
        {
            var dragging = queueDragActive && index == queueDragIndex;
            var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash);
            var row = dragging
                ? new Rect(new Vector2(cell.Bounds.Min.X, cell.Bounds.Min.Y + queueDragY),
                    new Vector2(cell.Bounds.Max.X, cell.Bounds.Max.Y + queueDragY))
                : cell.Bounds;
            DrawQueueRow(cell, row, entries[index], index, scale, dragging);
            FeedCell.End(drawList, cell, ui.Hairline, !dragging);
        }
    }

    private void UpdateDrag(int count, float rowPitch)
    {
        if (!queueDragActive)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var targetIndex = Math.Clamp(queueDragIndex + (int)MathF.Round(queueDragY / rowPitch), 0, count - 1);
            if (targetIndex != queueDragIndex)
            {
                queue.Reorder(queueDragIndex, targetIndex);
            }

            queueDragActive = false;
            queueDragIndex = -1;
            return;
        }

        queueDragY = ImGui.GetMousePos().Y - queueDragStart.Y;
    }

    private void DrawQueueRow(in FeedCellScope cell, Rect row, VideoQueueEntry entry, int index, float scale,
        bool dragging)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (dragging)
        {
            drawList.AddRectFilled(row.Min, row.Max,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.10f)));
        }

        var artSize = row.Height - 12f * scale;
        var artMin = new Vector2(row.Min.X + FeedCell.PadX * scale, row.Min.Y + 6f * scale);
        var artMax = artMin + new Vector2(artSize, artSize);
        Squircle.Fill(drawList, artMin, artMax, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.FieldSurface));
        var thumbnail = VideoThumbnailResolver.Get(remoteImages, http, entry.Url, entry.ThumbnailUrl);
        if (thumbnail is not null)
        {
            drawList.AddImageRounded(thumbnail.Handle, artMin, artMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                Metrics.Radius.Sm * scale, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            AppSkin.Icon((artMin + artMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Play), ui.MutedInk, 0.7f);
        }

        var textLeft = artMax.X + Metrics.Space.Md * scale;
        var textRight = row.Max.X - 78f * scale - FeedCell.PadX * scale;
        var textWidth = textRight - textLeft;
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 8f * scale),
            Typography.FitText(entry.Title, textWidth, TextStyles.Body), ui.TitleInk, TextStyles.Body);
        var secondLine = entry.Duration is { } duration
            ? $"{entry.Source}  ·  {TimeText.MinutesSeconds((int)duration.TotalSeconds)}"
            : entry.Source;
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 30f * scale),
            Typography.FitText(secondLine, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        var handleCenter = new Vector2(row.Max.X - (52f + FeedCell.PadX) * scale, row.Center.Y);
        AppSkin.Icon(handleCenter, IconGlyph.Of(FontAwesomeIcon.GripLines), ui.MutedInk, 0.6f);
        var handleHit = UiInteract.Hover(handleCenter - new Vector2(14f * scale, 14f * scale),
            handleCenter + new Vector2(14f * scale, 14f * scale));
        if (handleHit && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !queueDragActive)
        {
            queueDragIndex = index;
            queueDragStart = ImGui.GetMousePos();
            queueDragY = 0f;
        }

        if (queueDragIndex == index && !queueDragActive && ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
            Vector2.Distance(ImGui.GetMousePos(), queueDragStart) > QueueDragThreshold * scale)
        {
            queueDragActive = true;
        }

        var removeCenter = new Vector2(row.Max.X - (16f + FeedCell.PadX) * scale, row.Center.Y);
        if (ui.IconButton(removeCenter, 12f * scale, IconGlyph.Of(FontAwesomeIcon.Times), ui.MutedInk,
                AppSkin.Transparent, 0.55f, Loc.T(L.AetherStream.Remove)))
        {
            queue.Remove(entry);
        }

        if (cell.Tapped && !queueDragActive && !handleHit && ImGui.GetMousePos().X < removeCenter.X - 16f * scale)
        {
            queue.PlayNow(entry);
        }
    }
}
