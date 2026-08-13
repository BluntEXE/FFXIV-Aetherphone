using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Aetherphone.Core.Video;

internal enum DependencyState : byte
{
    Unknown,
    Checking,
    Missing,
    Downloading,
    Installing,
    Ready,
    Failed,
}

internal readonly record struct DependencyProgress(
    DependencyState State,
    long ReceivedBytes,
    long TotalBytes,
    string? FailureReason)
{
    internal float Fraction => TotalBytes > 0 ? Math.Clamp((float)ReceivedBytes / TotalBytes, 0f, 1f) : 0f;
}

internal sealed class MediaDependency
{
    private long receivedBytes;
    private long totalBytes;
    private volatile DependencyState state = DependencyState.Unknown;
    private volatile string? failureReason;

    internal MediaDependency(string id, string releaseUrl, string assetPrefix, string assetSuffix, string payloadName,
        long minimumPayloadBytes)
    {
        Id = id;
        ReleaseUrl = releaseUrl;
        AssetPrefix = assetPrefix;
        AssetSuffix = assetSuffix;
        PayloadName = payloadName;
        MinimumPayloadBytes = minimumPayloadBytes;
    }

    internal string Id { get; }
    internal string ReleaseUrl { get; }
    internal string AssetPrefix { get; }
    internal string AssetSuffix { get; }
    internal string PayloadName { get; }
    internal long MinimumPayloadBytes { get; }

    internal string? DownloadUrl { get; set; }
    internal string? RemoteVersion { get; set; }

    internal DependencyProgress Snapshot() => new(state, Interlocked.Read(ref receivedBytes),
        Interlocked.Read(ref totalBytes), failureReason);

    internal void SetState(DependencyState next)
    {
        state = next;
        if (next != DependencyState.Failed)
        {
            failureReason = null;
        }
    }

    internal void Fail(string reason)
    {
        failureReason = reason;
        state = DependencyState.Failed;
    }

    internal void SetTotalBytes(long total) => Interlocked.Exchange(ref totalBytes, total);

    internal void ReportReceived(long received) => Interlocked.Exchange(ref receivedBytes, received);

    internal void ResetTransfer()
    {
        Interlocked.Exchange(ref receivedBytes, 0);
        Interlocked.Exchange(ref totalBytes, 0);
    }
}

internal sealed class MediaDependencies : IDisposable
{
    private const string InstallFolder = "aetherstream";
    private const string VersionMarker = ".version";
    private const long MinimumLibraryBytes = 1 << 20;

    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    private readonly HttpClient httpClient;
    private readonly string installRoot;
    private readonly SemaphoreSlim installGate = new(1, 1);

    internal MediaDependencies()
    {
        httpClient = new HttpClient { Timeout = DownloadTimeout };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Aetherphone-AetherStream");
        installRoot = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, InstallFolder);

        VideoLibrary = new MediaDependency("mpv", "https://api.github.com/repos/zhongfly/mpv-winbuild/releases/latest",
            "mpv-dev-lgpl-x86_64-", ".7z", "libmpv-2.dll", MinimumLibraryBytes);
        LinkResolver = new MediaDependency("yt-dlp", "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest",
            "yt-dlp.exe", ".exe", "yt-dlp.exe", MinimumLibraryBytes);

        AdoptLegacyInstalls();
        RefreshInstalledState();
        NativeLoader.Register(this);
    }

    internal MediaDependency VideoLibrary { get; }
    internal MediaDependency LinkResolver { get; }

    internal string? VideoLibraryPath => VerifiedPayload(VideoLibrary);
    internal string? LinkResolverPath => VerifiedPayload(LinkResolver);

    internal bool IsReady => VideoLibraryPath is not null && LinkResolverPath is not null;

    internal long PendingDownloadBytes
    {
        get
        {
            var total = 0L;
            if (VideoLibraryPath is null)
            {
                total += VideoLibrary.Snapshot().TotalBytes;
            }

            if (LinkResolverPath is null)
            {
                total += LinkResolver.Snapshot().TotalBytes;
            }

            return total;
        }
    }

    private string ComponentFolder(MediaDependency dependency) => Path.Combine(installRoot, dependency.Id);

    private string PayloadPath(MediaDependency dependency) =>
        Path.Combine(ComponentFolder(dependency), dependency.PayloadName);

    private string? VerifiedPayload(MediaDependency dependency)
    {
        var path = PayloadPath(dependency);
        try
        {
            var file = new FileInfo(path);
            if (file.Exists && file.Length >= dependency.MinimumPayloadBytes)
            {
                return path;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Deps] Could not inspect {dependency.Id} at {path}: {exception.Message}");
        }

        return null;
    }

    private void RefreshInstalledState()
    {
        VideoLibrary.SetState(VideoLibraryPath is null ? DependencyState.Unknown : DependencyState.Ready);
        LinkResolver.SetState(LinkResolverPath is null ? DependencyState.Unknown : DependencyState.Ready);
    }

    private void AdoptLegacyInstalls()
    {
        AdoptLegacy(VideoLibrary, "mpv-dev-lgpl-x86_64-*", "libmpv-2.dll");
        AdoptLegacy(LinkResolver, "yt-dlp*", "yt-dlp.exe");
    }

    private void AdoptLegacy(MediaDependency dependency, string folderPattern, string payloadName)
    {
        if (VerifiedPayload(dependency) is not null)
        {
            return;
        }

        try
        {
            var configDirectory = Plugin.PluginInterface.ConfigDirectory.FullName;
            var candidates = Directory.GetDirectories(configDirectory, folderPattern);
            for (var index = 0; index < candidates.Length; index++)
            {
                var source = Path.Combine(candidates[index], payloadName);
                var file = new FileInfo(source);
                if (!file.Exists || file.Length < dependency.MinimumPayloadBytes)
                {
                    continue;
                }

                var target = PayloadPath(dependency);
                Directory.CreateDirectory(ComponentFolder(dependency));
                File.Copy(source, target, overwrite: true);
                AepLog.Debug($"[Deps] Adopted an existing {dependency.Id} install from {candidates[index]}");
                return;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Deps] Could not adopt an existing {dependency.Id} install: {exception.Message}");
        }
    }

    internal async Task<bool> EnsureReadyAsync(CancellationToken token)
    {
        if (IsReady)
        {
            return true;
        }

        await installGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (IsReady)
            {
                return true;
            }

            await PrepareAsync(VideoLibrary, token).ConfigureAwait(false);
            await PrepareAsync(LinkResolver, token).ConfigureAwait(false);
            return IsReady;
        }
        finally
        {
            installGate.Release();
        }
    }

    internal async Task CheckSizesAsync(CancellationToken token)
    {
        await CheckAsync(VideoLibrary, token).ConfigureAwait(false);
        await CheckAsync(LinkResolver, token).ConfigureAwait(false);
    }

    private async Task PrepareAsync(MediaDependency dependency, CancellationToken token)
    {
        if (VerifiedPayload(dependency) is not null)
        {
            dependency.SetState(DependencyState.Ready);
            return;
        }

        if (dependency.DownloadUrl is null && !await CheckAsync(dependency, token).ConfigureAwait(false))
        {
            return;
        }

        await DownloadAsync(dependency, token).ConfigureAwait(false);
    }

    private async Task<bool> CheckAsync(MediaDependency dependency, CancellationToken token)
    {
        dependency.SetState(DependencyState.Checking);
        try
        {
            var json = await httpClient.GetStringAsync(dependency.ReleaseUrl, token).ConfigureAwait(false);
            var release = JObject.Parse(json);
            var assets = release["assets"] as JArray;
            if (assets is null)
            {
                dependency.Fail("The release listing had no downloads.");
                return false;
            }

            for (var index = 0; index < assets.Count; index++)
            {
                var name = assets[index]["name"]?.Value<string>();
                if (name is null || !name.StartsWith(dependency.AssetPrefix, StringComparison.Ordinal)
                    || !name.EndsWith(dependency.AssetSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                dependency.DownloadUrl = assets[index]["browser_download_url"]?.Value<string>();
                dependency.RemoteVersion = release["tag_name"]?.Value<string>() ?? release["id"]?.Value<string>();
                dependency.SetTotalBytes(assets[index]["size"]?.Value<long>() ?? 0);
                if (dependency.DownloadUrl is null)
                {
                    dependency.Fail("The release listing had no download link.");
                    return false;
                }

                dependency.SetState(VerifiedPayload(dependency) is null
                    ? DependencyState.Missing
                    : DependencyState.Ready);
                return true;
            }

            dependency.Fail("No matching download in the latest release.");
            return false;
        }
        catch (OperationCanceledException)
        {
            dependency.SetState(DependencyState.Unknown);
            return false;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Deps] Update check for {dependency.Id} failed: {exception.Message}");
            dependency.Fail(exception.Message);
            return false;
        }
    }

    private async Task DownloadAsync(MediaDependency dependency, CancellationToken token)
    {
        if (dependency.DownloadUrl is not { Length: > 0 } url)
        {
            return;
        }

        var staging = Path.Combine(installRoot, dependency.Id + ".download");
        dependency.ResetTransfer();
        dependency.SetState(DependencyState.Downloading);

        try
        {
            Directory.CreateDirectory(installRoot);
            using var response = await httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is { } length && length > 0)
            {
                dependency.SetTotalBytes(length);
            }

            await using (var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var destination = new FileStream(staging, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                var received = 0L;
                while (true)
                {
                    var read = await source.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    received += read;
                    dependency.ReportReceived(received);
                }
            }

            dependency.SetState(DependencyState.Installing);
            Install(dependency, staging);

            if (VerifiedPayload(dependency) is null)
            {
                dependency.Fail($"{dependency.PayloadName} was missing after installing.");
                return;
            }

            WriteVersionMarker(dependency);
            dependency.SetState(DependencyState.Ready);
            AepLog.Debug($"[Deps] {dependency.Id} is ready");
        }
        catch (OperationCanceledException)
        {
            dependency.SetState(DependencyState.Unknown);
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Deps] Installing {dependency.Id} failed: {exception.Message}");
            dependency.Fail(exception.Message);
        }
        finally
        {
            TryDelete(staging);
        }
    }

    private void Install(MediaDependency dependency, string staging)
    {
        var folder = ComponentFolder(dependency);
        Directory.CreateDirectory(folder);

        if (!dependency.AssetSuffix.Equals(".7z", StringComparison.Ordinal))
        {
            File.Copy(staging, PayloadPath(dependency), overwrite: true);
            return;
        }

        using var archive = ArchiveFactory.OpenArchive(staging);
        var options = new ExtractionOptions { ExtractFullPath = false, Overwrite = true };
        foreach (var entry in archive.Entries)
        {
            if (entry.IsDirectory || entry.Key is null)
            {
                continue;
            }

            if (!Path.GetFileName(entry.Key).Equals(dependency.PayloadName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entry.WriteToDirectory(folder, options);
            return;
        }

        throw new InvalidOperationException($"{dependency.PayloadName} was not in the downloaded archive.");
    }

    private void WriteVersionMarker(MediaDependency dependency)
    {
        if (dependency.RemoteVersion is not { Length: > 0 } version)
        {
            return;
        }

        try
        {
            File.WriteAllText(Path.Combine(ComponentFolder(dependency), VersionMarker), version);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Deps] Could not record the {dependency.Id} version: {exception.Message}");
        }
    }

    internal string? InstalledVersion(MediaDependency dependency)
    {
        try
        {
            var marker = Path.Combine(ComponentFolder(dependency), VersionMarker);
            return File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Deps] Could not read the {dependency.Id} version: {exception.Message}");
            return null;
        }
    }

    internal bool HasUpdate(MediaDependency dependency)
    {
        if (dependency.RemoteVersion is not { Length: > 0 } remote || VerifiedPayload(dependency) is null)
        {
            return false;
        }

        return InstalledVersion(dependency) is { Length: > 0 } installed
            && !installed.Equals(remote, StringComparison.Ordinal);
    }

    internal async Task<bool> ReinstallAsync(MediaDependency dependency, CancellationToken token)
    {
        await installGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (!await CheckAsync(dependency, token).ConfigureAwait(false))
            {
                return false;
            }

            await DownloadAsync(dependency, token).ConfigureAwait(false);
            return VerifiedPayload(dependency) is not null;
        }
        finally
        {
            installGate.Release();
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Deps] Could not clean up {path}: {exception.Message}");
        }
    }

    public void Dispose()
    {
        httpClient.Dispose();
        installGate.Dispose();
    }

    internal static class NativeLoader
    {
        private static MediaDependencies? dependencies;
        private static bool registered;

        internal static void Register(MediaDependencies owner)
        {
            dependencies = owner;
            if (registered)
            {
                return;
            }

            registered = true;
            NativeLibrary.SetDllImportResolver(typeof(NativeLoader).Assembly, Resolve);
        }

        private static IntPtr Resolve(string name, System.Reflection.Assembly assembly, DllImportSearchPath? path)
        {
            if (!name.Equals("libmpv-2", StringComparison.Ordinal))
            {
                return IntPtr.Zero;
            }

            var location = dependencies?.VideoLibraryPath;
            if (location is not null && NativeLibrary.TryLoad(location, out var handle))
            {
                return handle;
            }

            AepLog.Error($"[Deps] Could not load the video library from: {location ?? "(not installed)"}");
            return IntPtr.Zero;
        }
    }
}
