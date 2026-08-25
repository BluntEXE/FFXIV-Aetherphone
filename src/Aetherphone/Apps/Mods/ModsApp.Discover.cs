using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Mods;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Mods;

internal sealed partial class ModsApp
{
    private const float SearchRowHeight = 46f;
    private const float CreditsRowHeight = 32f;
    private const double SearchDebounceSeconds = 0.5;

    private readonly ChipRail categoryRail = new();
    private readonly DropdownMenu sortMenu = new();
    private readonly DropdownMenu.Item[] sortItems = new DropdownMenu.Item[ModsQuery.SortCount];
    private string[] categorySlugs = ModsContent.DefaultCategories;
    private string[] categoryLabels = Array.Empty<string>();
    private bool[] categoryActive = Array.Empty<bool>();
    private int categoryIndex;
    private string searchText = string.Empty;
    private string searchApplied = string.Empty;
    private double searchEditedAt;
    private bool searchPending;
    private bool categoriesRequested;
    private string[]? fetchedCategories;

    private void DrawDiscover(Rect area)
    {
        var scale = UiScale.Current;
        DrawDiscoverHeader(area, scale);
        var controlsTop = area.Min.Y + AppHeader.Height * scale;
        DrawSearchRow(area, controlsTop, scale);
        var railTop = controlsTop + SearchRowHeight * scale;
        var railRow = new Rect(new Vector2(area.Min.X, railTop),
            new Vector2(area.Max.X, railTop + ChipRail.RowHeight * scale));
        DrawCategoryRail(railRow);
        ApplyPendingSearch();
        var body = new Rect(new Vector2(area.Min.X, railRow.Max.Y + Metrics.Space.Xs * scale), area.Max);
        var search = hub.Search;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            var results = search.Results;
            if (results.Length == 0)
            {
                DrawDiscoverState(body, search, scale);
            }
            else
            {
                for (var index = 0; index < results.Length; index++)
                {
                    DrawResultCard(results[index], scale);
                }

                if (search.LoadingMore)
                {
                    InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
                }
                else if (search.HasMore && InfiniteScroll.ReachedBottom())
                {
                    search.LoadMore();
                }

                DrawCreditsRow(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawDiscoverHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var inset = 16f * scale;
        var actionCenter = new Vector2(area.Max.X - inset - 11f * scale, rowCenterY);
        var sortHit = new Vector2(14f * scale, 14f * scale);
        if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.SortAmountDown.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 0.82f, Loc.T(L.Mods.Sort), HoverLabelSide.Below))
        {
            sortMenu.Toggle("mods.sort", new Rect(actionCenter - sortHit, actionCenter + sortHit));
        }

        var spinnerCenter = new Vector2(actionCenter.X - 28f * scale, rowCenterY);
        if (hub.Search.State == ModsSearchState.Loading)
        {
            LoadingPulse.Spinner(spinnerCenter, 8f * scale, ui.Accent);
        }

        var title = DisplayName;
        var maxWidth = MathF.Max(1f, area.Width - inset * 2f - 60f * scale);
        var titleY = rowCenterY - Typography.Measure(title, TextStyles.Title2).Y * 0.5f;
        Marquee.DrawLeftAuto(ImGui.GetWindowDrawList(), "mods.title", title, area.Min.X + inset, titleY, maxWidth,
            TextStyles.Title2, ui.TitleInk);
    }

    private void DrawSearchRow(Rect area, float top, float scale)
    {
        var inset = 16f * scale;
        var barHeight = Metrics.Size.FieldHeight * scale;
        var bar = new Rect(new Vector2(area.Min.X + inset, top + (SearchRowHeight * scale - barHeight) * 0.5f),
            new Vector2(area.Max.X - inset, top + (SearchRowHeight * scale + barHeight) * 0.5f));
        var before = searchText;
        SearchField.Draw(bar, "mods.search", Loc.T(L.Mods.SearchHint), ref searchText, ui.Palette);
        if (!ReferenceEquals(before, searchText) && !string.Equals(before, searchText, StringComparison.Ordinal))
        {
            searchEditedAt = ImGui.GetTime();
            searchPending = true;
        }
    }

    private void ApplyPendingSearch()
    {
        if (!searchPending || ImGui.GetTime() - searchEditedAt < SearchDebounceSeconds)
        {
            return;
        }

        searchPending = false;
        searchApplied = searchText;
        EnsureSearch();
    }

    private void DrawCategoryRail(Rect row)
    {
        AdoptFetchedCategories();
        if (categoryLabels.Length != categorySlugs.Length + 1)
        {
            RebuildCategoryLabels();
        }

        for (var index = 0; index < categoryActive.Length; index++)
        {
            categoryActive[index] = index == categoryIndex;
        }

        var tapped = categoryRail.Draw(row, ui, categoryLabels, categoryActive);
        if (tapped < 0 || tapped == categoryIndex)
        {
            return;
        }

        categoryIndex = tapped;
        EnsureSearch();
    }

    private void RebuildCategoryLabels()
    {
        categoryLabels = new string[categorySlugs.Length + 1];
        categoryActive = new bool[categorySlugs.Length + 1];
        categoryLabels[0] = Loc.T(L.Mods.AllCategories);
        for (var index = 0; index < categorySlugs.Length; index++)
        {
            categoryLabels[index + 1] = ModsContent.TagLabel(categorySlugs[index]);
        }

        if (categoryIndex >= categoryLabels.Length)
        {
            categoryIndex = 0;
        }
    }

    private void EnsureCategories()
    {
        if (categoriesRequested)
        {
            return;
        }

        categoriesRequested = true;
        work.Run("categories", async token =>
        {
            var tags = await hub.Api.CategoryTagsAsync(token).ConfigureAwait(false);
            if (tags is null || tags.Length == 0)
            {
                return;
            }

            var slugs = new List<string>(tags.Length);
            for (var index = 0; index < tags.Length; index++)
            {
                if (tags[index].Category && tags[index].Slug.Length > 0)
                {
                    slugs.Add(tags[index].Slug);
                }
            }

            slugs.Sort(StringComparer.Ordinal);
            Volatile.Write(ref fetchedCategories, slugs.ToArray());
        });
    }

    private void AdoptFetchedCategories()
    {
        var fetched = Interlocked.Exchange(ref fetchedCategories, null);
        if (fetched is null || fetched.Length == 0 || SameSlugs(fetched, categorySlugs))
        {
            return;
        }

        var selectedSlug = SelectedCategory();
        categorySlugs = fetched;
        RebuildCategoryLabels();
        categoryIndex = 0;
        for (var index = 0; index < categorySlugs.Length; index++)
        {
            if (string.Equals(categorySlugs[index], selectedSlug, StringComparison.Ordinal))
            {
                categoryIndex = index + 1;
                break;
            }
        }
    }

    private static bool SameSlugs(string[] left, string[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private string SelectedCategory() =>
        categoryIndex > 0 && categoryIndex - 1 < categorySlugs.Length ? categorySlugs[categoryIndex - 1] : string.Empty;

    private void EnsureSearch()
    {
        var query = new ModsQuery(searchApplied, SelectedCategory(), snapshot.SortKind, snapshot.Filter);
        if (hub.Search.State != ModsSearchState.Idle && hub.Search.Query == query)
        {
            return;
        }

        hub.Search.Search(query);
    }

    private void DrawDiscoverState(Rect body, ModsSearchStore search, float scale)
    {
        switch (search.State)
        {
            case ModsSearchState.Failed:
                if (EmptyState.Draw(body, ui, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Mods.LoadFailed),
                        Loc.T(L.Mods.LoadFailedHint), Loc.T(L.Mods.Retry)))
                {
                    search.Retry();
                }

                break;
            case ModsSearchState.Ready:
                EmptyState.Draw(body, ui, FontAwesomeIcon.Search, Loc.T(L.Mods.NoResults),
                    Loc.T(L.Mods.NoResultsHint));
                break;
            default:
                LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 110f * scale), 13f * scale, ui.Accent,
                    ui.MutedInk, Loc.T(L.Mods.Loading));
                break;
        }
    }

    private void DrawResultCard(ModCardModel model, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ModCard.Height * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (ImGui.IsRectVisible(card.Min, card.Max))
        {
            var thumbnail = model.ThumbnailUrl.Length == 0
                ? null
                : images.Sized(model.ThumbnailUrl, ModCard.ThumbSide * scale);
            var status = string.Empty;
            var statusColor = ModCard.InstalledGreen;
            if (hub.Library.TryFindVariant(model.VariantId, out var installed))
            {
                status = Loc.T(installed.HasUpdate ? L.Mods.BadgeUpdate : L.Mods.BadgeInstalled);
                statusColor = installed.HasUpdate ? ModCard.UpdateGold : ModCard.InstalledGreen;
            }

            var veil = model.Sensitive && snapshot.BlurSensitive;
            if (ModCard.Draw(card, model, thumbnail, ui, veil, status, statusColor))
            {
                OpenDetail(model.PackageId);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + ModCard.Gap * scale));
    }

    private void DrawCreditsRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + CreditsRowHeight * scale));
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var ink = hovered ? ui.BodyInk : ui.MutedInk;
        Typography.DrawCentered(ImGui.GetWindowDrawList(), row.Center, Loc.T(L.Mods.PoweredBy), ink,
            TextStyles.FootnoteEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            UrlActions.AskThenOpen(ModsContent.SiteUrl);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, CreditsRowHeight * scale));
    }

    private void DrawSortMenu(Rect screen)
    {
        var current = snapshot.SortKind;
        sortItems[0] = new DropdownMenu.Item(Loc.T(L.Mods.SortTrending), Selected: current == ModsSort.Trending);
        sortItems[1] = new DropdownMenu.Item(Loc.T(L.Mods.SortPopular), Selected: current == ModsSort.Popular);
        sortItems[2] = new DropdownMenu.Item(Loc.T(L.Mods.SortNewest), Selected: current == ModsSort.Newest);
        sortItems[3] = new DropdownMenu.Item(Loc.T(L.Mods.SortUpdated), Selected: current == ModsSort.Updated);
        var picked = sortMenu.Draw(screen, theme, sortItems);
        if (picked < 0 || picked == (int)current)
        {
            return;
        }

        snapshot.Sort = picked;
        SnapshotChanged();
        PersistSnapshot();
    }
}
