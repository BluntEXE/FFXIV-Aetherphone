using System.Globalization;
using System.Text;

namespace Aetherphone.Core.Mods;

internal static class ModsContent
{
    public const string AppId = "mods";
    public const string GraphQlUrl = "https://heliosphere.app/api/graphql";
    public const string SiteUrl = "https://heliosphere.app/";
    public const string OneClickSettingsUrl = "https://heliosphere.app/settings/one-click";
    public const string TermsUrl = "https://heliosphere.app/legal/terms";
    public const string SupportUrl = "https://forums.heliosphere.app/";
    public const string RepositoryUrl = "https://repo.heliosphere.app/";
    public const string BridgeUrl = "http://localhost:27389";
    public const string PluginInternalName = "heliosphere-plugin";
    public const string PenumbraInternalName = "Penumbra";
    public const string MetaFileName = "heliosphere.json";
    public const string CoverFileName = "cover.webp";
    public const string InstalledDirectoryPrefix = "hs-";
    public const string DefaultVariantName = "Default";
    public const int PageSize = 24;
    private const string ThumbnailBase = "https://edge.heliosphere.app/images/thumbnails/";
    private const string ImageBase = "https://data.heliosphere.app/images/";

    public static readonly string[] DefaultCategories =
    {
        "animation", "body-replacement", "eyes", "face", "fashion-accessories", "furniture", "gear", "hair", "minion",
        "mount", "other", "racial-scaling", "skin", "sound", "ui", "vfx", "weaponry",
    };

    public static string ThumbnailUrl(string hash) => ThumbnailBase + hash + "/256";

    public static string ImageUrl(string hash) => ImageBase + hash;

    public static string ModPageUrl(Guid packageId) => SiteUrl + "mod/" + Crockford.Encode(packageId);

    public static string TagLabel(string slug)
    {
        if (slug.Length == 0)
        {
            return slug;
        }

        if (string.Equals(slug, "ui", StringComparison.Ordinal) || string.Equals(slug, "vfx", StringComparison.Ordinal))
        {
            return slug.ToUpperInvariant();
        }

        var builder = new StringBuilder(slug.Length);
        for (var index = 0; index < slug.Length; index++)
        {
            var character = slug[index];
            if (character == '-' || character == '_')
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(index == 0 ? char.ToUpperInvariant(character) : character);
        }

        return builder.ToString();
    }

    public static string FormatCount(int count)
    {
        if (count >= 1_000_000)
        {
            return (count / 1_000_000f).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }

        if (count >= 10_000)
        {
            return (count / 1_000f).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }

        return count.ToString("N0", CultureInfo.InvariantCulture);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
        {
            return (bytes / (1024f * 1024f * 1024f)).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
        }

        if (bytes >= 1024L * 1024L)
        {
            return (bytes / (1024f * 1024f)).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
        }

        return MathF.Max(1f, bytes / 1024f).ToString("0", CultureInfo.InvariantCulture) + " KB";
    }
}
