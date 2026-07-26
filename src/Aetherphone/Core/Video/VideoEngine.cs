using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using InteropGenerator.Runtime;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Aetherphone.Core.Video;

// Ported from AlphaChannel's Core (Voudi, GPL-3.0), with the companion/minion tracking removed -
// see port/alphachannel-engine Stage 4. AlphaChannel's actual working display trick is kept as-is:
// GetResourceSync/TexOnLoad hook a live D3D11 texture into an already-rendering particle VFX
// (chara/monster/m7002/.../aetherstreamscreen_{session}.avfx), which is what actually renders
// in-game. That VFX is now invoked directly on the local player character instead of a summoned
// Carbuncle, so there is exactly one target and no per-owner tracking, appearance mod, or summon
// step at all.
internal sealed class VideoEngine : IDisposable
{
    internal const int ScreenWidth = 1920;
    internal const int ScreenHeight = 1080;

    private readonly Guid _sessionId;
    private readonly Dictionary<string, string> _screenPenumbraPaths;
    private readonly ConcurrentDictionary<nint, ShaderResourceView> _views = new();

    private MpvRenderer? _mpvRenderer;
    private Snes9xRenderer? _snesRenderer;
    private readonly Texture2D _screenTexture;
    private readonly Texture2D _snesScreenTexture;
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
    private static readonly Texture2DDescription SnesTextureDescription = new()
    {
        Width = ScreenWidth,
        Height = ScreenHeight,
        MipLevels = 1,
        ArraySize = 1,
        Format = Format.B5G6R5_UNorm,
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

    private bool _isPlayingSnes;
    private bool _snesControlsEnabled;
    private bool _isActive; // whether the screen VFX should currently be playing for the local player
    private bool _lastIdle = true;
    private readonly List<string> _recentSnesPaths = [];
    private int _pendingVolume = 60;

    // Read fresh at Play() time by MpvRenderer.Initialize so a settings change takes effect on
    // the next video, not the current one - matching how the old VideoPlayer read these.
    internal bool HardwareDecoding { get; set; }
    internal int MaxQualityHeight { get; set; } = 720;
    internal bool AllowInsecureDirectUrls { get; set; }

    internal Resources Resources { get; }
    internal InputManager Input { get; }
    internal WndProcKeyUpReader WindowKeyUpReader { get; }

    internal unsafe VideoEngine(Guid sessionId)
    {
        _sessionId = sessionId;

        Resources = new Resources(sessionId);
        Resources.NativeLoader.Register(Resources);
        MpvRenderer.Setup(Resources);
        DxHandler.Initialise(Plugin.PluginInterface);
        _screenPenumbraPaths = Resources.LoadPenumbraScreenResources();

        WindowKeyUpReader = new WndProcKeyUpReader(Plugin.PluginInterface.UiBuilder.WindowHandlePtr,
            Plugin.InteropProvider);
        Input = new InputManager(WindowKeyUpReader);

        _screenTexture = new Texture2D(DxHandler.Device, ScreenTextureDescription);
        _snesScreenTexture = new Texture2D(DxHandler.Device, SnesTextureDescription);

        _getResourceSyncHook = Plugin.InteropProvider.HookFromAddress<ResourceManager.Delegates.GetResourceSync>(
            ResourceManager.Addresses.GetResourceSync.Value, GetResourceSyncDetour);
        _textureOnLoadHook = Plugin.InteropProvider.HookFromAddress<Texture.Delegates.InitializeContents>(
            Texture.Addresses.InitializeContents.Value, TexOnLoadDetour);
        nint actorVfxCreateAddress = Plugin.SigScanner.ScanText(ActorVfxCreateSig);
        _actorVfxCreate = Marshal.GetDelegateForFunctionPointer<ActorVfxCreateDelegate>(actorVfxCreateAddress);

        _getResourceSyncHook.Enable();

        _recentSnesPaths.AddRange(Plugin.Cfg.SnesRecentRomPaths);
    }

    internal bool IsActive => _isActive;
    internal bool IsHookHealthy => _getResourceSyncHook is { IsDisposed: false };

    internal void StopVideo()
    {
        _isActive = false;
        if (_isPlayingSnes)
        {
            _snesRenderer?.Unload();
            _isPlayingSnes = false;
        }
        else
        {
            _mpvRenderer?.Stop();
            _mpvRenderer = null;
        }

        PenumbraIPC.RemoveTempMod("screenvfx");
    }

    internal void PlayVideo(string url, int playbackPosition = 0, bool isPlaying = true)
    {
        if (_mpvRenderer != null && _mpvRenderer.GetCurrentUrl() == url && !_mpvRenderer.IsIdle())
        {
            return;
        }

        Task.Run(async () =>
        {
            if (IsYTURL(url))
            {
                TimeSpan elapsed = DateTime.Now - _lastLoadYT;
                if (elapsed.TotalSeconds < 7)
                {
                    int sleepTime = Math.Min(Math.Max((int)(7000 - elapsed.TotalMilliseconds), 0), 7000); //Add some sleep time to avoid hitting rate limits
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
                    return;
                }

                _mpvRenderer = new MpvRenderer();
                _mpvRenderer.Initialize(ScreenWidth, ScreenHeight, _screenTexture, _renderCancellation,
                    HardwareDecoding, MaxQualityHeight, AllowInsecureDirectUrls, _pendingVolume);
                _mpvRenderer.Play(url, playbackPosition, isPlaying);
                _isActive = true;
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
        if (_isPlayingSnes)
        {
            _snesRenderer?.SetVolume(vol);
        }
        else if (!_renderCancellation.Token.IsCancellationRequested)
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

    internal bool ValidateURL(string inputUrl, out Uri? url)
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

    internal bool PlaySnes(string path)
    {
        try
        {
            _snesRenderer ??= new Snes9xRenderer(Resources.GetLocationSNES9X(), Resources.RomsDirectory);

            AddSnesPath(path);
            _snesControlsEnabled = true;
            _isPlayingSnes = _snesRenderer.Load(_snesScreenTexture, path);
            _isActive = true;
            AepLog.Debug("Starting ROM");
        }
        catch (Exception e)
        {
            AepLog.Error($"[SNES9X] Generic error: {e.Message} {e.StackTrace}");
        }

        return _isPlayingSnes;
    }

    internal void RemoveSnesPath(string path)
    {
        _recentSnesPaths.Remove(path);
    }

    private void AddSnesPath(string path)
    {
        _recentSnesPaths.Remove(path);
        _recentSnesPaths.Insert(0, path);
        if (_recentSnesPaths.Count > 6)
        {
            _recentSnesPaths.RemoveAt(_recentSnesPaths.Count - 1);
        }

        Plugin.Cfg.SnesRecentRomPaths = _recentSnesPaths;
        Plugin.Cfg.Save();
    }

    internal List<string> GetRecentSnesPaths() => [.. _recentSnesPaths];

    internal bool IsPlayingSnes() => _isPlayingSnes;

    internal bool IsSnesControlsEnabled() => _snesControlsEnabled;

    internal void EnableSnesControls(bool enabled) => _snesControlsEnabled = enabled;

    internal void OnFrameworkUpdate()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is not null && _isActive && !IsPlayingSnes())
        {
            bool idle = GetIdle();
            _lastIdle = idle;
            RefreshActorVfx(localPlayer.Address);
        }
        else if (localPlayer is not null && _isActive && IsPlayingSnes())
        {
            RefreshActorVfx(localPlayer.Address);
        }
        else
        {
            _lastIdle = true;
        }

        Input.OnFrameworkUpdate(_isPlayingSnes, _snesControlsEnabled, _snesRenderer);
    }

    // Ported from AlphaChannel's Core.RefreshActorVFX (Voudi, GPL-3.0), but always invoked with
    // the local player as both caster and target - there is no companion to point it at instead.
    // Called every tick while active so the VFX (which has a finite duration/loop) keeps replaying.
    private void RefreshActorVfx(nint localPlayerAddr)
    {
        if (!PenumbraIPC.CheckTempMod("screenvfx"))
        {
            PenumbraIPC.ApplyTempMod("screenvfx", _screenPenumbraPaths);
            return;
        }

        lock (_screenTextureLock)
        {
            var path = _isPlayingSnes
                ? $"chara/monster/m7002/obj/body/b0001/vfx/texture/snesscreen_{_sessionId}.avfx"
                : $"chara/monster/m7002/obj/body/b0001/vfx/texture/aetherstreamscreen_{_sessionId}.avfx";
            _actorVfxCreate?.Invoke(path, localPlayerAddr, localPlayerAddr, -1, (char)0, 0, (char)0);
        }
    }

    //https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Constants.cs
    private const string ActorVfxCreateSig = "40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8";
    private delegate IntPtr ActorVfxCreateDelegate(string path, IntPtr a2, IntPtr a3, float a4, char a5, ushort a6, char a7);
    private readonly ActorVfxCreateDelegate _actorVfxCreate;

    private readonly Hook<ResourceManager.Delegates.GetResourceSync> _getResourceSyncHook;
    private readonly Hook<Texture.Delegates.InitializeContents> _textureOnLoadHook;

    private unsafe ResourceHandle* GetResourceSyncDetour(ResourceManager* thisPtr, ResourceCategory* category, uint* type, uint* hash, CStringPointer path, void* unknown, void* unkDebugPtr, uint unkDebugInt)
    {
        if (path.ToString().Contains("chara/monster/m7002/obj/body/b0001/vfx/texture/alphachannelscreentex"))
        {
            _texCase = 1;
        }
        else if (path.ToString().Contains("chara/monster/m7002/obj/body/b0001/vfx/texture/snesscreentex"))
        {
            _texCase = 2;
        }

        if (_texCase > 0)
        {
            _textureOnLoadHook.Enable(); //Enable Texturehook only for the duration of the specific Resource Load, as hooking Textures from Kernel is unsafe and expensive
            ResourceHandle* ret = _getResourceSyncHook.Original(thisPtr, category, type, hash, path, unknown, unkDebugPtr, unkDebugInt);
            _textureOnLoadHook.Disable();
            _texCase = 0;
            return ret;
        }

        return _getResourceSyncHook.Original(thisPtr, category, type, hash, path, unknown, unkDebugPtr, unkDebugInt);
    }

    private readonly Lock _screenTextureLock = new();
    private int _texCase;

    private unsafe bool TexOnLoadDetour(Texture* thisPtr, void* contents)
    {
        try
        {
            if (thisPtr == null)
            {
                return _textureOnLoadHook.Original(thisPtr, contents);
            }

            uint w, h;
            try
            {
                w = thisPtr->ActualWidth;
                h = thisPtr->ActualHeight;
            }
            catch { return _textureOnLoadHook.Original(thisPtr, contents); }

            if (w != ScreenWidth || h != ScreenHeight)
            {
                return _textureOnLoadHook.Original(thisPtr, contents);
            }

            bool tex = _textureOnLoadHook.Original(thisPtr, contents);
            if (!tex)
            {
                return tex;
            }

            lock (_screenTextureLock)
            {
                var texture = _texCase == 2 ? _snesScreenTexture : _screenTexture;

                if (texture is not { IsDisposed: false })
                {
                    AepLog.Debug("New Texture detected, but our own texture is disposed, skipping");
                    return tex;
                }

                if (DxHandler.Device is not { IsDisposed: false })
                {
                    return tex;
                }

                nint key = (nint)thisPtr;

                if (_views.TryGetValue(key, out var oldView) && (nint)thisPtr->D3D11Texture2D == texture.NativePointer)
                {
                    AepLog.Debug("New Texture detected, but already hooked, skipping");
                    //Detected view on this
                    return tex;
                }

                AepLog.Debug("New Texture detected, assigning...");

                var newView = new ShaderResourceView(DxHandler.Device, texture,
                    new ShaderResourceViewDescription
                    {
                        Format = texture.Description.Format,
                        Dimension = ShaderResourceViewDimension.Texture2D,
                        Texture2D = { MipLevels = texture.Description.MipLevels },
                    });

                _views[key] = newView;

                Marshal.AddRef(texture.NativePointer);
                Marshal.AddRef(newView.NativePointer);

                thisPtr->D3D11Texture2D = (void*)texture.NativePointer;
                thisPtr->D3D11ShaderResourceView = (void*)newView.NativePointer;
            }

            return tex;
        }
        catch (Exception ex)
        {
            AepLog.Error(ex.ToString());
            return false;
        }
    }

    public void Dispose()
    {
        PenumbraIPC.Dispose();

        _mpvRenderer?.Dispose();
        _snesRenderer?.Dispose();

        _textureOnLoadHook.Disable();
        _textureOnLoadHook.Dispose();
        _getResourceSyncHook.Dispose();

        //Do not clean up Texture2D and ShaderResourceView as they may still be part of the currently running VFX
        //Instead just let it stay in the game until it eventually closes, its not growing anyway
        _views.Clear();

        WindowKeyUpReader.Dispose();
        Resources.Dispose();
    }
}
