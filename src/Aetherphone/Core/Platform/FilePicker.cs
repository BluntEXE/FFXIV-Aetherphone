using Aetherphone.Core.Localization;
using Dalamud.Interface.ImGuiFileDialog;

namespace Aetherphone.Core.Platform;

internal static class FilePicker
{
    private const string ImageExtensions = "{.png,.jpg,.jpeg,.bmp},.*";
    private const string AudioExtensions = "{.mp3,.wav},.*";
    private static readonly FileDialogManager Manager = new();

    public static void Draw()
    {
        Manager.Draw();
    }

    public static void PickImage(string title, Action<string> onPicked)
    {
        if (UsesNativeDialog)
        {
            NativeFileDialog.PickImage(title, onPicked);
            return;
        }

        Open(title, Loc.T(L.Common.FileKindImages) + ImageExtensions,
            ExistingFolder(Environment.SpecialFolder.MyPictures), onPicked);
    }

    public static void PickAudio(string title, Action<string> onPicked)
    {
        if (UsesNativeDialog)
        {
            NativeFileDialog.PickAudio(title, onPicked);
            return;
        }

        Open(title, Loc.T(L.Common.FileKindAudio) + AudioExtensions,
            ExistingFolder(Environment.SpecialFolder.MyMusic), onPicked);
    }

    private static bool UsesNativeDialog => Plugin.Cfg?.UseNativeFileDialog ?? NativeFileDialog.IsSupported;

    private static void Open(string title, string filters, string startPath, Action<string> onPicked)
    {
        Manager.OpenFileDialog(title, filters, (success, paths) =>
        {
            if (!success || paths.Count == 0)
            {
                return;
            }

            var path = paths[0];
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            onPicked(path);
        }, 1, startPath, false);
    }

    private static string ExistingFolder(Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder);
        return !string.IsNullOrEmpty(path) && Directory.Exists(path) ? path : string.Empty;
    }
}
