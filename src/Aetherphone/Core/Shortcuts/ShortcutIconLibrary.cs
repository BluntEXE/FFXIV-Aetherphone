using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Shortcuts;

internal sealed class ShortcutIconLibrary
{
    public const int IconSize = 256;

    private const string Extension = ".jpg";

    private readonly DirectoryInfo directory;
    private readonly Configuration configuration;
    private readonly WallpaperImageCache images;
    private readonly Dictionary<string, string> pathsById = new();

    public ShortcutIconLibrary(DirectoryInfo directory, Configuration configuration, WallpaperImageCache images)
    {
        this.directory = directory;
        this.configuration = configuration;
        this.images = images;
        directory.Create();

        var ids = configuration.CustomShortcutIconIds;
        for (var index = 0; index < ids.Count; index++)
        {
            pathsById[ids[index]] = Path.Combine(directory.FullName, ids[index] + Extension);
        }
    }

    public static byte[] Bake(string sourcePath, WallpaperCrop crop) =>
        ImageProcessor.BakeSquareJpeg(sourcePath, crop, IconSize).Bytes;

    public string Commit(byte[] bakedBytes)
    {
        var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(directory.FullName, id + Extension);
        File.WriteAllBytes(path, bakedBytes);
        configuration.CustomShortcutIconIds.Add(id);
        configuration.Save();
        pathsById[id] = path;
        return id;
    }

    public string? Duplicate(string id)
    {
        if (!pathsById.TryGetValue(id, out var sourcePath))
        {
            return null;
        }

        var newId = Guid.NewGuid().ToString("N");
        var newPath = Path.Combine(directory.FullName, newId + Extension);
        File.Copy(sourcePath, newPath, true);
        configuration.CustomShortcutIconIds.Add(newId);
        configuration.Save();
        pathsById[newId] = newPath;
        return newId;
    }

    public void Remove(string id)
    {
        if (!pathsById.TryGetValue(id, out var path))
        {
            return;
        }

        configuration.CustomShortcutIconIds.Remove(id);
        configuration.Save();
        pathsById.Remove(id);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Shortcuts] failed to delete custom icon {id}");
        }
    }

    public IDalamudTextureWrap? Icon(string id)
    {
        if (id.Length == 0)
        {
            return null;
        }

        return pathsById.TryGetValue(id, out var path) ? images.Get(path) : null;
    }
}
