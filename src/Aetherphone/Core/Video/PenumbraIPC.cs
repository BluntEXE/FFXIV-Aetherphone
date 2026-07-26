using Penumbra.Api.IpcSubscribers;
using Penumbra.Api.Enums;

namespace Aetherphone.Core.Video;

// Ported from AlphaChannel's PenumbraIPC (Voudi, GPL-3.0). RedrawAll used to redraw every
// summoned Carbuncle-model companion in range; there is no companion anymore, so it redraws the
// local player instead - the only object the screen VFX is ever attached to. IsAvailable/LastError
// added (mirroring the old ScreenPenumbraGate this replaces) so the UI can surface a failed IPC
// call instead of failing silently.
internal static class PenumbraIPC
{
    private const string Tag = "AetherStreamTemporaryMod";
    private const string PluginInternalName = "Penumbra";

    private static Guid _collectionId = Guid.Empty;

    private static bool _isTempCollection;

    private static readonly List<string> _keys = [];

    public static bool IsAvailable { get; private set; } = true;
    public static string? LastError { get; private set; }

    public static bool IsInstalled()
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, PluginInternalName, StringComparison.Ordinal) && plugin.IsLoaded)
            {
                return true;
            }
        }

        return false;
    }

    public static bool CheckTempMod(string key)
    {
        return _collectionId != Guid.Empty && _keys.Contains(key);
    }

    public static void ApplyTempMod(string key, Dictionary<string, string> gamePaths)
    {
        if (_keys.Contains(key))
        {
            return;
        }

        try
        {
            if (_collectionId == Guid.Empty)
            {
                var localIndex = Plugin.ObjectTable.LocalPlayer?.ObjectIndex;
                if (localIndex is null)
                {
                    LastError = "No local player.";
                    return;
                }

                var getCollection = new GetCollectionForObject(Plugin.PluginInterface);
                var collectionResult = getCollection.Invoke(localIndex.Value);
                if (collectionResult.ObjectValid)
                {
                    _collectionId = collectionResult.EffectiveCollection.Id;
                    _isTempCollection = false;
                }
                else
                {
                    var createCollection = new CreateTemporaryCollection(Plugin.PluginInterface);
                    createCollection.Invoke(Tag, Tag, out _collectionId);

                    _isTempCollection = true;
                }
            }

            var addMod = new AddTemporaryMod(Plugin.PluginInterface);
            addMod.Invoke(Tag + key, _collectionId, gamePaths, string.Empty, int.MaxValue);

            if (_isTempCollection)
            {
                var assign = new AssignTemporaryCollection(Plugin.PluginInterface);
                assign.Invoke(_collectionId, Plugin.ObjectTable.LocalPlayer!.ObjectIndex, true);
            }

            _keys.Add(key);
            IsAvailable = true;
            LastError = null;

            AepLog.Debug("Assigned Temp Mod " + key + " to collection " + _collectionId);
        }
        catch (Exception exception)
        {
            IsAvailable = false;
            LastError = $"Penumbra IPC failed ({key}): {exception.Message}";
            AepLog.Warning($"[Video] {LastError}");
        }
    }

    public static void RemoveTempMod(string key)
    {
        if (_collectionId == Guid.Empty || !_keys.Contains(key))
        {
            return;
        }

        try
        {
            var assign = new RemoveTemporaryMod(Plugin.PluginInterface);
            assign.Invoke(Tag + key, _collectionId, int.MaxValue);
            _keys.Remove(key);
            AepLog.Debug("Removed Temp Mod " + key);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] Failed removing Penumbra temp mod {key}: {exception.Message}");
        }
    }

    public static void Redraw(ushort gameObjectIndex)
    {
        if (gameObjectIndex == ushort.MaxValue)
        {
            RedrawAll();
            return;
        }

        try
        {
            var redraw = new RedrawObject(Plugin.PluginInterface);
            redraw.Invoke(gameObjectIndex, RedrawType.Redraw);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] Redraw failed: {exception.Message}");
        }
    }

    public static void RedrawAll()
    {
        var localIndex = Plugin.ObjectTable.LocalPlayer?.ObjectIndex;
        if (localIndex is null)
        {
            return;
        }

        try
        {
            var redraw = new RedrawObject(Plugin.PluginInterface);
            redraw.Invoke(localIndex.Value, RedrawType.Redraw);
            AepLog.Debug("Redrawing local player " + localIndex.Value);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] RedrawAll failed: {exception.Message}");
        }
    }

    public static void Dispose()
    {
        if (_collectionId == Guid.Empty)
        {
            return;
        }

        foreach (string key in _keys.ToList())
        {
            RemoveTempMod(key);
        }

        if (_isTempCollection)
        {
            try
            {
                var removeCollection = new DeleteTemporaryCollection(Plugin.PluginInterface);
                removeCollection.Invoke(_collectionId);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Video] Failed deleting temp collection: {exception.Message}");
            }
        }

        _collectionId = Guid.Empty;
        _keys.Clear();
    }
}
