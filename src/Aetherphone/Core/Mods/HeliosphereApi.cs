using Aetherphone.Core.Net;

namespace Aetherphone.Core.Mods;

internal sealed class HeliosphereApi
{
    private const string CardFields =
        "id name tagline downloads updatedAt user { id username visibleName subscriber } tags { slug category } "
        + "nsfw { nsfw nsfl cw } images { id hash displayOrder }";

    private const string SearchQuery =
        "query Search($info: SearchRequest!, $filter: FilterInfo, $amount: Int!, $page: Int) { "
        + "searchVersions(info: $info, filterInfo: $filter, amount: $amount, page: $page) { "
        + "pageInfo { prev next total } versions { id variantId version createdAt "
        + "variant { id name shortId displayOrder package { " + CardFields + " } } } } }";

    private const string PackageQuery =
        "query Package($id: UUID!) { package(id: $id) { " + CardFields
        + " description permissions contentWarning createdAt vanityUrl "
        + "variants { id name shortId displayOrder versions(limit: 1) { id variantId version changelog createdAt "
        + "affects downloadSize installSize } } } }";

    private const string VariantsQuery =
        "query Variants($ids: [UUID!]!) { variants(ids: $ids) { id name shortId displayOrder "
        + "package { id name images { id hash displayOrder } } "
        + "versions(limit: 1) { id variantId version changelog createdAt } } }";

    private const string CategoriesQuery = "{ categoryTags { slug category } }";

    private readonly HttpService http;

    public HeliosphereApi(HttpService http)
    {
        this.http = http;
    }

    public async Task<SearchResultDto?> SearchAsync(ModsQuery query, int page, CancellationToken token)
    {
        var request = new GraphQlRequest<SearchVariables>
        {
            Query = SearchQuery,
            Variables = new SearchVariables
            {
                Info = query.ToInfo(),
                Filter = query.Filter.ToInfo(),
                Amount = ModsContent.PageSize,
                Page = page,
            },
        };
        var envelope = await http.PostJsonAsync(ModsContent.GraphQlUrl, request,
                HeliosphereJsonContext.Default.GraphQlRequestSearchVariables,
                HeliosphereJsonContext.Default.SearchEnvelope, null, token)
            .ConfigureAwait(false);
        if (envelope is null || !Accepted(envelope.Errors, "search"))
        {
            return null;
        }

        return envelope.Data?.SearchVersions;
    }

    public async Task<PackageDto?> PackageAsync(Guid id, CancellationToken token)
    {
        var request = new GraphQlRequest<PackageVariables>
        {
            Query = PackageQuery,
            Variables = new PackageVariables { Id = id },
        };
        var envelope = await http.PostJsonAsync(ModsContent.GraphQlUrl, request,
                HeliosphereJsonContext.Default.GraphQlRequestPackageVariables,
                HeliosphereJsonContext.Default.PackageEnvelope, null, token)
            .ConfigureAwait(false);
        if (envelope is null || !Accepted(envelope.Errors, "package"))
        {
            return null;
        }

        return envelope.Data?.Package;
    }

    public async Task<VariantDto[]?> VariantsAsync(Guid[] ids, CancellationToken token)
    {
        if (ids.Length == 0)
        {
            return Array.Empty<VariantDto>();
        }

        var request = new GraphQlRequest<VariantsVariables>
        {
            Query = VariantsQuery,
            Variables = new VariantsVariables { Ids = ids },
        };
        var envelope = await http.PostJsonAsync(ModsContent.GraphQlUrl, request,
                HeliosphereJsonContext.Default.GraphQlRequestVariantsVariables,
                HeliosphereJsonContext.Default.VariantsEnvelope, null, token)
            .ConfigureAwait(false);
        if (envelope is null || !Accepted(envelope.Errors, "variants"))
        {
            return null;
        }

        return envelope.Data?.Variants;
    }

    public async Task<TagDto[]?> CategoryTagsAsync(CancellationToken token)
    {
        var request = new GraphQlRequest<object> { Query = CategoriesQuery };
        var envelope = await http.PostJsonAsync(ModsContent.GraphQlUrl, request,
                HeliosphereJsonContext.Default.GraphQlRequestObject,
                HeliosphereJsonContext.Default.TagsEnvelope, null, token)
            .ConfigureAwait(false);
        if (envelope is null || !Accepted(envelope.Errors, "categories"))
        {
            return null;
        }

        return envelope.Data?.CategoryTags;
    }

    private static bool Accepted(GraphQlError[]? errors, string operation)
    {
        if (errors is null || errors.Length == 0)
        {
            return true;
        }

        AepLog.Warning($"[Mods] Heliosphere {operation} query failed: {errors[0].Message}");
        return false;
    }
}
