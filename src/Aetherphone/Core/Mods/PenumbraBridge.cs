using Dalamud.Plugin.Ipc;

using ModSettingsResult = (int Code, (bool Enabled, int Priority, System.Collections.Generic.Dictionary<string,
    System.Collections.Generic.List<string>> Options, bool Inherited)? Settings);

namespace Aetherphone.Core.Mods;

internal static class PenumbraBridge
{
    private const string InternalName = "Penumbra";
    private const byte PlayerCollection = 0;
    private const int ModsTab = 1;
    private const int Success = 0;

    private static ICallGateSubscriber<(int Breaking, int Features)>? apiVersionGate;
    private static ICallGateSubscriber<string>? modDirectoryGate;
    private static ICallGateSubscriber<Dictionary<string, string>>? modListGate;
    private static ICallGateSubscriber<byte, (Guid Id, string Name)?>? collectionGate;
    private static ICallGateSubscriber<Guid, string, string, bool, ModSettingsResult>? modSettingsGate;
    private static ICallGateSubscriber<Guid, string, string, bool, int>? setModGate;
    private static ICallGateSubscriber<int, string, string, int>? openWindowGate;

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

    public static bool TryGetApiVersion(out int breaking, out int features)
    {
        breaking = 0;
        features = 0;
        if (!IsAvailable())
        {
            return false;
        }

        try
        {
            apiVersionGate ??= Plugin.PluginInterface.GetIpcSubscriber<(int Breaking, int Features)>(
                $"{InternalName}.ApiVersion.V5");
            (breaking, features) = apiVersionGate.InvokeFunc();
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Penumbra] ApiVersion failed");
            return false;
        }
    }

    public static bool TryGetModDirectory(out string directory)
    {
        directory = string.Empty;
        if (!IsAvailable())
        {
            return false;
        }

        try
        {
            modDirectoryGate ??= Plugin.PluginInterface.GetIpcSubscriber<string>($"{InternalName}.GetModDirectory");
            directory = modDirectoryGate.InvokeFunc() ?? string.Empty;
            return directory.Length > 0;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Penumbra] GetModDirectory failed");
            return false;
        }
    }

    public static Dictionary<string, string>? GetModList()
    {
        if (!IsAvailable())
        {
            return null;
        }

        try
        {
            modListGate ??= Plugin.PluginInterface.GetIpcSubscriber<Dictionary<string, string>>(
                $"{InternalName}.GetModList");
            return modListGate.InvokeFunc();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Penumbra] GetModList failed");
            return null;
        }
    }

    public static bool TryGetPlayerCollection(out Guid id, out string name)
    {
        id = Guid.Empty;
        name = string.Empty;
        if (!IsAvailable())
        {
            return false;
        }

        try
        {
            collectionGate ??= Plugin.PluginInterface.GetIpcSubscriber<byte, (Guid Id, string Name)?>(
                $"{InternalName}.GetCollection");
            var collection = collectionGate.InvokeFunc(PlayerCollection);
            if (collection is null)
            {
                return false;
            }

            id = collection.Value.Id;
            name = collection.Value.Name;
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Penumbra] GetCollection failed");
            return false;
        }
    }

    public static bool? IsModEnabled(Guid collectionId, string modDirectory)
    {
        try
        {
            modSettingsGate ??= Plugin.PluginInterface
                .GetIpcSubscriber<Guid, string, string, bool, ModSettingsResult>(
                    $"{InternalName}.GetCurrentModSettings.V5");
            var result = modSettingsGate.InvokeFunc(collectionId, modDirectory, string.Empty, false);
            if (result.Code != Success || result.Settings is null)
            {
                return null;
            }

            return result.Settings.Value.Enabled;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Penumbra] GetCurrentModSettings failed");
            return null;
        }
    }

    public static bool SetModEnabled(Guid collectionId, string modDirectory, bool enabled)
    {
        try
        {
            setModGate ??= Plugin.PluginInterface.GetIpcSubscriber<Guid, string, string, bool, int>(
                $"{InternalName}.TrySetMod.V5");
            var code = setModGate.InvokeFunc(collectionId, modDirectory, string.Empty, enabled);
            return code == Success || code == 1;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Penumbra] TrySetMod failed");
            return false;
        }
    }

    public static bool OpenMod(string modDirectory)
    {
        if (!IsAvailable())
        {
            return false;
        }

        try
        {
            openWindowGate ??= Plugin.PluginInterface.GetIpcSubscriber<int, string, string, int>(
                $"{InternalName}.OpenMainWindow.V5");
            return openWindowGate.InvokeFunc(ModsTab, modDirectory, string.Empty) == Success;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Penumbra] OpenMainWindow failed");
            return false;
        }
    }

    public static PenumbraModEvents SubscribeModChanges(Action onChanged) => new(InternalName, onChanged);
}

internal sealed class PenumbraModEvents : IDisposable
{
    private readonly ICallGateSubscriber<string, object>? added;
    private readonly ICallGateSubscriber<string, object>? deleted;
    private readonly Action<string> handler;

    public PenumbraModEvents(string internalName, Action onChanged)
    {
        handler = _ => onChanged();
        try
        {
            added = Plugin.PluginInterface.GetIpcSubscriber<string, object>($"{internalName}.ModAdded");
            deleted = Plugin.PluginInterface.GetIpcSubscriber<string, object>($"{internalName}.ModDeleted");
            added.Subscribe(handler);
            deleted.Subscribe(handler);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Penumbra] mod event subscription failed");
        }
    }

    public void Dispose()
    {
        try
        {
            added?.Unsubscribe(handler);
            deleted?.Unsubscribe(handler);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Penumbra] mod event unsubscribe failed");
        }
    }
}
