using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace Aetherphone.Core.Video;

// Ported from AlphaChannel's PenumbraIPC (Voudi, GPL-3.0), adapted to an instance rather than a
// static class, and with explicit availability/error surfacing - spec requires the UI say so
// when Penumbra or the screen mod isn't there, rather than failing silently.
internal sealed class ScreenPenumbraGate : IDisposable
{
    private const string Tag = "AetherStreamTemporaryMod";
    private const string PluginInternalName = "Penumbra";

    private Guid collectionId = Guid.Empty;
    private bool isTempCollection;
    private readonly List<string> appliedKeys = new();

    public bool IsAvailable { get; private set; } = true;
    public string? LastError { get; private set; }

    // A real scan of installed plugins (same pattern as LifestreamBridge.IsAvailable), not an
    // inference from IPC failures - IsAvailable above only ever flips once a mod-apply is
    // attempted and throws, so it says nothing until playback is already underway. This is what
    // the UI should check to state the requirement up front, before anything has failed.
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

    public bool HasApplied(string key) => collectionId != Guid.Empty && appliedKeys.Contains(key);

    public bool ApplyTempMod(string key, Dictionary<string, string> gamePaths)
    {
        if (HasApplied(key))
        {
            return true;
        }

        try
        {
            if (collectionId == Guid.Empty)
            {
                var localIndex = Plugin.ObjectTable.LocalPlayer?.ObjectIndex;
                if (localIndex is null)
                {
                    LastError = "No local player.";
                    return false;
                }

                var getCollection = new GetCollectionForObject(Plugin.PluginInterface);
                var collectionResult = getCollection.Invoke(localIndex.Value);
                if (collectionResult.ObjectValid)
                {
                    collectionId = collectionResult.EffectiveCollection.Id;
                    isTempCollection = false;
                }
                else
                {
                    var createCollection = new CreateTemporaryCollection(Plugin.PluginInterface);
                    createCollection.Invoke(Tag, Tag, out collectionId);
                    isTempCollection = true;
                }
            }

            var addMod = new AddTemporaryMod(Plugin.PluginInterface);
            addMod.Invoke(Tag + key, collectionId, gamePaths, string.Empty, int.MaxValue);

            if (isTempCollection)
            {
                var localIndex = Plugin.ObjectTable.LocalPlayer!.ObjectIndex;
                var assign = new AssignTemporaryCollection(Plugin.PluginInterface);
                assign.Invoke(collectionId, localIndex, true);
            }

            appliedKeys.Add(key);
            IsAvailable = true;
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            IsAvailable = false;
            LastError = $"Penumbra IPC failed ({key}): {exception.Message}";
            AepLog.Warning($"[Video] {LastError}");
            return false;
        }
    }

    public void RemoveTempMod(string key)
    {
        if (collectionId == Guid.Empty || !appliedKeys.Contains(key))
        {
            return;
        }

        try
        {
            var remove = new RemoveTemporaryMod(Plugin.PluginInterface);
            remove.Invoke(Tag + key, collectionId, int.MaxValue);
            appliedKeys.Remove(key);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] Failed removing Penumbra temp mod {key}: {exception.Message}");
        }
    }

    public void Redraw(ushort gameObjectIndex)
    {
        if (gameObjectIndex == ushort.MaxValue)
        {
            RedrawAll();
            return;
        }

        try
        {
            new RedrawObject(Plugin.PluginInterface).Invoke(gameObjectIndex, RedrawType.Redraw);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] Redraw failed: {exception.Message}");
        }
    }

    public void RedrawAll()
    {
        try
        {
            var redraw = new RedrawObject(Plugin.PluginInterface);
            foreach (var index in Plugin.ObjectTable
                         .Where(o => o is Dalamud.Game.ClientState.Objects.Types.IBattleNpc &&
                                     o.BaseId == VideoRenderTarget.CompanionBaseId &&
                                     o.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)
                         .Select(o => o.ObjectIndex))
            {
                redraw.Invoke(index, RedrawType.Redraw);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] RedrawAll failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (collectionId == Guid.Empty)
        {
            return;
        }

        foreach (var key in appliedKeys.ToList())
        {
            RemoveTempMod(key);
        }

        if (isTempCollection)
        {
            try
            {
                new DeleteTemporaryCollection(Plugin.PluginInterface).Invoke(collectionId);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Video] Failed deleting temp collection: {exception.Message}");
            }
        }

        collectionId = Guid.Empty;
        appliedKeys.Clear();
    }
}
