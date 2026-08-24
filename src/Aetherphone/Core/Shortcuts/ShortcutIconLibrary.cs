using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Shortcuts;

internal sealed class ShortcutIconLibrary
{
    public const int IconSize = 256;

    private readonly DirectoryInfo directory;
    private readonly Configuration configuration;
    private readonly WallpaperImageCache images;

    public ShortcutIconLibrary(DirectoryInfo directory, Configuration configuration, WallpaperImageCache images)
    {
        this.directory = directory;
        this.configuration = configuration;
        this.images = images;
        directory.Create();
    }

    public static byte[] Bake(string sourcePath, WallpaperCrop crop) =>
        ImageProcessor.BakeSquareJpeg(sourcePath, crop, IconSize).Bytes;

    public string Commit(byte[] bakedBytes)
    {
        var id = Guid.NewGuid().ToString("N");
        var fileName = id + ".jpg";
        File.WriteAllBytes(Path.Combine(directory.FullName, fileName), bakedBytes);
        configuration.CustomShortcutIcons.Add(new CustomShortcutIcon { Id = id, FileName = fileName });
        configuration.Save();
        return id;
    }

    public string? Duplicate(string id)
    {
        var record = Find(id);
        if (record is null)
        {
            return null;
        }

        var newId = Guid.NewGuid().ToString("N");
        var newFileName = newId + Path.GetExtension(record.FileName);
        File.Copy(Path.Combine(directory.FullName, record.FileName), Path.Combine(directory.FullName, newFileName),
            true);
        configuration.CustomShortcutIcons.Add(new CustomShortcutIcon { Id = newId, FileName = newFileName });
        configuration.Save();
        return newId;
    }

    public void Remove(string id)
    {
        var record = Find(id);
        if (record is null)
        {
            return;
        }

        configuration.CustomShortcutIcons.Remove(record);
        configuration.Save();
        var path = Path.Combine(directory.FullName, record.FileName);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Shortcuts] failed to delete custom icon {record.FileName}");
        }
    }

    public IDalamudTextureWrap? Icon(string id)
    {
        if (id.Length == 0)
        {
            return null;
        }

        var record = Find(id);
        return record is null ? null : images.Get(Path.Combine(directory.FullName, record.FileName));
    }

    private CustomShortcutIcon? Find(string id)
    {
        var customs = configuration.CustomShortcutIcons;
        for (var index = 0; index < customs.Count; index++)
        {
            if (customs[index].Id == id)
            {
                return customs[index];
            }
        }

        return null;
    }
}
