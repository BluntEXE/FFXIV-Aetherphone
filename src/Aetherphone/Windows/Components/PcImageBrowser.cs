using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal sealed class PcImageBrowser : IDisposable
{
    private const int Columns = 3;

    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    private static string? rememberedDirectory;

    private readonly LocalThumbnailCache thumbnails = new();

    private string? currentDirectory;
    private string[] folderPaths = Array.Empty<string>();
    private string[] filePaths = Array.Empty<string>();
    private bool listingFailed;
    private bool scrollTopPending;

    public void Open()
    {
        var start = rememberedDirectory;
        if (string.IsNullOrEmpty(start) || !Directory.Exists(start))
        {
            start = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }

        if (string.IsNullOrEmpty(start) || !Directory.Exists(start))
        {
            NavigateToDrives();
            return;
        }

        NavigateTo(start);
    }

    public string? Draw(Rect area, AppSkin ui, PhoneTheme theme, Vector4 bodyInk, Vector4 mutedInk,
        Vector4 fieldSurface)
    {
        var scale = UiScale.Current;
        var navigationHeight = 40f * scale;
        DrawNavigationRow(area, ui, bodyInk, mutedInk, navigationHeight, scale);
        var gridRect = new Rect(new Vector2(area.Min.X, area.Min.Y + navigationHeight), area.Max);
        string? picked = null;
        using (var surface = AppSurface.Begin(gridRect))
        {
            if (scrollTopPending)
            {
                surface.JumpToTop();
                scrollTopPending = false;
            }

            if (listingFailed || (folderPaths.Length == 0 && filePaths.Length == 0))
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(listingFailed ? L.Common.ImageFailed : L.Common.NoPhotos), mutedInk);
                return null;
            }

            var gap = 6f * scale;
            var available = ScrollLayout.StableContentWidth();
            var cell = (available - gap * (Columns - 1)) / Columns;
            var origin = ImGui.GetCursorScreenPos();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var cullMargin = cell + 60f * scale;
            var totalEntries = folderPaths.Length + filePaths.Length;
            for (var entryIndex = 0; entryIndex < totalEntries; entryIndex++)
            {
                var column = entryIndex % Columns;
                var rowIndex = entryIndex / Columns;
                var rowTop = rowIndex * (cell + gap);
                if (rowTop + cell < scrollY - cullMargin || rowTop > scrollY + viewHeight + cullMargin)
                {
                    continue;
                }

                var min = new Vector2(origin.X + column * (cell + gap), origin.Y + rowTop);
                var max = new Vector2(min.X + cell, min.Y + cell);
                var hovered = UiInteract.Hover(min, max);
                if (entryIndex < folderPaths.Length)
                {
                    DrawFolderTile(folderPaths[entryIndex], min, max, scale, hovered, bodyInk, fieldSurface);
                    if (UiInteract.Click(min, max, hovered))
                    {
                        NavigateTo(folderPaths[entryIndex]);
                        return null;
                    }
                }
                else
                {
                    var path = filePaths[entryIndex - folderPaths.Length];
                    DrawImageTile(path, min, max, scale, hovered, theme);
                    if (UiInteract.Click(min, max, hovered))
                    {
                        picked = path;
                    }
                }
            }

            var rows = (totalEntries + Columns - 1) / Columns;
            var totalHeight = rows * (cell + gap);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(available, totalHeight));
        }

        return picked;
    }

    private void DrawNavigationRow(Rect area, AppSkin ui, Vector4 bodyInk, Vector4 mutedInk, float height,
        float scale)
    {
        var centerY = area.Min.Y + height * 0.5f;
        var upCenter = new Vector2(area.Min.X + 16f * scale + 15f * scale, centerY);
        var atDrives = currentDirectory is null;
        if (ui.IconButton(upCenter, 15f * scale, FontAwesomeIcon.LevelUpAlt.ToIconString(),
                atDrives ? Palette.WithAlpha(mutedInk, 0.4f) : bodyInk, new Vector4(0f, 0f, 0f, 0f), 1f,
                Loc.T(L.Common.Back)) && !atDrives)
        {
            NavigateUp();
        }

        var label = currentDirectory is null
            ? Environment.MachineName
            : DisplayNameFor(currentDirectory);
        var labelLeft = upCenter.X + 24f * scale;
        var labelMaxWidth = MathF.Max(1f, area.Max.X - 16f * scale - labelLeft);
        label = Typography.FitText(label, labelMaxWidth, 0.95f, FontWeight.SemiBold);
        var labelSize = Typography.Measure(label, 0.95f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(labelLeft, centerY - labelSize.Y * 0.5f), label, bodyInk, 0.95f,
            FontWeight.SemiBold);
    }

    private static void DrawFolderTile(string path, Vector2 min, Vector2 max, float scale, bool hovered,
        Vector4 ink, Vector4 fieldSurface)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(hovered ? Palette.WithAlpha(fieldSurface, MathF.Min(1f, fieldSurface.W + 0.08f)) : fieldSurface));
        var center = (min + max) * 0.5f;
        AppSkin.Icon(new Vector2(center.X, center.Y - 8f * scale), FontAwesomeIcon.Folder.ToIconString(),
            Palette.WithAlpha(ink, 0.8f), 1.5f);
        var name = DisplayNameFor(path);
        var maxNameWidth = max.X - min.X - 12f * scale;
        name = Typography.FitText(name, maxNameWidth, 0.75f, FontWeight.Medium);
        var nameSize = Typography.Measure(name, 0.75f, FontWeight.Medium);
        Typography.Draw(new Vector2(center.X - nameSize.X * 0.5f, max.Y - nameSize.Y - 8f * scale), name, ink,
            0.75f, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawImageTile(string path, Vector2 min, Vector2 max, float scale, bool hovered, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = thumbnails.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding,
                ImGui.GetColorU32(thumbnails.Failed(path) ? Palette.WithAlpha(theme.SurfaceMuted, 0.5f) : theme.SurfaceMuted));
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private static string DisplayNameFor(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return name.Length > 0 ? name : path;
    }

    private void NavigateUp()
    {
        if (currentDirectory is null)
        {
            return;
        }

        var parent = Path.GetDirectoryName(
            currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(parent))
        {
            NavigateToDrives();
            return;
        }

        NavigateTo(parent);
    }

    private void NavigateToDrives()
    {
        currentDirectory = null;
        listingFailed = false;
        scrollTopPending = true;
        filePaths = Array.Empty<string>();
        try
        {
            var drives = DriveInfo.GetDrives();
            var roots = new List<string>(drives.Length);
            for (var driveIndex = 0; driveIndex < drives.Length; driveIndex++)
            {
                if (drives[driveIndex].IsReady)
                {
                    roots.Add(drives[driveIndex].RootDirectory.FullName);
                }
            }

            folderPaths = roots.ToArray();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[PcBrowser] failed to list drives");
            folderPaths = Array.Empty<string>();
            listingFailed = true;
        }
    }

    private void NavigateTo(string directory)
    {
        currentDirectory = directory;
        rememberedDirectory = directory;
        listingFailed = false;
        scrollTopPending = true;
        try
        {
            folderPaths = ListVisibleFolders(directory);
            filePaths = ListImages(directory);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[PcBrowser] failed to list {directory}");
            folderPaths = Array.Empty<string>();
            filePaths = Array.Empty<string>();
            listingFailed = true;
        }
    }

    private static string[] ListVisibleFolders(string directory)
    {
        var all = Directory.GetDirectories(directory);
        var visible = new List<string>(all.Length);
        for (var index = 0; index < all.Length; index++)
        {
            var attributes = File.GetAttributes(all[index]);
            if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
            {
                visible.Add(all[index]);
            }
        }

        visible.Sort(static (left, right) =>
            string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase));
        return visible.ToArray();
    }

    private static string[] ListImages(string directory)
    {
        var all = Directory.GetFiles(directory);
        var images = new List<(string Path, DateTime WrittenUtc)>(all.Length);
        for (var index = 0; index < all.Length; index++)
        {
            if (HasImageExtension(all[index]))
            {
                images.Add((all[index], File.GetLastWriteTimeUtc(all[index])));
            }
        }

        images.Sort(static (left, right) => right.WrittenUtc.CompareTo(left.WrittenUtc));
        var sorted = new string[images.Count];
        for (var index = 0; index < images.Count; index++)
        {
            sorted[index] = images[index].Path;
        }

        return sorted;
    }

    private static bool HasImageExtension(string path)
    {
        for (var index = 0; index < ImageExtensions.Length; index++)
        {
            if (path.EndsWith(ImageExtensions[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        thumbnails.Dispose();
    }
}
