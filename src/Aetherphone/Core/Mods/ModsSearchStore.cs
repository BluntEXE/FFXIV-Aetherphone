using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Mods;

internal enum ModsSearchState : byte
{
    Idle,
    Loading,
    Ready,
    Failed,
}

internal sealed class ModCardModel
{
    public required Guid PackageId { get; init; }
    public required Guid VariantId { get; init; }
    public required Guid VersionId { get; init; }
    public required string Key { get; init; }
    public required string MarqueeId { get; init; }
    public required string Name { get; init; }
    public required string Author { get; init; }
    public required string ByLine { get; init; }
    public required string Tagline { get; init; }
    public required string Version { get; init; }
    public required string CategoryLabel { get; init; }
    public required string DownloadsText { get; init; }
    public required string ThumbnailUrl { get; init; }
    public required bool Nsfw { get; init; }
    public required bool Nsfl { get; init; }
    public required bool ContentWarning { get; init; }

    public bool Sensitive => Nsfw || Nsfl;

    public static ModCardModel? From(VersionDto version)
    {
        var variant = version.Variant;
        var package = variant?.Package;
        if (variant is null || package is null)
        {
            return null;
        }

        var key = package.Id.ToString("N");
        var author = package.User?.VisibleName ?? string.Empty;
        return new ModCardModel
        {
            PackageId = package.Id,
            VariantId = variant.Id,
            VersionId = version.Id,
            Key = key,
            MarqueeId = "mods.card.title." + key,
            Name = package.Name,
            Author = author,
            ByLine = author.Length == 0 ? string.Empty : Loc.T(L.Mods.By, author),
            Tagline = package.Tagline,
            Version = version.Version,
            CategoryLabel = FirstCategory(package.Tags),
            DownloadsText = ModsContent.FormatCount(package.Downloads ?? 0),
            ThumbnailUrl = CoverThumbnail(package.Images),
            Nsfw = package.Nsfw?.Nsfw ?? false,
            Nsfl = package.Nsfw?.Nsfl ?? false,
            ContentWarning = package.Nsfw?.Cw ?? false,
        };
    }

    public static string FirstCategory(TagDto[] tags)
    {
        for (var index = 0; index < tags.Length; index++)
        {
            if (tags[index].Category)
            {
                return ModsContent.TagLabel(tags[index].Slug);
            }
        }

        return string.Empty;
    }

    public static string CoverThumbnail(ImageDto[] images)
    {
        var hash = CoverHash(images);
        return hash.Length == 0 ? string.Empty : ModsContent.ThumbnailUrl(hash);
    }

    public static string CoverHash(ImageDto[] images)
    {
        var best = -1;
        for (var index = 0; index < images.Length; index++)
        {
            if (images[index].Hash.Length == 0)
            {
                continue;
            }

            if (best < 0 || images[index].DisplayOrder < images[best].DisplayOrder)
            {
                best = index;
            }
        }

        return best < 0 ? string.Empty : images[best].Hash;
    }
}

internal sealed class ModsSearchStore : IDisposable
{
    private readonly HeliosphereApi api;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? active;
    private int generation;
    private int nextPage;
    private volatile ModCardModel[] results = Array.Empty<ModCardModel>();
    private volatile ModsSearchState state = ModsSearchState.Idle;
    private volatile bool loadingMore;
    private volatile bool hasMore;
    private volatile int total;
    private volatile int version;

    public ModsSearchStore(HeliosphereApi api)
    {
        this.api = api;
    }

    public ModCardModel[] Results => results;
    public ModsSearchState State => state;
    public bool LoadingMore => loadingMore;
    public bool HasMore => hasMore;
    public int Total => total;
    public int Version => version;
    public ModsQuery Query { get; private set; } = ModsQuery.Default;

    public void Search(ModsQuery query)
    {
        Query = query;
        Restart();
    }

    public void Retry() => Restart();

    public void LoadMore()
    {
        if (loadingMore || !hasMore || state != ModsSearchState.Ready)
        {
            return;
        }

        loadingMore = true;
        var token = active?.Token ?? lifetime.Token;
        _ = FetchAsync(Query, nextPage, generation, token);
    }

    private void Restart()
    {
        active?.Cancel();
        active?.Dispose();
        active = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        generation++;
        nextPage = 0;
        results = Array.Empty<ModCardModel>();
        hasMore = false;
        total = 0;
        loadingMore = false;
        state = ModsSearchState.Loading;
        version++;
        _ = FetchAsync(Query, 0, generation, active.Token);
    }

    private async Task FetchAsync(ModsQuery query, int page, int requestGeneration, CancellationToken token)
    {
        SearchResultDto? result;
        try
        {
            result = await Task.Run(() => api.SearchAsync(query, page, token), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Mods] search failed");
            result = null;
        }

        if (requestGeneration != generation || token.IsCancellationRequested)
        {
            return;
        }

        if (result is null)
        {
            if (page == 0)
            {
                state = ModsSearchState.Failed;
            }

            loadingMore = false;
            version++;
            return;
        }

        Publish(result, page);
    }

    private void Publish(SearchResultDto result, int page)
    {
        var existing = page == 0 ? Array.Empty<ModCardModel>() : results;
        var merged = new List<ModCardModel>(existing.Length + result.Versions.Length);
        merged.AddRange(existing);
        for (var index = 0; index < result.Versions.Length; index++)
        {
            var card = ModCardModel.From(result.Versions[index]);
            if (card is not null && !Contains(merged, card.PackageId))
            {
                merged.Add(card);
            }
        }

        results = merged.ToArray();
        nextPage = page + 1;
        hasMore = result.PageInfo.Next && result.Versions.Length > 0;
        total = result.PageInfo.Total;
        loadingMore = false;
        state = ModsSearchState.Ready;
        version++;
    }

    private static bool Contains(List<ModCardModel> cards, Guid packageId)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            if (cards[index].PackageId == packageId)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        active?.Cancel();
        active?.Dispose();
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
