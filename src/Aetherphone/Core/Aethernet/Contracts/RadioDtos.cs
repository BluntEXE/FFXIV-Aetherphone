namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CommunityLinkDto(int Kind, string Url);

internal sealed record CommunityStationDto(
    string Id,
    string OwnerId,
    string OwnerDisplayName,
    string OwnerHandle,
    string OwnerAvatarUrl,
    int OwnerBadges,
    string Slug,
    string Name,
    string Description,
    string[] Tags,
    string ArtworkUrl,
    string StreamUrl,
    CommunityLinkDto[] Links,
    bool IsLive,
    int Listeners,
    string NowPlaying,
    long LastLiveAtUnix,
    long CreatedAtUnix);

internal sealed record CommunityStationPage(CommunityStationDto[] Items, string? NextCursor);

internal sealed record UpdateCommunityStationRequest(
    string Name,
    string Description,
    string[]? Tags,
    CommunityLinkDto[]? Links,
    string? MediaKey);

internal sealed record CommunityCredentialsDto(
    string Host,
    int Port,
    string Mount,
    string Username,
    string Password,
    string Format,
    int Bitrate,
    int SampleRate);

internal sealed record MyCommunityStationDto(CommunityStationDto Station, CommunityCredentialsDto Credentials);
