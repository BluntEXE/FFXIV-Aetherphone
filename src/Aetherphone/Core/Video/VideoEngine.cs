using System.Text.RegularExpressions;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Aetherphone.Core.Video;

internal sealed class VideoEngine : IDisposable
{
    internal const int ScreenWidth = 1920;
    internal const int ScreenHeight = 1080;

    private const float DefaultScreenSpawnDistance = 2.0f;
    private const float DefaultScreenHeightOffset = 1.0f;

    internal const float MinScreenScale = 0.1f;
    internal const float MaxScreenScale = 8.0f;

    internal const float ScreenPositionSliderRange = 10f;

    private readonly ScreenPainter _screenPainter;
    private readonly List<ScreenPositionPreset> _screenPresets = [];

    internal Vector3 ScreenPosition { get; private set; }
    internal float ScreenYaw { get; private set; }
    internal float ScreenScale { get; private set; } = 1.0f;

    internal Vector3 ScreenSpawnAnchor { get; private set; }

    private MpvRenderer? _mpvRenderer;
    private readonly Texture2D _screenTexture;
    private static readonly Texture2DDescription ScreenTextureDescription = new()
    {
        Width = ScreenWidth,
        Height = ScreenHeight,
        MipLevels = 1,
        ArraySize = 1,
        Format = Format.B8G8R8A8_UNorm,
        BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
        CpuAccessFlags = CpuAccessFlags.None,
        SampleDescription = new SampleDescription(1, 0),
        Usage = ResourceUsage.Default,
        OptionFlags = ResourceOptionFlags.None,
    };
    private CancellationTokenSource _renderCancellation = new();

    private DateTime _lastLoadYT = DateTime.MinValue;
    private static readonly Regex YtRegex = new(@"^\w+://[^/]*youtube\.\w+/|^\w+://youtu\.be/", RegexOptions.Compiled);
    private static bool IsYTURL(string url) => YtRegex.IsMatch(url);

    private bool _isActive;
    private int _pendingVolume = 60;

    internal bool HardwareDecoding { get; set; }
    internal int MaxQualityHeight { get; set; } = 720;
    internal bool AllowInsecureDirectUrls { get; set; }

    internal Resources Resources { get; }

    internal VideoEngine()
    {
        Resources = new Resources();
        Resources.NativeLoader.Register(Resources);
        MpvRenderer.Setup(Resources);
        DxHandler.Initialise(Plugin.PluginInterface);

        _screenTexture = new Texture2D(DxHandler.Device, ScreenTextureDescription);
        _screenPainter = new ScreenPainter();

        _screenPresets.AddRange(Plugin.Cfg.ScreenPresets);
    }

    internal bool IsActive => _isActive;

    internal string? LastError { get; private set; }

    internal void StopVideo()
    {
        _isActive = false;
        _mpvRenderer?.Stop();
        _mpvRenderer = null;

        _screenPainter.SetTarget(null);
    }

    internal void PlayVideo(string url, int playbackPosition = 0, bool isPlaying = true)
    {
        if (_mpvRenderer != null && _mpvRenderer.GetCurrentUrl() == url && !_mpvRenderer.IsIdle())
        {
            return;
        }

        Resources.EnsureProvisioned();

        LastError = null;
        AssignScreenForSession(_screenTexture);

        Task.Run(async () =>
        {
            if (IsYTURL(url))
            {
                TimeSpan elapsed = DateTime.Now - _lastLoadYT;
                if (elapsed.TotalSeconds < 7)
                {
                    int sleepTime = Math.Min(Math.Max((int)(7000 - elapsed.TotalMilliseconds), 0), 7000);
                    Thread.Sleep(sleepTime);
                }

                _lastLoadYT = DateTime.Now;
            }

            try
            {
                if (_mpvRenderer != null)
                {
                    _mpvRenderer.Play(url, playbackPosition, isPlaying);
                    _isActive = true;
                    _screenPainter.SetTransform(ScreenPosition, ScreenYaw, ScreenScale);
                    return;
                }

                _mpvRenderer = new MpvRenderer();
                _mpvRenderer.Initialize(ScreenWidth, ScreenHeight, _screenTexture, _renderCancellation,
                    HardwareDecoding, MaxQualityHeight, AllowInsecureDirectUrls, _pendingVolume);
                _mpvRenderer.Play(url, playbackPosition, isPlaying);
                _isActive = true;
                _screenPainter.SetTransform(ScreenPosition, ScreenYaw, ScreenScale);
                while (true)
                {
                    if (!_mpvRenderer.RenderFrame())
                    {
                        break;
                    }
                }

                AepLog.Debug("Stopping Video Player");
            }
            catch (Exception e)
            {
                AepLog.Error($"[MPV] Generic error: {e.Message} {e.StackTrace}");
                LastError = e.Message;
            }
        });
    }

    internal void Pause(bool pause)
    {
        if (!_renderCancellation.Token.IsCancellationRequested)
        {
            _mpvRenderer?.Pause(pause);
        }
    }

    internal bool GetIdle()
    {
        if (!_renderCancellation.Token.IsCancellationRequested)
        {
            return _mpvRenderer?.IsEofReached() ?? true;
        }

        return true;
    }

    internal bool GetPaused()
    {
        if (!_renderCancellation.Token.IsCancellationRequested)
        {
            return _mpvRenderer?.GetPaused() ?? false;
        }

        return false;
    }

    internal double[] GetInfo()
    {
        if (!_renderCancellation.Token.IsCancellationRequested)
        {
            return _mpvRenderer?.GetProperties() ?? [0, 0, 0];
        }

        return [0, 0, 0];
    }

    internal void Seek(int seconds)
    {
        if (!_renderCancellation.Token.IsCancellationRequested)
        {
            _mpvRenderer?.Seek(seconds);
        }
    }

    internal void SetVolume(int vol)
    {
        _pendingVolume = Math.Clamp(vol, 0, 100);
        if (!_renderCancellation.Token.IsCancellationRequested)
        {
            _mpvRenderer?.SetVolume(vol);
        }
    }

    internal byte[]? TryGetFrame(out int width, out int height)
    {
        if (_mpvRenderer is null)
        {
            width = ScreenWidth;
            height = ScreenHeight;
            return null;
        }

        return _mpvRenderer.TryGetFrame(out width, out height);
    }

    internal string GetMediaTitle()
    {
        if (!_renderCancellation.Token.IsCancellationRequested)
        {
            return _mpvRenderer?.GetMediaTitle() ?? string.Empty;
        }

        return string.Empty;
    }

    internal string? GetCurrentUrl() => _mpvRenderer?.GetCurrentUrl();

    internal static bool ValidateURL(string inputUrl, out Uri? url)
    {
        string formattedUrl = inputUrl;

        if (!formattedUrl.StartsWith("http://", StringComparison.Ordinal) && !formattedUrl.StartsWith("https://", StringComparison.Ordinal))
        {
            formattedUrl = "https://" + formattedUrl;
        }

        return Uri.TryCreate(formattedUrl, UriKind.Absolute, out url)
            && (url?.Scheme == Uri.UriSchemeHttp || url?.Scheme == Uri.UriSchemeHttps)
            && url.Host.Contains('.') && !url.Host.EndsWith('.')
            && Uri.CheckHostName(url.Host) == UriHostNameType.Dns;
    }

    private void SpawnScreenInFrontOfLocalPlayer()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is null)
        {
            return;
        }

        float yaw = localPlayer.Rotation;
        Vector3 forward = Vector3.Transform(Vector3.UnitZ, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw));

        var position = localPlayer.Position + forward * DefaultScreenSpawnDistance + new Vector3(0, DefaultScreenHeightOffset, 0);
        ScreenSpawnAnchor = position;
        SetScreenTransform(position, yaw + MathF.PI, 1.0f);
    }

    internal void RecenterScreen() => SpawnScreenInFrontOfLocalPlayer();

    internal void SetScreenTransform(Vector3 position, float yaw, float scale)
    {
        ScreenPosition = position;
        ScreenYaw = yaw;
        ScreenScale = Math.Clamp(scale, MinScreenScale, MaxScreenScale);

        if (_isActive)
        {
            _screenPainter.SetTransform(ScreenPosition, ScreenYaw, ScreenScale);
        }
    }

    internal List<ScreenPositionPreset> GetScreenPresets() => [.. _screenPresets];

    internal void SaveScreenPreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _screenPresets.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        _screenPresets.Add(new ScreenPositionPreset
        {
            Name = name, X = ScreenPosition.X, Y = ScreenPosition.Y, Z = ScreenPosition.Z, Yaw = ScreenYaw,
            Scale = ScreenScale,
        });

        Plugin.Cfg.ScreenPresets = _screenPresets;
        Plugin.Cfg.Save();
    }

    internal void RemoveScreenPreset(string name)
    {
        _screenPresets.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        Plugin.Cfg.ScreenPresets = _screenPresets;
        Plugin.Cfg.Save();
    }

    internal void ApplyScreenPreset(ScreenPositionPreset preset)
    {
        var position = new Vector3(preset.X, preset.Y, preset.Z);
        ScreenSpawnAnchor = position;
        SetScreenTransform(position, preset.Yaw, preset.Scale);
    }

    internal void ApplyRemoteScreenTransform(Vector3 position, float yaw, float scale)
    {
        ScreenSpawnAnchor = position;
        SetScreenTransform(position, yaw, scale);
    }

    private void AssignScreenForSession(Texture2D screenTexture)
    {
        bool isNewSession = !_isActive;
        _screenPainter.SetTarget(screenTexture);

        if (isNewSession)
        {
            SpawnScreenInFrontOfLocalPlayer();
        }
    }

    public void Dispose()
    {
        _mpvRenderer?.Dispose();
        _screenPainter.Dispose();
        Resources.Dispose();
    }
}
