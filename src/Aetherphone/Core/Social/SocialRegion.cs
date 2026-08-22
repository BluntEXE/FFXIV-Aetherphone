using System.Text;
using Aetherphone.Core.Game;

namespace Aetherphone.Core.Social;

internal static class SocialRegion
{
    public static readonly string[] Codes = { "NA", "EU", "JP", "OCE", "CN" };

    private static readonly int AllRegionsMask = (1 << Codes.Length) - 1;

    public static bool IsValid(string code) => Array.IndexOf(Codes, code) >= 0;

    public static bool MaskShows(int mask, int regionIndex) =>
        mask == 0 || (mask & (1 << regionIndex)) != 0;

    public static int ToggleMask(int mask, int regionIndex)
    {
        var bits = (mask == 0 ? AllRegionsMask : mask) ^ (1 << regionIndex);
        if (bits == 0)
        {
            return mask;
        }

        return bits == AllRegionsMask ? 0 : bits;
    }

    public static string? FilterCsv(int mask)
    {
        if (mask == 0 || mask == AllRegionsMask)
        {
            return null;
        }

        var builder = new StringBuilder(16);
        for (var index = 0; index < Codes.Length; index++)
        {
            if ((mask & (1 << index)) == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(',');
            }

            builder.Append(Codes[index]);
        }

        return builder.ToString();
    }

    public static string AutoCode(GameData gameData)
    {
        var code = gameData.LocalRegionCode();
        return code.Length > 0 ? code : Codes[0];
    }

    public static string EffectiveCode(Configuration configuration, GameData gameData)
    {
        if (configuration.RegionManual && IsValid(configuration.ManualRegion))
        {
            return configuration.ManualRegion;
        }

        return AutoCode(gameData);
    }

    public static string Resolve(string? region, string? world, GameData gameData)
    {
        if (!string.IsNullOrEmpty(region))
        {
            return region;
        }

        return gameData.RegionCodeForWorld(world);
    }
}
