using System.Security.Cryptography;
using Aetherphone.Core;
using Aetherphone.Core.Video;
using SharpCompress.Readers;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomAssets : IDisposable
{
    public const string FolderName = "doom";
    public const string SharewareFileName = "doom1.wad";
    public const string SoundfontFileName = "TimGM6mb.sf2";
    public const long SharewareDownloadBytes = 1756095;
    public const long SoundfontDownloadBytes = 5560953;
    public static readonly string[] IwadPreference =
    {
        "doom2.wad", "plutonia.wad", "tnt.wad", "doom.wad", "freedoom2.wad", "freedoom1.wad", SharewareFileName,
    };
    private const string SharewareUrl =
        "https://deb.debian.org/debian/pool/non-free/d/doom-wad-shareware/doom-wad-shareware_1.9.fixed.orig.tar.gz";
    private const string SharewareSha256 = "1d7d43be501e67d927e415e0b8f3e29c3bf33075e859721816f652a526cac771";
    private const long SharewareMinimumBytes = 4_000_000;
    private const string SoundfontUrl =
        "https://deb.debian.org/debian/pool/main/t/timgm6mb-soundfont/timgm6mb-soundfont_1.3.orig.tar.gz";
    private const string SoundfontSha256 = "c5378b62028c920cb11e4803327983fee2f2cdff5dc89c708e39da417e51c854";
    private const long SoundfontMinimumBytes = 5_000_000;
    private const string StagingSuffix = ".download";
    private const string FreshSuffix = ".fresh";
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
    private readonly HttpClient httpClient;
    private readonly string folder;
    private int installing;

    public DoomAssets()
    {
        httpClient = new HttpClient { Timeout = DownloadTimeout };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Aetherphone-Doom");
        folder = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, FolderName);
        Wad = new MediaDependency("doom-wad", SharewareUrl, string.Empty, ".tar.gz", SharewareFileName,
            SharewareMinimumBytes);
        Soundfont = new MediaDependency("doom-soundfont", SoundfontUrl, string.Empty, ".tar.gz", SoundfontFileName,
            SoundfontMinimumBytes);
        Wad.SetTotalBytes(SharewareDownloadBytes);
        Soundfont.SetTotalBytes(SoundfontDownloadBytes);
        RefreshStates();
    }

    public MediaDependency Wad { get; }
    public MediaDependency Soundfont { get; }
    public string Folder => folder;
    public bool Installing => installing != 0;

    public string? IwadPath()
    {
        for (var index = 0; index < IwadPreference.Length; index++)
        {
            var path = Path.Combine(folder, IwadPreference[index]);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public string? SoundfontPath()
    {
        var path = Path.Combine(folder, SoundfontFileName);
        return File.Exists(path) ? path : null;
    }

    public static string? PreferredIwad(ReadOnlySpan<string> fileNames)
    {
        for (var preferenceIndex = 0; preferenceIndex < IwadPreference.Length; preferenceIndex++)
        {
            for (var fileIndex = 0; fileIndex < fileNames.Length; fileIndex++)
            {
                if (string.Equals(fileNames[fileIndex], IwadPreference[preferenceIndex], StringComparison.OrdinalIgnoreCase))
                {
                    return fileNames[fileIndex];
                }
            }
        }

        return null;
    }

    public long PendingBytes()
    {
        var pending = 0L;
        if (IwadPath() is null)
        {
            pending += SharewareDownloadBytes;
        }

        if (SoundfontPath() is null)
        {
            pending += SoundfontDownloadBytes;
        }

        return pending;
    }

    public void RefreshStates()
    {
        if (!Installing)
        {
            Wad.SetState(IwadPath() is null ? DependencyState.Missing : DependencyState.Ready);
            Soundfont.SetState(SoundfontPath() is null ? DependencyState.Missing : DependencyState.Ready);
        }
    }

    public void Install()
    {
        if (Interlocked.CompareExchange(ref installing, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(InstallAsync);
    }

    private async Task InstallAsync()
    {
        try
        {
            if (IwadPath() is null)
            {
                await DownloadAsync(Wad, SharewareUrl, SharewareFileName, SharewareSha256, SharewareDownloadBytes)
                    .ConfigureAwait(false);
            }

            if (SoundfontPath() is null)
            {
                await DownloadAsync(Soundfont, SoundfontUrl, SoundfontFileName, SoundfontSha256, SoundfontDownloadBytes)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref installing, 0);
        }
    }

    private async Task DownloadAsync(MediaDependency dependency, string url, string entryName, string sha256,
        long expectedBytes)
    {
        var staging = Path.Combine(folder, dependency.Id + StagingSuffix);
        var target = Path.Combine(folder, entryName);
        var fresh = target + FreshSuffix;
        dependency.ResetTransfer();
        dependency.SetTotalBytes(expectedBytes);
        dependency.SetState(DependencyState.Downloading);
        try
        {
            Directory.CreateDirectory(folder);
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is { } length && length > 0)
                {
                    dependency.SetTotalBytes(length);
                }

                await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var destination = new FileStream(staging, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                var received = 0L;
                while (true)
                {
                    var read = await source.ReadAsync(buffer).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    received += read;
                    dependency.ReportReceived(received);
                }
            }

            dependency.SetState(DependencyState.Installing);
            ExtractEntry(staging, entryName, fresh);
            var actual = HashFile(fresh);
            if (!actual.Equals(sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{entryName} did not match its expected checksum.");
            }

            File.Move(fresh, target, true);
            dependency.SetState(DependencyState.Ready);
            AepLog.Debug($"[Doom] {entryName} is ready");
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Doom] Installing {entryName} failed: {exception.Message}");
            dependency.Fail(exception.Message);
        }
        finally
        {
            QuietDelete(staging);
            QuietDelete(fresh);
        }
    }

    private static void ExtractEntry(string archivePath, string entryName, string destination)
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream);
        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            if (entry.IsDirectory || entry.Key is null)
            {
                continue;
            }

            if (!Path.GetFileName(entry.Key).Equals(entryName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            reader.WriteEntryTo(target);
            return;
        }

        throw new InvalidOperationException($"{entryName} was not in the downloaded archive.");
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static void QuietDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
