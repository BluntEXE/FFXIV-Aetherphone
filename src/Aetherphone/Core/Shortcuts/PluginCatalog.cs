using Aetherphone.Core.Media;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;

namespace Aetherphone.Core.Shortcuts;

internal readonly record struct PluginCommand(string Command, string Help);

internal sealed class PluginEntry
{
    public required string InternalName { get; init; }
    public required string Name { get; init; }
    public required string Author { get; init; }
    public required string Punchline { get; init; }
    public required string IconUrl { get; init; }
    public required bool HasMainUi { get; init; }
    public required bool HasConfigUi { get; init; }
    public required bool Loaded { get; init; }
    public List<PluginCommand> Commands { get; } = new();
}

internal sealed class PluginCatalog
{
    private const string OwnInternalName = "Aetherphone";

    private readonly RemoteImageCache images;
    private readonly List<PluginEntry> entries = new();
    private readonly Dictionary<string, PluginEntry> byName = new(StringComparer.OrdinalIgnoreCase);
    private const long RecheckIntervalMilliseconds = 1000;

    private int lastPluginCount = -1;
    private int lastCommandCount = -1;
    private long nextRecheckTick;
    private bool dirty = true;

    public PluginCatalog(RemoteImageCache images)
    {
        this.images = images;
    }

    public IReadOnlyList<PluginEntry> Entries
    {
        get
        {
            EnsureFresh();
            return entries;
        }
    }

    public void Invalidate() => dirty = true;

    public PluginEntry? Find(string internalName)
    {
        EnsureFresh();
        return byName.TryGetValue(internalName, out var entry) ? entry : null;
    }

    public bool IsLoaded(string internalName) => Find(internalName)?.Loaded ?? false;

    public string DisplayName(string internalName) => Find(internalName)?.Name ?? internalName;

    public IDalamudTextureWrap? Icon(string internalName)
    {
        var entry = Find(internalName);
        return entry is null || entry.IconUrl.Length == 0 ? null : images.Get(entry.IconUrl);
    }

    public static bool TryOpenMainUi(string internalName)
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (!string.Equals(plugin.InternalName, internalName, StringComparison.Ordinal) || !plugin.IsLoaded)
            {
                continue;
            }

            try
            {
                if (plugin.HasMainUi)
                {
                    plugin.OpenMainUi();
                    return true;
                }

                if (plugin.HasConfigUi)
                {
                    plugin.OpenConfigUi();
                    return true;
                }
            }
            catch (Exception ex)
            {
                AepLog.Warning($"Opening {internalName} failed: {ex.Message}");
            }

            return false;
        }

        return false;
    }

    public static bool TryOpenConfigUi(string internalName)
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (!string.Equals(plugin.InternalName, internalName, StringComparison.Ordinal) || !plugin.IsLoaded ||
                !plugin.HasConfigUi)
            {
                continue;
            }

            try
            {
                plugin.OpenConfigUi();
                return true;
            }
            catch (Exception ex)
            {
                AepLog.Warning($"Opening the settings of {internalName} failed: {ex.Message}");
                return false;
            }
        }

        return false;
    }

    private void EnsureFresh()
    {
        var now = Environment.TickCount64;
        if (!dirty && now < nextRecheckTick)
        {
            return;
        }

        nextRecheckTick = now + RecheckIntervalMilliseconds;
        var pluginCount = CountPlugins();
        var commandCount = Plugin.CommandManager.Commands.Count;
        if (!dirty && pluginCount == lastPluginCount && commandCount == lastCommandCount)
        {
            return;
        }

        dirty = false;
        lastPluginCount = pluginCount;
        lastCommandCount = commandCount;
        Rebuild();
    }

    private static int CountPlugins()
    {
        var installed = 0;
        var loaded = 0;
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            installed++;
            if (plugin.IsLoaded)
            {
                loaded++;
            }
        }

        return installed * 1024 + loaded;
    }

    private void Rebuild()
    {
        entries.Clear();
        byName.Clear();
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, OwnInternalName, StringComparison.Ordinal) ||
                byName.ContainsKey(plugin.InternalName))
            {
                continue;
            }

            var entry = Describe(plugin);
            entries.Add(entry);
            byName[entry.InternalName] = entry;
        }

        AttachCommands();
        entries.Sort(CompareByName);
    }

    private static PluginEntry Describe(IExposedPlugin plugin)
    {
        var author = string.Empty;
        var punchline = string.Empty;
        var iconUrl = string.Empty;
        try
        {
            var manifest = plugin.Manifest;
            author = manifest.Author ?? string.Empty;
            punchline = manifest.Punchline ?? string.Empty;
            iconUrl = NormalizeIconUrl(manifest.IconUrl);
        }
        catch (Exception)
        {
            author = string.Empty;
        }

        return new PluginEntry
        {
            InternalName = plugin.InternalName,
            Name = plugin.Name.Length > 0 ? plugin.Name : plugin.InternalName,
            Author = author,
            Punchline = punchline,
            IconUrl = iconUrl,
            HasMainUi = plugin.HasMainUi,
            HasConfigUi = plugin.HasConfigUi,
            Loaded = plugin.IsLoaded,
        };
    }

    private static string NormalizeIconUrl(string? url)
    {
        if (url is null || url.Length == 0 || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return url;
    }

    private void AttachCommands()
    {
        var commands = Plugin.CommandManager.Commands;
        foreach (var pair in commands)
        {
            if (!pair.Value.ShowInHelp)
            {
                continue;
            }

            var owner = OwnerAssembly(pair.Value);
            if (owner.Length == 0 || ResolveOwner(owner) is not { } entry)
            {
                continue;
            }

            entry.Commands.Add(new PluginCommand(pair.Key, pair.Value.HelpMessage ?? string.Empty));
        }

        for (var index = 0; index < entries.Count; index++)
        {
            entries[index].Commands.Sort(CompareCommands);
        }
    }

    private PluginEntry? ResolveOwner(string assemblyName)
    {
        if (byName.TryGetValue(assemblyName, out var byInternalName))
        {
            return byInternalName;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return entries[index];
            }
        }

        return null;
    }

    private static string OwnerAssembly(Dalamud.Game.Command.IReadOnlyCommandInfo info)
    {
        try
        {
            var handler = info.Handler;
            var declaring = handler.Target?.GetType() ?? handler.Method.DeclaringType;
            return declaring?.Assembly.GetName().Name ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static int CompareByName(PluginEntry first, PluginEntry second) =>
        string.Compare(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);

    private static int CompareCommands(PluginCommand first, PluginCommand second) =>
        string.Compare(first.Command, second.Command, StringComparison.OrdinalIgnoreCase);
}
