using Aetherphone.Core.Localization;
using System.Text.Json.Serialization;

namespace Aetherphone.Apps.Music.Rolladeck;

internal sealed class LiveResponse
{
    [JsonPropertyName("liveDJs")]      public List<LiveDjEntry>      LiveDJs      { get; set; } = [];
    [JsonPropertyName("openVenues")]   public List<OpenVenueEntry>   OpenVenues   { get; set; } = [];
    [JsonPropertyName("activeEvents")] public List<ActiveEventEntry> ActiveEvents { get; set; } = [];
}

internal sealed class LiveDjEntry
{
    [JsonPropertyName("djName")]    public string  DjName    { get; set; } = "";
    [JsonPropertyName("djSlug")]    public string? DjSlug    { get; set; }
    [JsonPropertyName("twitchUrl")] public string? TwitchUrl { get; set; }
    [JsonPropertyName("venueName")] public string? VenueName { get; set; }
    [JsonPropertyName("server")]    public string? Server    { get; set; }
    [JsonPropertyName("datacenter")] public string? Datacenter { get; set; }
    [JsonPropertyName("district")]  public string? District  { get; set; }

    [JsonPropertyName("ward")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Ward { get; set; }

    [JsonPropertyName("plot")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Plot { get; set; }

    [JsonPropertyName("viewerCount")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int ViewerCount { get; set; }

    [JsonPropertyName("lifestream")]  public string? LifestreamArg { get; set; }
    [JsonPropertyName("streamTitle")] public string? StreamTitle   { get; set; }
    [JsonPropertyName("avatarUrl")]   public string? AvatarUrl     { get; set; }
    [JsonPropertyName("eventName")]   public string? EventName     { get; set; }
    [JsonPropertyName("genres")]        public List<string> Genres        { get; set; } = [];
    [JsonPropertyName("bio")]           public string? Bio           { get; set; }
    [JsonPropertyName("discordName")]   public string? DiscordName   { get; set; }
    [JsonPropertyName("twitter")]       public string? Twitter       { get; set; }
    [JsonPropertyName("bluesky")]       public string? Bluesky       { get; set; }
    [JsonPropertyName("instagram")]     public string? Instagram     { get; set; }
    [JsonPropertyName("youtube")]       public string? Youtube       { get; set; }
    [JsonPropertyName("tiktok")]        public string? Tiktok        { get; set; }
    [JsonPropertyName("website")]       public string? Website       { get; set; }
    [JsonPropertyName("musicPlatform")] public string? MusicPlatform { get; set; }

    public bool    CanTeleport   => !string.IsNullOrEmpty(LifestreamArg);
    public string  ServerLabel   => Server ?? Datacenter ?? "";
    public string? RolladeckUrl  => TwitchUsername != null ? $"https://xivrolladeck.com/{TwitchUsername}" : null;

    public string  NormalizedName  => RolladeckText.Normalize(DjName);
    public string? NormalizedTitle => string.IsNullOrEmpty(StreamTitle) ? null : RolladeckText.Normalize(StreamTitle);

    public string? TwitchUsername
    {
        get
        {
            if (TwitchUrl == null) return null;
            var uri        = TwitchUrl.TrimEnd('/');
            var slashIndex = uri.LastIndexOf('/');
            return slashIndex >= 0 ? uri[(slashIndex + 1)..] : null;
        }
    }

    public string FormattedAddress =>
        District != null && Ward.HasValue && Plot.HasValue
            ? $"{District} W{Ward} P{Plot}"
            : District ?? "";
}

internal sealed class OpenVenueEntry
{
    [JsonPropertyName("name")]      public string? Name       { get; set; }
    [JsonPropertyName("slug")]      public string? Slug       { get; set; }
    [JsonPropertyName("server")]    public string? Server     { get; set; }
    [JsonPropertyName("datacenter")] public string? Datacenter { get; set; }
    [JsonPropertyName("district")]  public string? District   { get; set; }

    [JsonPropertyName("ward")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Ward { get; set; }

    [JsonPropertyName("plot")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Plot { get; set; }

    [JsonPropertyName("logoUrl")]   public string? LogoUrl    { get; set; }
    [JsonPropertyName("lifestream")] public string? Lifestream { get; set; }
    [JsonPropertyName("openReason")] public string? OpenReason { get; set; }
    [JsonPropertyName("eventName")] public string? EventName  { get; set; }
    [JsonPropertyName("djName")]    public string? DjName     { get; set; }
    [JsonPropertyName("djTwitch")]  public string? DjTwitch   { get; set; }
    [JsonPropertyName("activeDiscordEventName")] public string? DiscordEventName { get; set; }
    [JsonPropertyName("description")]    public string? Description    { get; set; }
    [JsonPropertyName("firstOpened")]    public string? FirstOpened    { get; set; }
    [JsonPropertyName("websiteOrCarrd")] public string? WebsiteOrCarrd { get; set; }
    [JsonPropertyName("discordServer")]  public string? DiscordServer  { get; set; }
    [JsonPropertyName("amenities")] public List<string> Amenities { get; set; } = [];

    public bool   CanTeleport    => !string.IsNullOrEmpty(Lifestream);
    public string ServerLabel    => Server ?? Datacenter ?? "";
    public string DisplayName    => Name ?? "Unknown Venue";
    public string? RolladeckUrl  => Slug != null ? $"https://xivrolladeck.com/venue/{Slug}" : null;

    public string FormattedAddress =>
        District != null && Ward.HasValue && Plot.HasValue
            ? $"{District} W{Ward} P{Plot}"
            : District ?? "";

    public string? ActiveLabel => EventName ?? DjName ?? DiscordEventName;

    public string? WebsiteUrl => WebsiteOrCarrd;
    public string? DiscordUrl => DiscordServer;
    public string? FirstOpenedYear
    {
        get
        {
            if (string.IsNullOrEmpty(FirstOpened)) return null;
            if (DateTime.TryParse(FirstOpened, out var dt)) return dt.ToString("MMM yyyy");
            return null;
        }
    }
}

internal sealed class EventLineupEntry
{
    [JsonPropertyName("djName")]   public string? DjName   { get; set; }
    [JsonPropertyName("timeSlot")] public string? TimeSlot { get; set; }
}

internal sealed class ActiveEventEntry
{
    [JsonPropertyName("name")]        public string? Name        { get; set; }
    [JsonPropertyName("startDate")]   public string? StartDate   { get; set; }
    [JsonPropertyName("endDate")]     public string? EndDate     { get; set; }
    [JsonPropertyName("bannerUrl")]   public string? BannerUrl   { get; set; }
    [JsonPropertyName("server")]      public string? Server      { get; set; }
    [JsonPropertyName("datacenter")]  public string? Datacenter  { get; set; }
    [JsonPropertyName("district")]    public string? District    { get; set; }

    [JsonPropertyName("ward")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Ward { get; set; }

    [JsonPropertyName("plot")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Plot { get; set; }

    [JsonPropertyName("lineup")] public List<EventLineupEntry> Lineup { get; set; } = [];
}

internal sealed class ScheduleEntry
{
    [JsonPropertyName("type")]       public string? Type       { get; set; }
    [JsonPropertyName("id")]         public string? Id         { get; set; }
    [JsonPropertyName("name")]       public string? Name       { get; set; }
    [JsonPropertyName("djName")]     public string? DjName     { get; set; }
    [JsonPropertyName("djId")]       public string? DjId       { get; set; }
    [JsonPropertyName("djSlug")]     public string? DjSlug     { get; set; }
    [JsonPropertyName("avatarUrl")]  public string? AvatarUrl  { get; set; }
    [JsonPropertyName("venueName")]  public string? VenueName  { get; set; }
    [JsonPropertyName("venueId")]    public string? VenueId    { get; set; }
    [JsonPropertyName("venueSlug")]  public string? VenueSlug  { get; set; }
    [JsonPropertyName("startDate")]  public string? StartDate  { get; set; }
    [JsonPropertyName("endDate")]    public string? EndDate    { get; set; }
    [JsonPropertyName("server")]     public string? Server     { get; set; }
    [JsonPropertyName("datacenter")] public string? Datacenter { get; set; }
    [JsonPropertyName("district")]   public string? District   { get; set; }

    [JsonPropertyName("ward")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Ward { get; set; }

    [JsonPropertyName("plot")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Plot { get; set; }

    public string DisplayName => Name ?? VenueName ?? DjName ?? "Event";
    public string ServerLabel => Server ?? Datacenter ?? "";
    public bool   IsEvent     => Type == "event";

    public string? DjUrl    => DjSlug    != null ? $"https://xivrolladeck.com/{DjSlug}"          : null;
    public string? VenueUrl => VenueSlug != null ? $"https://xivrolladeck.com/venue/{VenueSlug}" : null;

    public string? EventUrl => Id != null ? $"https://xivrolladeck.com/event/{Id}" : null;

    public string? ClickUrl => Type switch
    {
        "set"     => DjUrl    ?? VenueUrl,
        "booking" => DjUrl    ?? VenueUrl,
        "discord" => VenueUrl,
        "event"   => EventUrl ?? VenueUrl,
        _         => null,
    };

    public string FormattedAddress =>
        District != null && Ward.HasValue && Plot.HasValue
            ? $"{District} W{Ward} P{Plot}"
            : District ?? "";

    public string TimeLabel
    {
        get
        {
            if (!DateTime.TryParse(StartDate, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var startParsed)) return "";
            return TimeText.Clock(startParsed.ToLocalTime());
        }
    }

    public string DateLabel
    {
        get
        {
            if (!DateTime.TryParse(StartDate, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var startDate)) return "";
            if (!DateTime.TryParse(EndDate, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var endDate))
                return startDate.ToLocalTime().ToString("MMM d");
            var localStart = startDate.ToLocalTime();
            var localEnd   = endDate.ToLocalTime();
            return localStart.Date == localEnd.Date
                ? localStart.ToString("MMM d")
                : $"{localStart:MMM d} – {localEnd:MMM d}";
        }
    }
}

internal sealed class ScheduleResponse
{
    [JsonPropertyName("schedule")] public List<ScheduleEntry> Schedule { get; set; } = [];
}

[JsonSerializable(typeof(LiveResponse))]
[JsonSerializable(typeof(ScheduleResponse))]
internal partial class RolladeckJsonContext : JsonSerializerContext { }
