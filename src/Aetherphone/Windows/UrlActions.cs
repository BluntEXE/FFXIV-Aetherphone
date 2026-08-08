using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using System.Diagnostics;

namespace Aetherphone.Windows;

internal static class UrlActions
{
    public static void OpenInBrowser(string url, Action<Exception>? onError = null)
    {
        if (!IsWebUrl(url))
        {
            var rejected = new NotSupportedException("Only http and https links can be opened.");
            AepLog.Warning(rejected, $"Refused to open a non-web link: {url}");
            onError?.Invoke(rejected);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"Opening {url} in a browser failed; copied it to the clipboard instead");
            ImGui.SetClipboardText(url);
            onError?.Invoke(exception);
        }
    }

    public static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, $"Failed to open the folder: {path}");
        }
    }

    private static bool IsWebUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return parsed.Host.Length > 0;
    }
}
