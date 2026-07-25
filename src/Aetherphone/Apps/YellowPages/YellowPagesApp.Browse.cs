using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float SearchRowHeight = 46f;
    private const float ChipHeight = 32f;
    private const float RailCardWidth = 118f;
    private const float RailCardHeight = 134f;
    private const float RailThumbHeight = 64f;
    private const double SearchDebounceSeconds = 0.6;
    private const int SectionFallbackRebuildSeconds = 30;
    private const long OpeningSoonLeadSeconds = 8L * 3600L;

    private readonly List<AdDto> openSection = new();
    private readonly List<AdDto> latestSection = new();
    private readonly string[] chipLabels = new string[AdCategories.Count];
    private readonly bool[] chipActive = new bool[AdCategories.Count];
    private AdDto[] lastDirectory = Array.Empty<AdDto>();
    private long nextSectionRebuildUnix;
    private string browseSearch = string.Empty;
    private string browseSearchApplied = string.Empty;
    private double browseSearchEditedAt;
    private bool browseOpenNow;
    private int railStart;

    private void DrawBrowse(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var nowUnix = NowUnix();
        DrawBrowseHeader(area, scale);
        var controlsTop = area.Min.Y + AppHeader.Height * scale;
        DrawSearchRow(area, controlsTop, scale);
        var body = new Rect(new Vector2(area.Min.X, controlsTop + SearchRowHeight * scale), area.Max);
        EnsureBrowseSections(nowUnix);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            if (openSection.Count > 0)
            {
                ui.SectionHeading(Loc.T(L.YellowPages.OpenSection), 6f);
                DrawOpenRail(nowUnix, scale);
            }

            DrawCategoryFilters(scale);
            if (latestSection.Count == 0)
            {
                DrawBrowseEmpty(body, scale);
            }
            else
            {
                ui.SectionHeading(Loc.T(L.YellowPages.LatestSection), 6f);
                DrawCards(latestSection, nowUnix, scale);
                DrawLoadMore(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawBrowseHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), DisplayName,
            AppPalettes.YellowPages.TitleInk, 1.3f, FontWeight.Bold);
        var actionCenter = new Vector2(area.Max.X - 22f * scale, rowCenterY);
        if (store.Syncing || store.DirectoryLoading)
        {
            LoadingPulse.Spinner(actionCenter, 8f * scale, ui.Accent);
            return;
        }

        if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.Sync.ToIconString(),
                AppPalettes.YellowPages.BodyInk, AppSkin.Transparent, 0.9f))
        {
            store.SyncNow();
            RefreshBrowse();
        }
    }

    private void DrawSearchRow(Rect area, float top, float scale)
    {
        var inset = 16f * scale;
        var chipLabel = Loc.T(ScopeLabel());
        var chipWidth = Typography.Measure(chipLabel, 0.8f, FontWeight.SemiBold).X + 24f * scale;
        var gap = Metrics.Space.Sm * scale;
        var searchRect = new Rect(new Vector2(area.Min.X + inset, top + 4f * scale),
            new Vector2(area.Max.X - inset - chipWidth - gap, top + (SearchRowHeight - 6f) * scale));
        SearchField.Draw(searchRect, "##yellowPagesSearch", Loc.T(L.YellowPages.SearchLabel), ref browseSearch,
            AppPalettes.YellowPages, 60);
        if (!string.Equals(browseSearch, browseSearchApplied, StringComparison.Ordinal))
        {
            if (browseSearchEditedAt == 0d)
            {
                browseSearchEditedAt = ImGui.GetTime();
            }

            if (ImGui.GetTime() - browseSearchEditedAt > SearchDebounceSeconds)
            {
                browseSearchApplied = browseSearch;
                browseSearchEditedAt = 0d;
                RefreshBrowse();
            }
        }

        var drawList = ImGui.GetWindowDrawList();
        var chipRect = new Rect(new Vector2(searchRect.Max.X + gap, searchRect.Min.Y),
            new Vector2(searchRect.Max.X + gap + chipWidth, searchRect.Max.Y));
        var hovered = UiInteract.Hover(chipRect.Min, chipRect.Max);
        Squircle.Fill(drawList, chipRect.Min, chipRect.Max, chipRect.Height * 0.5f,
            ImGui.GetColorU32(hovered ? ui.HoverTint : AppPalettes.YellowPages.FieldSurface));
        Squircle.Stroke(drawList, chipRect.Min, chipRect.Max, chipRect.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.45f)), 1f);
        Typography.DrawCentered(drawList, chipRect.Center, chipLabel, ui.Accent, 0.8f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(chipRect.Min, chipRect.Max, hovered))
        {
            configuration.YellowPagesScope = configuration.YellowPagesScope == AdScopes.Everywhere
                ? AdScopes.Region
                : configuration.YellowPagesScope + 1;
            configuration.Save();
            RefreshBrowse();
        }
    }

    private Core.Localization.LocString ScopeLabel()
    {
        return configuration.YellowPagesScope switch
        {
            AdScopes.DataCenter => L.YellowPages.ScopeMyDc,
            AdScopes.Everywhere => L.YellowPages.ScopeEverywhere,
            _ => L.YellowPages.ScopeRegion,
        };
    }

    private void DrawOpenRail(long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cardWidth = RailCardWidth * scale;
        var cardHeight = RailCardHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var fit = Math.Max(1, (int)((width + gap) / (cardWidth + gap)));
        if (railStart > Math.Max(0, openSection.Count - fit))
        {
            railStart = Math.Max(0, openSection.Count - fit);
        }

        var shown = Math.Min(fit, openSection.Count - railStart);
        for (var index = 0; index < shown; index++)
        {
            var ad = openSection[railStart + index];
            var min = new Vector2(origin.X + (cardWidth + gap) * index, origin.Y);
            if (DrawRailCard(drawList, ad, min, new Vector2(min.X + cardWidth, min.Y + cardHeight), nowUnix, scale))
            {
                OpenDetail(ad.Id);
            }
        }

        if (railStart > 0)
        {
            DrawRailChevron(drawList, new Vector2(origin.X + 12f * scale, origin.Y + cardHeight * 0.5f),
                FontAwesomeIcon.ChevronLeft, () => railStart--, scale);
        }

        if (railStart + fit < openSection.Count)
        {
            DrawRailChevron(drawList, new Vector2(origin.X + width - 12f * scale, origin.Y + cardHeight * 0.5f),
                FontAwesomeIcon.ChevronRight, () => railStart++, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Md * scale));
    }

    private bool DrawRailCard(ImDrawListPtr drawList, AdDto ad, Vector2 min, Vector2 max, long nowUnix, float scale)
    {
        var rounding = Metrics.Radius.Md * scale;
        ui.Card(drawList, min, max, rounding, elevated: true);
        var thumbMax = new Vector2(max.X, min.Y + RailThumbHeight * scale);
        var thumb = string.IsNullOrEmpty(ad.MediaUrl) ? null : images.Get(ad.MediaUrl);
        if (thumb is not null)
        {
            var (uv0, uv1) = ImageFit.Cover(thumb.Size.X, thumb.Size.Y, max.X - min.X, thumbMax.Y - min.Y);
            drawList.AddImageRounded(thumb.Handle, min, thumbMax, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersTop);
        }
        else
        {
            Squircle.Fill(drawList, min, thumbMax, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)));
            AppSkin.Icon(drawList, (min + thumbMax) * 0.5f, AdCategories.Icon(ad.Category).ToIconString(),
                Palette.WithAlpha(ui.Accent, 0.85f), 0.9f);
        }

        var pad = 8f * scale;
        var name = Typography.FitText(ad.Title, max.X - min.X - pad * 2f, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + pad, thumbMax.Y + 6f * scale), name,
            AppPalettes.YellowPages.TitleInk, TextStyles.FootnoteEmphasized);
        var state = AdText.OpenState(ad, nowUnix);
        var statusTop = thumbMax.Y + 26f * scale;
        if (state.IsOpen)
        {
            var label = Loc.T(L.YellowPages.OpenNow);
            var labelSize = Typography.Measure(label, TextStyles.Caption1);
            var pillMin = new Vector2(min.X + pad, statusTop);
            var pillMax = pillMin + labelSize + new Vector2(12f * scale, 5f * scale);
            Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
                ImGui.GetColorU32(AdCard.OpenGreen));
            Typography.Draw(drawList, pillMin + new Vector2(6f * scale, 2.5f * scale), label,
                new Vector4(0.03f, 0.08f, 0.05f, 1f), TextStyles.Caption1);
        }
        else if (state.NextOpeningUnix > 0)
        {
            Typography.Draw(drawList, new Vector2(min.X + pad, statusTop),
                Loc.T(L.YellowPages.OpensAt, TimeText.Clock(state.NextOpeningUnix)), ui.Accent,
                TextStyles.Caption1);
        }

        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, min, max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void DrawRailChevron(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon icon, Action step,
        float scale)
    {
        var radius = 12f * scale;
        var hovered = UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, hovered ? 0.72f : 0.55f)), 28);
        AppSkin.Icon(drawList, center, icon.ToIconString(), new Vector4(1f, 1f, 1f, 0.92f), 0.62f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(center - new Vector2(radius, radius), center + new Vector2(radius, radius), hovered))
        {
            step();
        }
    }

    private void DrawCategoryFilters(float scale)
    {
        var intents = AdIntents.All;
        for (var intentIndex = 0; intentIndex < intents.Length; intentIndex++)
        {
            var categories = AdCategories.ForIntent(intents[intentIndex]);
            ui.SectionLabel(Loc.T(AdIntents.Label(intents[intentIndex])));
            for (var index = 0; index < categories.Length; index++)
            {
                chipLabels[index] = Loc.T(AdCategories.Label(categories[index]));
                chipActive[index] = (configuration.YellowPagesCategoryFilter & (1 << categories[index])) != 0;
            }

            var tapped = DrawChipFlow(categories.Length, scale);
            if (tapped >= 0)
            {
                configuration.YellowPagesCategoryFilter ^= 1 << categories[tapped];
                configuration.Save();
                RefreshBrowse();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }

        DrawAfterDarkToggle(scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    private void DrawAfterDarkToggle(float scale)
    {
        var afterDark = configuration.YellowPagesAfterDark;
        ui.ToggleRow(Loc.T(L.YellowPages.AfterDarkToggle), ref afterDark);
        if (afterDark == configuration.YellowPagesAfterDark)
        {
            return;
        }

        if (!afterDark)
        {
            configuration.YellowPagesAfterDark = false;
            configuration.Save();
            RefreshBrowse();
            return;
        }

        confirm.Ask(new Core.Confirm.ConfirmRequest
        {
            Title = Loc.T(L.YellowPages.AfterDarkConfirmTitle),
            Message = Loc.T(L.YellowPages.AfterDarkConfirmBody),
            ConfirmLabel = Loc.T(L.YellowPages.AfterDarkConfirmYes),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Common.Loading),
            FailedMessage = string.Empty,
            ConfirmAsync = done =>
            {
                configuration.YellowPagesAfterDark = true;
                configuration.Save();
                RefreshBrowse();
                done(true);
            },
        });
    }

    private int DrawChipFlow(int count, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var right = origin.X + width;
        var gap = Metrics.Space.Sm * scale;
        var chipHeight = ChipHeight * scale;
        var lineAdvance = chipHeight + gap;
        var cursorX = origin.X;
        var lineTop = origin.Y;
        var tapped = -1;
        for (var index = 0; index < count; index++)
        {
            var label = chipLabels[index];
            var chipWidth = Typography.Measure(label, 0.85f, FontWeight.Medium).X + 26f * scale;
            if (cursorX + chipWidth > right && cursorX > origin.X)
            {
                cursorX = origin.X;
                lineTop += lineAdvance;
            }

            var centerY = lineTop + chipHeight * 0.5f;
            if (ui.FlowChip(ref cursorX, centerY, gap, label, chipActive[index]))
            {
                tapped = index;
            }
        }

        var totalHeight = lineTop + chipHeight - origin.Y;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, totalHeight));
        return tapped;
    }

    private void DrawCards(List<AdDto> items, long nowUnix, float scale)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var ad = items[index];
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = AdCard.Height(ad, width, scale);
            var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            if (ImGui.IsRectVisible(card.Min, card.Max)
                && AdCard.Draw(card, ad, images, lodestone, theme, ui, nowUnix))
            {
                OpenDetail(ad.Id);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + AdCard.Gap * scale));
        }
    }

    private void DrawLoadMore(float scale)
    {
        if (!store.DirectoryHasMore)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 36f * scale;
        if (store.DirectoryLoadingMore)
        {
            LoadingPulse.Spinner(new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f), 9f * scale,
                ui.Accent);
        }
        else
        {
            var label = Loc.T(L.YellowPages.LoadMore);
            var buttonWidth = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 44f * scale;
            var rect = new Rect(new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y),
                new Vector2(origin.X + (width + buttonWidth) * 0.5f, origin.Y + height));
            if (ui.GhostButton(rect, label))
            {
                store.LoadMoreDirectory();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawBrowseEmpty(Rect body, float scale)
    {
        if (store.DirectoryLoading && !store.DirectoryLoadedOnce)
        {
            var origin = ImGui.GetCursorScreenPos();
            LoadingPulse.Draw(new Vector2(body.Center.X, origin.Y + 90f * scale), 13f * scale, ui.Accent,
                AppPalettes.YellowPages.MutedInk, Loc.T(L.Common.Loading));
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 160f * scale));
            return;
        }

        EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.YellowPages.EmptyTitle),
            Loc.T(L.YellowPages.EmptyHint));
    }

    private void EnsureBrowseSections(long nowUnix)
    {
        var directory = store.Directory;
        if (ReferenceEquals(directory, lastDirectory) && nowUnix < nextSectionRebuildUnix)
        {
            return;
        }

        lastDirectory = directory;
        openSection.Clear();
        latestSection.Clear();
        railStart = 0;
        var upcoming = new List<AdDto>();
        var nextBoundary = long.MaxValue;
        for (var index = 0; index < directory.Length; index++)
        {
            var ad = directory[index];
            if (ad.Archetype == AdArchetypes.Place)
            {
                var state = AdText.OpenState(ad, nowUnix);
                if (state.IsOpen)
                {
                    openSection.Add(ad);
                    if (state.ClosesAtUnix > 0)
                    {
                        nextBoundary = Math.Min(nextBoundary, state.ClosesAtUnix);
                    }

                    continue;
                }

                if (state.NextOpeningUnix > 0)
                {
                    nextBoundary = Math.Min(nextBoundary, state.NextOpeningUnix);
                    if (state.NextOpeningUnix - nowUnix <= OpeningSoonLeadSeconds)
                    {
                        upcoming.Add(ad);
                        continue;
                    }
                }
            }

            latestSection.Add(ad);
        }

        openSection.AddRange(upcoming);
        nextSectionRebuildUnix = nextBoundary == long.MaxValue
            ? nowUnix + SectionFallbackRebuildSeconds
            : Math.Min(nextBoundary, nowUnix + SectionFallbackRebuildSeconds);
    }
}
