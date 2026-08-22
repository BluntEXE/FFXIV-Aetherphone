using Aetherphone.Core.Net;

namespace Aetherphone.Core.Housing;

internal sealed class HousingRestProvider
{
    private const int PhaseEntry = 1;
    private const int PhaseResults = 2;
    private const int PhaseUnavailable = 3;
    private const int FlagLottery = 1;
    private const int FlagFreeCompany = 2;
    private const int FlagIndividual = 4;

    private readonly HttpService http;
    private readonly RequestThrottle throttle;
    private readonly Func<string> baseUrl;

    public HousingRestProvider(HttpService http, HousingProviderKind kind, string displayName, Func<string> baseUrl)
    {
        this.http = http;
        this.baseUrl = baseUrl;
        Kind = kind;
        DisplayName = displayName;
        throttle = new RequestThrottle(2, TimeSpan.FromMilliseconds(250));
    }

    public HousingProviderKind Kind { get; }

    public string DisplayName { get; }

    public int LastStatusCode { get; private set; }

    public int? LastProxyCacheAge { get; private set; }

    public string BaseUrl => baseUrl();

    public async Task<IReadOnlyList<HousingWorld>?> GetWorldsAsync(CancellationToken token)
    {
        var worlds = new List<HousingWorld>();
        using (await throttle.EnterAsync(token).ConfigureAwait(false))
        {
            var status = 0;
            var payload = await http.GetJsonAsync(HousingEndpoints.Worlds(BaseUrl),
                HousingJsonContext.Default.PaissaWorldSummaryArray, null, token, code => status = code)
                .ConfigureAwait(false);
            LastStatusCode = status;
            if (payload is not null)
            {
                for (var index = 0; index < payload.Length; index++)
                {
                    var entry = payload[index];
                    if (entry.Id == 0 || string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    var dataCenter = entry.DataCenterName ?? string.Empty;
                    worlds.Add(new HousingWorld(entry.Id, entry.Name, entry.DataCenterId, dataCenter,
                        HousingRegions.For(dataCenter)));
                }
            }
        }

        CnHousing.AppendWorlds(worlds);
        return worlds.Count == 0 ? null : worlds;
    }

    public async Task<HousingDistrictSnapshot?> GetDistrictAsync(uint worldId, uint districtId,
        CancellationToken token)
    {
        if (worldId == 0 || districtId == 0)
        {
            return null;
        }

        if (CnHousing.IsChinaWorld(worldId))
        {
            LastProxyCacheAge = null;
            return await GetCnDistrictAsync(worldId, districtId, token).ConfigureAwait(false);
        }

        using (await throttle.EnterAsync(token).ConfigureAwait(false))
        {
            var status = 0;
            var payload = await http.GetJsonAsync(HousingEndpoints.World(BaseUrl, worldId),
                HousingJsonContext.Default.PaissaWorldDetail, null, token, code => status = code)
                .ConfigureAwait(false);
            LastStatusCode = status;
            if (payload is null)
            {
                return null;
            }

            var district = FindDistrict(payload, districtId);
            LastProxyCacheAge = district?.Proxy?.CacheAgeSeconds ?? payload.Proxy?.CacheAgeSeconds;
            return district is null ? null : Map(worldId, districtId, district, payload.Proxy);
        }
    }

    private async Task<HousingDistrictSnapshot?> GetCnDistrictAsync(uint worldId, uint districtId,
        CancellationToken token)
    {
        var area = CnHousing.AreaOf(districtId);
        if (area < 0)
        {
            LastStatusCode = 0;
            return null;
        }

        using (await throttle.EnterAsync(token).ConfigureAwait(false))
        {
            var status = 0;
            var payload = await http.GetJsonAsync(HousingEndpoints.CnSales(HousingEndpoints.CnBaseUrl, worldId),
                    HousingJsonContext.Default.CnSalesPlotArray, null, token, code => status = code)
                .ConfigureAwait(false);
            LastStatusCode = status;
            if (payload is null)
            {
                return null;
            }

            var plots = new List<HousingPlot>(payload.Length);
            for (var index = 0; index < payload.Length; index++)
            {
                var entry = payload[index];
                if (entry.Area != area)
                {
                    continue;
                }

                if (TryMapCnPlot(worldId, districtId, entry, out var plot))
                {
                    plots.Add(plot);
                }
            }

            plots.Sort(HousingPlotOrder.ByWardThenPlot);
            return new HousingDistrictSnapshot
            {
                WorldId = worldId,
                DistrictId = districtId,
                DistrictName = HousingDistricts.Name(districtId),
                FetchedUtc = DateTime.UtcNow,
                Source = Kind,
                Plots = plots,
            };
        }
    }

    private static PaissaDistrictDetail? FindDistrict(PaissaWorldDetail payload, uint districtId)
    {
        var districts = payload.Districts ?? Array.Empty<PaissaDistrictDetail>();
        for (var index = 0; index < districts.Length; index++)
        {
            if (districts[index].Id == districtId)
            {
                return districts[index];
            }
        }

        return null;
    }

    private HousingDistrictSnapshot Map(uint worldId, uint districtId, PaissaDistrictDetail payload,
        PaissaProxyInfo? worldProxy)
    {
        var raw = payload.OpenPlots ?? Array.Empty<PaissaOpenPlot>();
        var plots = new List<HousingPlot>(raw.Length);
        for (var index = 0; index < raw.Length; index++)
        {
            if (TryMapPlot(worldId, districtId, raw[index], out var plot))
            {
                plots.Add(plot);
            }
        }

        plots.Sort(HousingPlotOrder.ByWardThenPlot);

        var fetched = DateTime.UtcNow;
        if ((payload.Proxy?.CacheAgeSeconds ?? worldProxy?.CacheAgeSeconds) is { } age && age > 0)
        {
            fetched = fetched.AddSeconds(-age);
        }

        return new HousingDistrictSnapshot
        {
            WorldId = worldId,
            DistrictId = districtId,
            DistrictName = string.IsNullOrEmpty(payload.Name) ? HousingDistricts.Name(districtId) : payload.Name,
            FetchedUtc = fetched,
            Source = Kind,
            Plots = plots,
        };
    }

    private static bool TryMapPlot(uint worldId, uint districtId, PaissaOpenPlot entry, out HousingPlot plot)
    {
        plot = null!;
        var ward = NormalizeWard(entry.WardNumber);
        var plotNumber = NormalizePlot(entry.PlotNumber);
        if (ward <= 0 || plotNumber <= 0)
        {
            return false;
        }

        plot = new HousingPlot
        {
            Key = new HousingPlotKey(worldId, districtId, ward, plotNumber),
            Size = MapSize(entry.Size),
            Price = entry.Price > 0 ? entry.Price : 0L,
            Eligibility = MapEligibility(entry.PurchaseSystem),
            Mode = MapMode(entry.PurchaseSystem),
            Phase = MapPhase(entry.LottoPhase),
            PhaseEndsUtc = FromUnixSeconds(entry.LottoPhaseUntil),
            Entries = entry.LottoEntries,
            LastSeenUtc = FromUnixSeconds(entry.LastUpdatedTime) ?? default,
            FirstSeenUtc = FromUnixSeconds(entry.FirstSeenTime) ?? default,
        };
        return true;
    }

    private static bool TryMapCnPlot(uint worldId, uint districtId, CnSalesPlot entry, out HousingPlot plot)
    {
        plot = null!;
        var ward = entry.Slot + 1;
        var plotNumber = entry.Id;
        if (ward is < 1 or > HousingDistricts.DefaultWards ||
            plotNumber is < 1 or > HousingDistricts.PlotsPerWard)
        {
            return false;
        }

        var lastSeen = FromUnixSeconds((long?)entry.LastSeen) ?? DateTime.UtcNow;
        plot = new HousingPlot
        {
            Key = new HousingPlotKey(worldId, districtId, ward, plotNumber),
            Size = MapSize(entry.Size),
            Price = entry.Price > 0 ? entry.Price : 0L,
            Eligibility = HousingPurchaseEligibility.Unknown,
            Mode = HousingPurchaseMode.Unknown,
            Phase = HousingLotteryPhase.Unknown,
            PhaseEndsUtc = null,
            Entries = null,
            LastSeenUtc = lastSeen,
            FirstSeenUtc = FromUnixSeconds((long?)entry.FirstSeen) ?? lastSeen,
        };
        return true;
    }

    public static int NormalizeWard(int apiWardNumber) =>
        apiWardNumber is < 0 or >= HousingDistricts.DefaultWards ? 0 : apiWardNumber + 1;

    public static int NormalizePlot(int apiPlotNumber) =>
        apiPlotNumber is < 0 or >= HousingDistricts.PlotsPerWard ? 0 : apiPlotNumber + 1;

    public static HousingPlotSize MapSize(int apiSize) => apiSize switch
    {
        0 => HousingPlotSize.Small,
        1 => HousingPlotSize.Medium,
        2 => HousingPlotSize.Large,
        _ => HousingPlotSize.Unknown,
    };

    public static HousingLotteryPhase MapPhase(int? apiPhase) => apiPhase switch
    {
        PhaseEntry => HousingLotteryPhase.Entry,
        PhaseResults => HousingLotteryPhase.Results,
        PhaseUnavailable => HousingLotteryPhase.Unavailable,
        _ => HousingLotteryPhase.Unknown,
    };

    public static HousingPurchaseMode MapMode(int purchaseSystem)
    {
        if (purchaseSystem <= 0)
        {
            return HousingPurchaseMode.Unknown;
        }

        return (purchaseSystem & FlagLottery) != 0
            ? HousingPurchaseMode.Lottery
            : HousingPurchaseMode.FirstComeFirstServed;
    }

    public static HousingPurchaseEligibility MapEligibility(int purchaseSystem)
    {
        var freeCompany = (purchaseSystem & FlagFreeCompany) != 0;
        var individual = (purchaseSystem & FlagIndividual) != 0;
        if (freeCompany && individual)
        {
            return HousingPurchaseEligibility.Both;
        }

        if (freeCompany)
        {
            return HousingPurchaseEligibility.FreeCompany;
        }

        return individual ? HousingPurchaseEligibility.Private : HousingPurchaseEligibility.Unknown;
    }

    private static DateTime? FromUnixSeconds(double seconds) =>
        seconds <= 0d ? null : DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000d)).UtcDateTime;

    private static DateTime? FromUnixSeconds(long? seconds) =>
        seconds is null or <= 0L ? null : DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime;

    public void Dispose() => throttle.Dispose();
}

internal static class HousingPlotOrder
{
    public static readonly Comparison<HousingPlot> ByWardThenPlot = static (first, second) =>
    {
        var ward = first.Key.Ward.CompareTo(second.Key.Ward);
        return ward != 0 ? ward : first.Key.Plot.CompareTo(second.Key.Plot);
    };
}

internal static class CnHousing
{
    private static readonly (string DataCenter, (uint Id, string Name)[] Worlds)[] DataCenters =
    [
        ("陆行鸟",
        [
            (1167u, "红玉海"), (1081u, "神意之地"), (1042u, "拉诺西亚"), (1044u, "幻影群岛"),
            (1060u, "萌芽池"), (1173u, "宇宙和音"), (1174u, "沃仙曦染"), (1175u, "晨曦王座"),
        ]),
        ("莫古力",
        [
            (1172u, "白银乡"), (1076u, "白金幻象"), (1171u, "神拳痕"), (1170u, "潮风亭"),
            (1113u, "旅人栈桥"), (1121u, "拂晓之间"), (1166u, "龙巢神殿"), (1176u, "梦羽宝境"),
        ]),
        ("猫小胖",
        [
            (1043u, "紫水栈桥"), (1169u, "延夏"), (1106u, "静语庄园"), (1045u, "摩杜纳"),
            (1177u, "海猫茶屋"), (1178u, "柔风海湾"), (1179u, "琥珀原"),
        ]),
        ("豆豆柴",
        [
            (1192u, "水晶塔"), (1183u, "银泪湖"), (1180u, "太阳海岸"), (1186u, "伊修加德"),
            (1201u, "红茶川"),
        ]),
    ];

    private static readonly HashSet<uint> Ids = BuildIds();

    private static HashSet<uint> BuildIds()
    {
        var ids = new HashSet<uint>();
        foreach (var (_, worlds) in DataCenters)
        {
            foreach (var (id, _) in worlds)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    public static bool IsChinaWorld(uint worldId) => Ids.Contains(worldId);

    public static void AppendWorlds(List<HousingWorld> worlds)
    {
        for (var dcIndex = 0; dcIndex < DataCenters.Length; dcIndex++)
        {
            var (dataCenter, dcWorlds) = DataCenters[dcIndex];
            for (var index = 0; index < dcWorlds.Length; index++)
            {
                var (id, name) = dcWorlds[index];
                worlds.Add(new HousingWorld(id, name, dcIndex, dataCenter, HousingRegions.For(dataCenter)));
            }
        }
    }

    public static int AreaOf(uint districtId) => districtId switch
    {
        HousingDistricts.MistId => 0,
        HousingDistricts.LavenderBedsId => 1,
        HousingDistricts.GobletId => 2,
        HousingDistricts.ShiroganeId => 3,
        HousingDistricts.EmpyreumId => 4,
        _ => -1,
    };
}
