using System.Runtime.InteropServices;

namespace Aetherphone.Core.Video;

internal static class VideoNativeLibrary
{
    private const string MpvName = "libmpv-2";
    private static bool registered;

    // Surfaced in AetherStream's Settings status rows - the spec calls silent dependency failure
    // this app's worst failure mode, so this stays visible rather than only going to /xllog.
    public static string? LoadError { get; private set; }

    private static bool ytdlpChecked;

    // yt-dlp isn't loaded like mpv (it's an external subprocess, invoked by mpv's ytdl_hook, not
    // by this plugin directly) so there is no load event to hook - just a presence check on the
    // bundled binary, mirroring LoadError's shape for the Settings status row.
    public static string? YtdlpError { get; private set; }

    public static void EnsureYtdlpChecked()
    {
        if (ytdlpChecked)
        {
            return;
        }

        ytdlpChecked = true;
        var path = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Native",
            "yt-dlp.exe");
        YtdlpError = File.Exists(path) ? null : $"yt-dlp.exe not found at: {path}";
    }

    public static void EnsureRegistered()
    {
        if (registered)
        {
            return;
        }

        registered = true;
        NativeLibrary.SetDllImportResolver(typeof(VideoNativeLibrary).Assembly, Resolve);
    }

    private static nint Resolve(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != MpvName)
        {
            return nint.Zero;
        }

        var path = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Native",
            "libmpv-2.dll");
        if (NativeLibrary.TryLoad(path, out var handle))
        {
            LoadError = null;
            return handle;
        }

        LoadError = $"Failed to load libmpv-2 from: {path}";
        AepLog.Error($"[Video] {LoadError}");
        return nint.Zero;
    }
}
