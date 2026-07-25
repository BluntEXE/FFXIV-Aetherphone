using Penumbra.Api.IpcSubscribers;

namespace Aetherphone.Core.Video;

// Ported from AlphaChannel's Resources.LoadPenumbraScreenResources / LoadPenumbraModResources /
// FixTempCopyGamePaths (Voudi, GPL-3.0). Penumbra's temp-mod redirects need their local file
// targets to live under Penumbra's own managed mod directory, so bundled files get copied there
// once, GUID-suffixed so concurrent sessions - and AlphaChannel running alongside - never
// collide on the same staged file name.
internal sealed class ScreenResourceStaging
{
    private const string StagingSubfolder = "AetherStreamTemp";
    private readonly string bundledRoot;
    private readonly Guid sessionId;

    public ScreenResourceStaging(Guid sessionId)
    {
        this.sessionId = sessionId;
        bundledRoot = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
            "Resources", "Video");
    }

    public string ScreenVfxGamePath => string.Format(VideoRenderTarget.ScreenVfxGamePathTemplate, sessionId.ToString("N"));

    public bool TryBuildScreenPaths(out Dictionary<string, string> paths, out string? error)
    {
        paths = new Dictionary<string, string>();
        if (!TryStage(VideoRenderTarget.ScreenTextureBundledFile, out var texturePath, out error))
        {
            return false;
        }

        paths[VideoRenderTarget.ScreenTextureGamePath] = texturePath;

        if (!TryStage(VideoRenderTarget.ScreenVfxBundledFile, out var vfxPath, out error))
        {
            return false;
        }

        paths[ScreenVfxGamePath] = vfxPath;
        return true;
    }

    public bool TryBuildCompanionModPaths(out Dictionary<string, string> paths, out string? error)
    {
        paths = new Dictionary<string, string>();
        foreach (var (gamePath, bundledFile) in VideoRenderTarget.CompanionModFiles)
        {
            if (!TryStage(bundledFile, out var staged, out error))
            {
                return false;
            }

            paths[gamePath] = staged;
        }

        error = null;
        return true;
    }

    private bool TryStage(string bundledRelativePath, out string stagedPath, out string? error)
    {
        stagedPath = string.Empty;
        var source = Path.Combine(bundledRoot, bundledRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(source))
        {
            error = $"Missing bundled video resource: {source}";
            return false;
        }

        try
        {
            var modDirectory = new GetModDirectory(Plugin.PluginInterface).Invoke();
            var stagingRoot = Path.Combine(modDirectory, StagingSubfolder);
            Directory.CreateDirectory(stagingRoot);
            var fileName = $"{Path.GetFileNameWithoutExtension(bundledRelativePath)}_{sessionId:N}" +
                Path.GetExtension(bundledRelativePath);
            var destination = Path.Combine(stagingRoot, fileName);
            File.Copy(source, destination, true);
            stagedPath = destination;
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Failed staging {bundledRelativePath} into Penumbra's mod directory: {exception.Message}";
            return false;
        }
    }
}
