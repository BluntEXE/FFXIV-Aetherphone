using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Announcements;

internal sealed partial class AnnouncementsApp
{
    private const float CellPadTop = 13f;
    private const float CellPadBottom = 13f;
    private const float FooterGap = 9f;
    private const int MaxTitleLines = 2;
    private const int MaxLeadLines = 6;
    private const int MaxPreviewLines = 2;

    private readonly PullToRefresh listRefresh = new();

    private void DrawList(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, DisplayName, navigation.Back);

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        UiAnchors.Report("announcements.feed", body);

        var announcements = store.Announcements;
        using (var surface = AppSurface.BeginEdgeToEdge(body))
        {
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            listRefresh.Draw(body, surface.Pull, surface.Dragging, store.Loading, ui.MutedInk, store.Refresh);
            if (announcements.Length == 0)
            {
                DrawEmptyState(body, scale);
                return;
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxs * scale));
            var previousUnix = 0L;
            for (var index = 0; index < announcements.Length; index++)
            {
                var announcement = announcements[index];
                if (index == 0 || !TimeText.SameLocalDay(previousUnix, announcement.CreatedAtUnix))
                {
                    ListSection.Label(ui, TimeText.DayLabel(announcement.CreatedAtUnix));
                }

                previousUnix = announcement.CreatedAtUnix;
                DrawCell(announcement, scale, index == 0 ? MaxLeadLines : MaxPreviewLines, index == 0);
            }

            if (store.LoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
            }
            else if (store.HasMore && InfiniteScroll.ReachedBottom())
            {
                store.LoadMore();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawEmptyState(Rect body, float scale)
    {
        if (store.Loading && !store.LoadedOnce)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 120f * scale), 13f * scale, ui.Accent,
                ui.MutedInk, Loc.T(L.Common.Loading));
            return;
        }

        if (store.Failed)
        {
            listFailure.Set(store.Failure);
            if (EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Failure.CouldNotLoad),
                    listFailure.Text(), Loc.T(L.Common.Retry)))
            {
                store.Refresh();
            }

            return;
        }

        EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.Announcements.EmptyTitle),
            Loc.T(L.Announcements.EmptyHint));
    }

    private void DrawCell(AnnouncementDto announcement, float scale, int maxBodyLines, bool isLead)
    {
        var drawList = ImGui.GetWindowDrawList();
        var inset = FeedCell.PadX * scale;
        var width = ScrollLayout.StableContentWidth();
        var unread = store.IsUnread(announcement);
        var text = AnnouncementText.For(announcement);
        var innerWidth = width - inset * 2f;

        var titleLines = ClampLines(text.Title, TextStyles.Headline, innerWidth, MaxTitleLines);
        var bodyLines = ClampLines(text.Body, TextStyles.Subheadline, innerWidth, maxBodyLines);
        var titleLineHeight = LineHeight(TextStyles.Headline);
        var bodyLineHeight = LineHeight(TextStyles.Subheadline);
        var stamp = TimeText.Clock(announcement.CreatedAtUnix);
        var stampSize = Typography.Measure(stamp, TextStyles.Footnote);
        var footerHeight = MathF.Max(stampSize.Y, PillHeight(scale));
        var bodyBlock = bodyLines.Length > 0 ? Metrics.Space.Xxs * scale + bodyLines.Length * bodyLineHeight : 0f;
        var cellHeight = CellPadTop * scale + titleLines.Length * titleLineHeight + bodyBlock
            + FooterGap * scale + footerHeight + CellPadBottom * scale;

        var cell = FeedCell.Begin(drawList, cellHeight, ui.HoverWash);
        if (unread)
        {
            drawList.AddRectFilled(cell.Bounds.Min, cell.Bounds.Max,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.07f)));
        }

        if (isLead)
        {
            UiAnchors.Report("announcements.card", cell.Bounds);
        }

        var textLeft = cell.Bounds.Min.X + inset;
        var cursorY = cell.Bounds.Min.Y + CellPadTop * scale;
        for (var index = 0; index < titleLines.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), titleLines[index], ui.TitleInk,
                TextStyles.Headline);
            cursorY += titleLineHeight;
        }

        if (bodyLines.Length > 0)
        {
            cursorY += Metrics.Space.Xxs * scale;
            for (var index = 0; index < bodyLines.Length; index++)
            {
                Typography.Draw(drawList, new Vector2(textLeft, cursorY), bodyLines[index], ui.BodyInk,
                    TextStyles.Subheadline);
                cursorY += bodyLineHeight;
            }
        }

        cursorY += FooterGap * scale;
        var footerCenterY = cursorY + footerHeight * 0.5f;
        var stampLeft = textLeft;
        if (unread)
        {
            stampLeft += DrawNewPill(drawList, new Vector2(textLeft, footerCenterY), scale) + Metrics.Space.Sm * scale;
        }

        Typography.Draw(drawList, new Vector2(stampLeft, footerCenterY - stampSize.Y * 0.5f), stamp, ui.MutedInk,
            TextStyles.Footnote);
        DrawChevron(drawList, new Vector2(cell.Bounds.Max.X - inset, footerCenterY), 5f * scale,
            Metrics.Stroke.Thin * scale, cell.Hovered ? ui.TitleInk : Palette.WithAlpha(ui.MutedInk, 0.7f));
        if (cell.Tapped)
        {
            router.Push(AnnouncementsRoute.Detail(announcement.Id));
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private float DrawNewPill(ImDrawListPtr drawList, Vector2 leftCenter, float scale)
    {
        var label = Loc.T(L.Announcements.NewBadge);
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var height = PillHeight(scale);
        var width = labelSize.X + Metrics.Space.Lg * scale;
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(ui.Accent));
        Typography.Draw(drawList, new Vector2(min.X + (width - labelSize.X) * 0.5f, leftCenter.Y - labelSize.Y * 0.5f),
            label, OnAccentInk, TextStyles.FootnoteEmphasized);
        return width;
    }

    private static float PillHeight(float scale) =>
        Typography.Measure("Ag", TextStyles.FootnoteEmphasized).Y + Metrics.Space.Xs * scale;
}
