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

    private void DrawQueueTab(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawQueueComposer(width, scale);

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
                var emptyHeight = MathF.Max(220f * scale, body.Max.Y - origin.Y - Metrics.Space.Lg * scale);
                EmptyState.Draw(new Rect(origin, origin + new Vector2(width, emptyHeight)), ui,
                    FontAwesomeIcon.Film, Loc.T(L.AetherStream.QueueEmptyTitle),
                    Loc.T(L.AetherStream.QueueEmptyHint));
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, emptyHeight));
                return;
            }

            DrawQueueHeader(width, scale);
            DrawQueueList(width, scale, entries);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawQueueComposer(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var fieldRect = new Rect(origin, origin + new Vector2(width, 44f * scale));
        var submitted = SubmitField.Draw(fieldRect, "##aetherstreamQueueUrl", Loc.T(L.AetherStream.UrlHint),
            ref urlInput, accentedTheme, 2000, FontAwesomeIcon.Link);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 44f * scale));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));

        var rowOrigin = ImGui.GetCursorScreenPos();
        var buttonHeight = 34f * scale;
        var gap = Metrics.Space.Sm * scale;
        var half = (width - gap) * 0.5f;
        var enabled = urlInput.Trim().Length > 0;
        var addRect = new Rect(rowOrigin, rowOrigin + new Vector2(half, buttonHeight));
        var nextRect = new Rect(new Vector2(rowOrigin.X + width - half, rowOrigin.Y),
            new Vector2(rowOrigin.X + width, rowOrigin.Y + buttonHeight));

        if (submitted && enabled)
        {
            queue.Add(MakeEntry(urlInput.Trim()));
            urlInput = string.Empty;
            enabled = false;
        }

        var addClicked = AppSkin.PillButton(addRect, Loc.T(L.AetherStream.AddToQueue), true, enabled,
            accentedTheme);
        var playNextClicked = AppSkin.PillButton(nextRect, Loc.T(L.AetherStream.PlayNext), false, enabled,
            accentedTheme);
        if (addClicked && enabled)
        {
            queue.Add(MakeEntry(urlInput.Trim()));
            urlInput = string.Empty;
        }
        else if (playNextClicked && enabled)
        {
            queue.PlayNext(MakeEntry(urlInput.Trim()));
            urlInput = string.Empty;
        }

        ImGui.SetCursorScreenPos(rowOrigin);
        ImGui.Dummy(new Vector2(width, buttonHeight));
    }

    private void DrawQueueHeader(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = 28f * scale;
        var label = Loc.Culture.TextInfo.ToUpper(Loc.T(L.AetherStream.QueueUpNext));
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(origin.X, origin.Y + rowHeight * 0.5f - labelHeight * 0.5f), label, ui.Palette.HeaderInk,
            TextStyles.FootnoteEmphasized);

        var clearLabel = Loc.T(L.AetherStream.ClearQueue);
        var clearHalfWidth = Typography.Measure(clearLabel, TextStyles.Subheadline).X * 0.5f + 12f * scale;
        var clearCenter = new Vector2(origin.X + width - clearHalfWidth, origin.Y + rowHeight * 0.5f);
        if (TextButton.Draw(clearCenter, clearLabel, theme.Danger, scale))
        {
            confirm.Ask(new ConfirmRequest
            {
                Message = Loc.T(L.AetherStream.ClearQueueConfirm),
                ConfirmLabel = Loc.T(L.AetherStream.Stop),
                CancelLabel = Loc.T(L.AetherStream.Keep),
                Confirm = () => queue.Clear(),
            });
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Xs * scale));
    }

    private void DrawQueueSuggestions(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var labelOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(ImGui.GetWindowDrawList(), labelOrigin,
            Loc.Culture.TextInfo.ToUpper(Loc.T(L.AetherStream.QueueSuggestionsHeader)), ui.Palette.HeaderInk,
            TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(labelOrigin);
        ImGui.Dummy(new Vector2(width,
            Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Xs * scale));

        var suggestions = watchAlong.PendingQueueSuggestions;
        for (var index = 0; index < suggestions.Count; index++)
        {
            DrawQueueSuggestionRow(width, scale, suggestions[index]);
        }
    }

    private void DrawQueueSuggestionRow(float width, float scale, QueueSuggestion suggestion)
    {
        var origin = ImGui.GetCursorScreenPos();
        var row = new Rect(origin, origin + new Vector2(width, SuggestionRowHeight * scale));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, row.Min, row.Max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(accentedTheme.GroupedCard));

        var delta = ImGui.GetIO().DeltaTime;
        var circleRadius = 14f * scale;
        var denyCenter = new Vector2(row.Max.X - Metrics.Space.Md * scale - circleRadius, row.Center.Y);
        var approveCenter = new Vector2(denyCenter.X - circleRadius * 2f - Metrics.Space.Sm * scale, row.Center.Y);

        var textLeft = row.Min.X + Metrics.Space.Md * scale;
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

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, SuggestionRowHeight * scale));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    private static VideoQueueEntry MakeEntry(string url) => new(url, url, string.Empty, null, null);

    private void DrawQueueList(float width, float scale, IReadOnlyList<VideoQueueEntry> entries)
    {
        var rowHeight = QueueRowHeight * scale;
        var listOrigin = ImGui.GetCursorScreenPos();
        UpdateDrag(entries.Count, rowHeight);

        for (var index = 0; index < entries.Count; index++)
        {
            var origin = ImGui.GetCursorScreenPos();
            var rowMin = origin;
            var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
            if (queueDragActive && index == queueDragIndex)
            {
                rowMin = new Vector2(rowMin.X, rowMin.Y + queueDragY);
                rowMax = new Vector2(rowMax.X, rowMax.Y + queueDragY);
            }

            DrawQueueRow(new Rect(rowMin, rowMax), entries[index], index, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, rowHeight));
        }

        ImGui.SetCursorScreenPos(new Vector2(listOrigin.X, listOrigin.Y + entries.Count * rowHeight));
    }

    private void UpdateDrag(int count, float rowHeight)
    {
        if (!queueDragActive)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var targetIndex = Math.Clamp(queueDragIndex + (int)MathF.Round(queueDragY / rowHeight), 0, count - 1);
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

    private void DrawQueueRow(Rect row, VideoQueueEntry entry, int index, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var dragging = queueDragActive && index == queueDragIndex;
        if (hovered || dragging)
        {
            Squircle.Fill(drawList, row.Min, row.Max, Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, dragging ? 0.10f : 0.05f)));
        }

        var artSize = row.Height - 12f * scale;
        var artMin = new Vector2(row.Min.X + Metrics.Space.Xs * scale, row.Min.Y + 6f * scale);
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
            AppSkin.Icon((artMin + artMax) * 0.5f, FontAwesomeIcon.Play.ToIconString(), ui.MutedInk, 0.7f);
        }

        var textLeft = artMax.X + Metrics.Space.Md * scale;
        var textRight = row.Max.X - 78f * scale;
        var textWidth = textRight - textLeft;
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 8f * scale),
            Typography.FitText(entry.Title, textWidth, TextStyles.Body), ui.TitleInk, TextStyles.Body);
        var secondLine = entry.Duration is { } duration
            ? $"{entry.Source}  ·  {TimeText.MinutesSeconds((int)duration.TotalSeconds)}"
            : entry.Source;
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 30f * scale),
            Typography.FitText(secondLine, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        var handleCenter = new Vector2(row.Max.X - 52f * scale, row.Center.Y);
        AppSkin.Icon(handleCenter, FontAwesomeIcon.GripLines.ToIconString(), ui.MutedInk, 0.6f);
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

        var removeCenter = new Vector2(row.Max.X - 16f * scale, row.Center.Y);
        if (ui.IconButton(removeCenter, 12f * scale, FontAwesomeIcon.Times.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 0.55f, Loc.T(L.AetherStream.Remove)))
        {
            queue.Remove(entry);
        }

        if (hovered && !queueDragActive && !handleHit && ImGui.GetMousePos().X < removeCenter.X - 16f * scale &&
            UiInteract.Click(row.Min, row.Max, hovered))
        {
            queue.PlayNow(entry);
        }
    }
}
