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
        ["陆行鸟"] = "China",
        ["莫古力"] = "China",
        ["猫小胖"] = "China",
        ["豆豆柴"] = "China",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<string> Order = new[]
    {
        "North America", "Europe", "Japan", "Oceania", "China", Unknown,
    };

    public static string For(string dataCenterName) =>
        !string.IsNullOrEmpty(dataCenterName) && ByDataCenter.TryGetValue(dataCenterName, out var region)
            ? region
            : Unknown;
}
