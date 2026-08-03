namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record RadioLinkDto(int Kind, string Url);

internal sealed record RadioStationDto(
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
    RadioLinkDto[] Links,
    bool IsLive,
    int Listeners,
    string NowPlaying,
    long LastLiveAtUnix,
    long CreatedAtUnix);

internal sealed record RadioStationPage(RadioStationDto[] Items, string? NextCursor);

internal sealed record UpdateRadioStationRequest(
    string Name,
    string Description,
    string[]? Tags,
    RadioLinkDto[]? Links,
    string? MediaKey);

internal sealed record RadioCredentialsDto(
    string Host,
    int Port,
    string Mount,
    string Username,
    string Password,
    string Format,
    int Bitrate,
    int SampleRate);

internal sealed record MyRadioStationDto(RadioStationDto Station, RadioCredentialsDto Credentials);
