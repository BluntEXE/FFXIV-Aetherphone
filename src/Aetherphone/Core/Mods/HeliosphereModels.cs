using System.Text.Json.Serialization;

namespace Aetherphone.Core.Mods;

internal sealed class GraphQlRequest<TVariables>
{
    public string Query { get; set; } = string.Empty;
    public TVariables? Variables { get; set; }
}

internal sealed class GraphQlError
{
    public string Message { get; set; } = string.Empty;
}

internal sealed class SearchInfo
{
    public string? Name { get; set; }
    public string[] IncludeTags { get; set; } = Array.Empty<string>();
    public string[] ExcludeTags { get; set; } = Array.Empty<string>();
    public string Order { get; set; } = "DOWNLOADS_LAST_MONTH";
}

internal sealed class FilterInfo
{
    public bool Nsfw { get; set; }
    public bool Nsfl { get; set; }
    public bool Cw { get; set; } = true;
    public bool Paid { get; set; } = true;
}

internal sealed class SearchVariables
{
    public SearchInfo Info { get; set; } = new();
    public FilterInfo Filter { get; set; } = new();
    public int Amount { get; set; }
    public int Page { get; set; }
}

internal sealed class PackageVariables
{
    public Guid Id { get; set; }
}

internal sealed class VariantsVariables
{
    public Guid[] Ids { get; set; } = Array.Empty<Guid>();
}

internal sealed class PageInfoDto
{
    public bool Prev { get; set; }
    public bool Next { get; set; }
    public int Total { get; set; }
}

internal sealed class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string VisibleName { get; set; } = string.Empty;
    public bool Subscriber { get; set; }
}

internal sealed class TagDto
{
    public string Slug { get; set; } = string.Empty;
    public bool Category { get; set; }
}

internal sealed class RestrictedDto
{
    public bool Nsfw { get; set; }
    public bool Nsfl { get; set; }
    public bool Cw { get; set; }
}

internal sealed class ImageDto
{
    public int Id { get; set; }
    public string Hash { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

internal sealed class VersionDto
{
    public Guid Id { get; set; }
    public Guid VariantId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? Changelog { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string[] Affects { get; set; } = Array.Empty<string>();
    public long DownloadSize { get; set; }
    public long InstallSize { get; set; }
    public VariantDto? Variant { get; set; }
}

internal sealed class VariantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ShortId { get; set; }
    public int DisplayOrder { get; set; }
    public PackageDto? Package { get; set; }
    public VersionDto[] Versions { get; set; } = Array.Empty<VersionDto>();
}

internal sealed class PackageDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Permissions { get; set; }
    public string? ContentWarning { get; set; }
    public int? Downloads { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? VanityUrl { get; set; }
    public UserDto? User { get; set; }
    public TagDto[] Tags { get; set; } = Array.Empty<TagDto>();
    public RestrictedDto? Nsfw { get; set; }
    public ImageDto[] Images { get; set; } = Array.Empty<ImageDto>();
    public VariantDto[] Variants { get; set; } = Array.Empty<VariantDto>();
}

internal sealed class SearchResultDto
{
    public PageInfoDto PageInfo { get; set; } = new();
    public VersionDto[] Versions { get; set; } = Array.Empty<VersionDto>();
}

internal sealed class SearchData
{
    public SearchResultDto? SearchVersions { get; set; }
}

internal sealed class SearchEnvelope
{
    public SearchData? Data { get; set; }
    public GraphQlError[]? Errors { get; set; }
}

internal sealed class PackageData
{
    public PackageDto? Package { get; set; }
}

internal sealed class PackageEnvelope
{
    public PackageData? Data { get; set; }
    public GraphQlError[]? Errors { get; set; }
}

internal sealed class VariantsData
{
    public VariantDto[] Variants { get; set; } = Array.Empty<VariantDto>();
}

internal sealed class VariantsEnvelope
{
    public VariantsData? Data { get; set; }
    public GraphQlError[]? Errors { get; set; }
}

internal sealed class TagsData
{
    public TagDto[] CategoryTags { get; set; } = Array.Empty<TagDto>();
}

internal sealed class TagsEnvelope
{
    public TagsData? Data { get; set; }
    public GraphQlError[]? Errors { get; set; }
}

internal sealed class BridgeInstallRequest
{
    public Guid PackageId { get; set; }
    public Guid VariantId { get; set; }
    public Guid VersionId { get; set; }
    public string? OneClickPassword { get; set; }
}


internal sealed class HeliosphereMeta
{
    public uint MetaVersion { get; set; }
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Guid VersionId { get; set; }
    public string Variant { get; set; } = string.Empty;
    public Guid VariantId { get; set; }
    public uint ShortVariantId { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GraphQlRequest<SearchVariables>))]
[JsonSerializable(typeof(GraphQlRequest<PackageVariables>))]
[JsonSerializable(typeof(GraphQlRequest<VariantsVariables>))]
[JsonSerializable(typeof(GraphQlRequest<object>))]
[JsonSerializable(typeof(SearchEnvelope))]
[JsonSerializable(typeof(PackageEnvelope))]
[JsonSerializable(typeof(VariantsEnvelope))]
[JsonSerializable(typeof(TagsEnvelope))]
[JsonSerializable(typeof(BridgeInstallRequest))]
[JsonSerializable(typeof(HeliosphereMeta))]
internal sealed partial class HeliosphereJsonContext : JsonSerializerContext;
