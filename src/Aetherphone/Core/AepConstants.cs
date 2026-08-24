namespace Aetherphone.Core;

internal static class AepConstants
{
    #if DEBUG
    public const string Name = "AetherphoneDev";
    public const string PrimaryCommand = "/phonedev";
    public const string AliasCommand = "/aetherphonedev";
    #else
    public const string Name = "Aetherphone";
    public const string PrimaryCommand = "/phone";
    public const string AliasCommand = "/aetherphone";
    #endif
    public const string DiscordUrl = "https://discord.gg/3HbJCscMyS";
    public const string WebsiteUrl = "https://www.aetherphone.net";
    public const string PatreonUrl = "https://www.patreon.com/XeldarAlz";
    public const string OfficialRepositoryUrl = "https://raw.githubusercontent.com/XeldarAlz/DalamudPlugins/main/repo.json";
    public static readonly string Version = typeof(AepConstants).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
