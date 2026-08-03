namespace Aetherphone.Core.Housing;

internal interface IHousingDataProvider
{
    HousingProviderKind Kind { get; }

    string DisplayName { get; }
    Task<IReadOnlyList<HousingWorld>?> GetWorldsAsync(CancellationToken token);
    Task<HousingDistrictSnapshot?> GetDistrictAsync(uint worldId, uint districtId, CancellationToken token);
}
