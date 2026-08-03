using System.Collections.Frozen;

namespace Aetherphone.Core.Housing;

internal static class HousingRegions
{
    public const string Unknown = "Other";

    private static readonly FrozenDictionary<string, string> ByDataCenter = new Dictionary<string, string>
    {
        ["Aether"] = "North America",
        ["Primal"] = "North America",
        ["Crystal"] = "North America",
        ["Dynamis"] = "North America",
        ["Chaos"] = "Europe",
        ["Light"] = "Europe",
        ["Shadow"] = "Europe",
        ["Elemental"] = "Japan",
        ["Gaia"] = "Japan",
        ["Mana"] = "Japan",
        ["Meteor"] = "Japan",
        ["Materia"] = "Oceania",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<string> Order = new[]
    {
        "North America", "Europe", "Japan", "Oceania", Unknown,
    };

    public static string For(string dataCenterName) =>
        !string.IsNullOrEmpty(dataCenterName) && ByDataCenter.TryGetValue(dataCenterName, out var region)
            ? region
            : Unknown;

    public static int OrderOf(string region)
    {
        for (var index = 0; index < Order.Count; index++)
        {
            if (string.Equals(Order[index], region, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return Order.Count;
    }
}
