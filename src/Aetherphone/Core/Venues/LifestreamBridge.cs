using System.Globalization;
using Dalamud.Plugin.Ipc;

using HousingAddress = (string Name, int World, int City, int Ward, int PropertyType, int Plot, int Apartment,
    bool ApartmentSubdivision, bool AliasEnabled, string Alias);

namespace Aetherphone.Core.Venues;

internal enum LifestreamOutcome
{
    Started,
    NotInstalled,
    Busy,
    NotAttuned,
    CannotTeleportNow,
    WorldUnreachable,
}

internal static class LifestreamBridge
{
    private const string InternalName = "Lifestream";
    private const int HouseProperty = 0;

    private static ICallGateSubscriber<bool>? busyGate;
    private static ICallGateSubscriber<uint, byte, bool>? teleportGate;
    private static ICallGateSubscriber<uint, bool>? changeWorldGate;
    private static ICallGateSubscriber<string, bool>? sameDataCenterGate;
    private static ICallGateSubscriber<string, bool>? crossDataCenterGate;
    private static ICallGateSubscriber<HousingAddress, object>? housingAddressGate;

    public static bool IsAvailable()
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, InternalName, StringComparison.Ordinal) && plugin.IsLoaded)
            {
                return true;
            }
        }

        return false;
    }

    public static LifestreamOutcome TeleportToAetheryte(uint aetheryteRowId)
    {
        if (!IsAvailable())
        {
            return LifestreamOutcome.NotInstalled;
        }

        if (IsBusy())
        {
            return LifestreamOutcome.Busy;
        }

        if (!IsAttuned(aetheryteRowId))
        {
            return LifestreamOutcome.NotAttuned;
        }

        return Teleport(aetheryteRowId) ? LifestreamOutcome.Started : LifestreamOutcome.CannotTeleportNow;
    }

    public static LifestreamOutcome TravelToAethernet(uint masterRowId, string shardName)
    {
        if (!IsAvailable())
        {
            return LifestreamOutcome.NotInstalled;
        }

        if (IsBusy())
        {
            return LifestreamOutcome.Busy;
        }

        if (!IsAttuned(masterRowId))
        {
            return LifestreamOutcome.NotAttuned;
        }

        Plugin.CommandManager.ProcessCommand(TravelCommand(shardName));
        return LifestreamOutcome.Started;
    }

    public static LifestreamOutcome TravelToWorld(uint worldId, string worldName)
    {
        if (!IsAvailable())
        {
            return LifestreamOutcome.NotInstalled;
        }

        if (IsBusy())
        {
            return LifestreamOutcome.Busy;
        }

        if (!CanVisit(worldName))
        {
            return LifestreamOutcome.WorldUnreachable;
        }

        return ChangeWorld(worldId) ? LifestreamOutcome.Started : LifestreamOutcome.WorldUnreachable;
    }

    public static LifestreamOutcome TravelToHousingPlot(uint worldId, uint cityAetheryteRowId, int ward, int plot)
    {
        if (!IsAvailable())
        {
            return LifestreamOutcome.NotInstalled;
        }

        if (IsBusy())
        {
            return LifestreamOutcome.Busy;
        }

        if (!IsAttuned(cityAetheryteRowId))
        {
            return LifestreamOutcome.NotAttuned;
        }

        return GoToHousingAddress(worldId, cityAetheryteRowId, ward, plot)
            ? LifestreamOutcome.Started
            : LifestreamOutcome.CannotTeleportNow;
    }

    public static void Travel(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        Plugin.CommandManager.ProcessCommand(TravelCommand(code));
    }

    public static string TravelCommand(string destination) => $"/li {destination}";

    public static string AetheryteCommand(string aetheryteName) => $"/li tp {aetheryteName}";

    public static string HousingCommand(string worldName, string districtName, int ward, int plot) =>
        string.Create(CultureInfo.InvariantCulture, $"/li {worldName}, {districtName}, W{ward}, P{plot}");

    private static bool GoToHousingAddress(uint worldId, uint cityAetheryteRowId, int ward, int plot)
    {
        try
        {
            housingAddressGate ??= Plugin.PluginInterface.GetIpcSubscriber<HousingAddress, object>(
                $"{InternalName}.GoToHousingAddress");
            housingAddressGate.InvokeAction((string.Empty, (int)worldId, (int)cityAetheryteRowId, ward, HouseProperty,
                plot, 1, false, false, string.Empty));
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Lifestream] GoToHousingAddress failed");
            return false;
        }
    }

    private static bool IsBusy()
    {
        try
        {
            busyGate ??= Plugin.PluginInterface.GetIpcSubscriber<bool>($"{InternalName}.IsBusy");
            return busyGate.InvokeFunc();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Lifestream] IsBusy failed");
            return false;
        }
    }

    private static bool Teleport(uint aetheryteRowId)
    {
        try
        {
            teleportGate ??= Plugin.PluginInterface.GetIpcSubscriber<uint, byte, bool>($"{InternalName}.Teleport");
            return teleportGate.InvokeFunc(aetheryteRowId, 0);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Lifestream] Teleport failed");
            return false;
        }
    }

    private static bool ChangeWorld(uint worldId)
    {
        try
        {
            changeWorldGate ??= Plugin.PluginInterface.GetIpcSubscriber<uint, bool>($"{InternalName}.ChangeWorldById");
            return changeWorldGate.InvokeFunc(worldId);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Lifestream] ChangeWorldById failed");
            return false;
        }
    }

    private static bool CanVisit(string worldName)
    {
        try
        {
            sameDataCenterGate ??= Plugin.PluginInterface.GetIpcSubscriber<string, bool>(
                $"{InternalName}.CanVisitSameDC");
            crossDataCenterGate ??= Plugin.PluginInterface.GetIpcSubscriber<string, bool>(
                $"{InternalName}.CanVisitCrossDC");
            return sameDataCenterGate.InvokeFunc(worldName) || crossDataCenterGate.InvokeFunc(worldName);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Lifestream] world reachability check failed");
            return true;
        }
    }

    private static bool IsAttuned(uint aetheryteRowId)
    {
        if (aetheryteRowId == 0)
        {
            return false;
        }

        var attuned = Plugin.AetheryteList;
        for (var index = 0; index < attuned.Length; index++)
        {
            if (attuned[index] is { SubIndex: 0 } entry && entry.AetheryteId == aetheryteRowId)
            {
                return true;
            }
        }

        return false;
    }
}
