namespace Aetherphone.Core.Housing;

internal readonly record struct HousingDistrict(uint Id, string Name, string ShortName, int Wards);

internal static class HousingDistricts
{
    public const int PlotsPerDivision = 30;
    public const int PlotsPerWard = PlotsPerDivision * 2;
    public const int DefaultWards = 30;

    public static readonly HousingDistrict Mist = new(339u, "Mist", "Mist", DefaultWards);
    public static readonly HousingDistrict LavenderBeds = new(340u, "The Lavender Beds", "Lavender", DefaultWards);
    public static readonly HousingDistrict Goblet = new(341u, "The Goblet", "Goblet", DefaultWards);
    public static readonly HousingDistrict Shirogane = new(641u, "Shirogane", "Shirogane", DefaultWards);
    public static readonly HousingDistrict Empyreum = new(979u, "Empyreum", "Empyreum", DefaultWards);

    public static readonly IReadOnlyList<HousingDistrict> All = new[]
    {
        Mist, LavenderBeds, Goblet, Shirogane, Empyreum,
    };

    public static uint DefaultId => Mist.Id;

    public static bool TryGet(uint districtId, out HousingDistrict district)
    {
        for (var index = 0; index < All.Count; index++)
        {
            if (All[index].Id == districtId)
            {
                district = All[index];
                return true;
            }
        }

        district = default;
        return false;
    }

    public static HousingDistrict Resolve(uint districtId) => TryGet(districtId, out var district) ? district : Mist;

    public static string Name(uint districtId) => TryGet(districtId, out var district) ? district.Name : string.Empty;

    public static int ClampWard(uint districtId, int ward)
    {
        var wards = Resolve(districtId).Wards;
        return Math.Clamp(ward, 1, wards);
    }

    public static bool IsSubdivision(int plotNumber) => plotNumber > PlotsPerDivision;
}
