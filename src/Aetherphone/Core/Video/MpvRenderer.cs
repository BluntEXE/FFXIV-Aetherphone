using System.Globalization;
using System.Runtime.InteropServices;
using SharpDX.Direct3D11;

namespace Aetherphone.Core.Video;

internal enum MpvEndReason : byte
{
    Finished,
    Stopped,
    Quit,
    Failed,
    Redirect,
}

internal sealed class MpvRenderer : IDisposable
{
    private const string Library = "libmpv-2";
    private const string RenderKey = "mpv";

    private const int FormatFlag = 3;
    private const int FormatDouble = 5;

    private const int EventShutdown = 1;
    private const int EventLogMessage = 2;
    private const int EventStartFile = 6;
    private const int EventEndFile = 7;
    private const int EventFileLoaded = 8;
    private const int EventPlaybackRestart = 21;

    private const int ParamInvalid = 0;
    private const int ParamApiType = 1;
    private const int ParamSoftwareSize = 17;
    private const int ParamSoftwareFormat = 18;
    private const int ParamSoftwareStride = 19;
    private const int ParamSoftwarePointer = 20;

    private const int ParamStride = 16;
    private const int MaxConsecutiveRenderFailures = 30;
    private const int RenderWaitMilliseconds = 250;
    private const double EventWaitSeconds = 0.1;

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_create();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_initialize(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_set_option_string(IntPtr ctx, string name, string data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_command(IntPtr ctx, string?[] args);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_render_context_create(ref IntPtr result, IntPtr ctx, IntPtr parameters);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_render_context_render(IntPtr ctx, IntPtr parameters);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_render_context_free(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_render_context_set_update_callback(IntPtr ctx, MpvRenderUpdateFn callback,
        IntPtr callbackContext);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong mpv_render_context_update(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_request_log_messages(IntPtr ctx, string minimumLevel);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_get_property(IntPtr ctx, string name, int format, out double data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_get_property(IntPtr ctx, string name, int format, out int data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_get_property_string(IntPtr ctx, string name);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_free(IntPtr data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_error_string(int error);

    [StructLayout(LayoutKind.Sequential)]
    private struct MpvRenderParam
    {
        public int Type;
        public IntPtr Data;
    }

    internal delegate void MpvRenderUpdateFn(IntPtr callbackContext);

    private readonly Lock commandLock = new();
    private readonly Lock snapshotLock = new();
    private readonly ManualResetEventSlim frameReady = new(false);

    private IntPtr mpvContext;
    private IntPtr renderContext;
    private IntPtr frameBuffer;
    private IntPtr snapshotA;
    private IntPtr snapshotB;
    private IntPtr latestSnapshot;
    private bool useSnapshotA = true;

    private IntPtr renderParameters;
    private IntPtr sizeParameter;
    private IntPtr strideParameter;
    private IntPtr formatParameter;

    private int frameBytes;
    private int width;
    private int height;

    private Texture2D? targetTexture;
    private MpvRenderUpdateFn? updateCallback;
    private GCHandle updateCallbackHandle;

    private Thread? eventThread;
    private Thread? renderThread;

    private volatile bool disposed;
    private volatile bool started;
    private volatile string? currentUrl;
    private int consecutiveRenderFailures;

    internal event Action? FileLoaded;
    internal event Action<MpvEndReason, string?>? FileEnded;

    internal bool IsRunning => started && !disposed;

    internal void Initialize(int frameWidth, int frameHeight, Texture2D? texture, string? linkResolverPath,
        bool hardwareDecoding, int maxQualityHeight, bool allowInsecureDirectUrls, int initialVolume)
    {
        width = frameWidth;
        height = frameHeight;
        targetTexture = texture;
        frameBytes = frameWidth * frameHeight * 4;

        frameBuffer = Marshal.AllocHGlobal(frameBytes);
        snapshotA = Marshal.AllocHGlobal(frameBytes);
        snapshotB = Marshal.AllocHGlobal(frameBytes);

        mpvContext = mpv_create();
        if (mpvContext == IntPtr.Zero)
        {
            throw new InvalidOperationException("mpv could not be created.");
        }

        SetOption("vo", "libmpv");
        SetOption("hwdec", hardwareDecoding ? "auto-safe" : "no");
        SetOption("profile", "sw-fast");
        SetOption("ytdl", "yes");
        if (linkResolverPath is { Length: > 0 })
        {
            SetOption("script-opts", $"ytdl_hook-ytdl_path={linkResolverPath}");
        }

        SetOption("ytdl-format",
            $"bestvideo[height<={maxQualityHeight}][ext=mp4]+bestaudio/best[height<={maxQualityHeight}]");
        SetOption("ytdl-raw-options", "force-ipv4=,hls-use-mpegts=");
        SetOption("volume", initialVolume.ToString(CultureInfo.InvariantCulture));
        SetOption("msg-level", "all=warn,ffmpeg=error");
        SetOption("terminal", "yes");
        SetOption("idle", "yes");
        SetOption("keep-open", "yes");
        if (WineEnvironment.IsWine && allowInsecureDirectUrls)
        {
            SetOption("tls-verify", "no");
        }

        _ = mpv_request_log_messages(mpvContext, "warn");

        var initialized = mpv_initialize(mpvContext);
        if (initialized < 0)
        {
            throw new InvalidOperationException($"mpv could not start: {ErrorText(initialized)}");
        }

        CreateRenderContext();
        BuildRenderParameters();

        updateCallback = _ => frameReady.Set();
        updateCallbackHandle = GCHandle.Alloc(updateCallback);
        mpv_render_context_set_update_callback(renderContext, updateCallback, IntPtr.Zero);

        started = true;

        eventThread = new Thread(EventLoop) { IsBackground = true, Name = "aetherstream-mpv-events" };
        eventThread.Start();

        renderThread = new Thread(RenderLoop) { IsBackground = true, Name = "aetherstream-mpv-render" };
        renderThread.Start();
    }

    private void SetOption(string name, string value)
    {
        var result = mpv_set_option_string(mpvContext, name, value);
        if (result < 0)
        {
            AepLog.Warning($"[MPV] Option {name}={value} was refused: {ErrorText(result)}");
        }
    }

    private void CreateRenderContext()
    {
        var apiType = Marshal.StringToHGlobalAnsi("sw");
        var parameters = Marshal.AllocHGlobal(ParamStride * 2);
        try
        {
            Marshal.StructureToPtr(new MpvRenderParam { Type = ParamApiType, Data = apiType }, parameters, false);
            Marshal.StructureToPtr(new MpvRenderParam { Type = ParamInvalid, Data = IntPtr.Zero },
                parameters + ParamStride, false);

            var created = mpv_render_context_create(ref renderContext, mpvContext, parameters);
            if (created < 0)
            {
                throw new InvalidOperationException($"mpv could not start rendering: {ErrorText(created)}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(apiType);
            Marshal.FreeHGlobal(parameters);
        }
    }

    private void BuildRenderParameters()
    {
        sizeParameter = Marshal.AllocHGlobal(8);
        Marshal.WriteInt32(sizeParameter, width);
        Marshal.WriteInt32(sizeParameter + 4, height);

        strideParameter = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(strideParameter, new IntPtr(width * 4));

        formatParameter = Marshal.StringToHGlobalAnsi("bgra");

        renderParameters = Marshal.AllocHGlobal(ParamStride * 5);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamSoftwareSize, Data = sizeParameter },
            renderParameters, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamSoftwareFormat, Data = formatParameter },
            renderParameters + ParamStride, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamSoftwareStride, Data = strideParameter },
            renderParameters + ParamStride * 2, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamSoftwarePointer, Data = frameBuffer },
            renderParameters + ParamStride * 3, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = ParamInvalid, Data = IntPtr.Zero },
            renderParameters + ParamStride * 4, false);
    }

    internal bool Play(string url, double startSeconds, bool playing)
    {
        if (!IsRunning || url.Length == 0)
        {
            return false;
        }

        var start = ((int)Math.Max(0d, startSeconds)).ToString(CultureInfo.InvariantCulture);
        var options = playing ? $"start={start}" : $"start={start},pause=yes";

        lock (commandLock)
        {
            var result = mpv_command(mpvContext, ["loadfile", url, "replace", "0", options, null]);
            if (result < 0)
            {
                AepLog.Warning($"[MPV] Could not load {url}: {ErrorText(result)}");
                return false;
            }
        }

        currentUrl = url;
        Interlocked.Exchange(ref consecutiveRenderFailures, 0);
        return true;
    }

    internal void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        currentUrl = null;
        lock (commandLock)
        {
            _ = mpv_command(mpvContext, ["stop", null]);
        }

        lock (snapshotLock)
        {
            latestSnapshot = IntPtr.Zero;
        }
    }

    internal void Pause(bool paused) => SetProperty("pause", paused ? "yes" : "no");

    internal void SetVolume(int volume) =>
        SetProperty("volume", Math.Clamp(volume, 0, 100).ToString(CultureInfo.InvariantCulture));

    internal void Seek(double seconds)
    {
        if (!IsRunning)
        {
            return;
        }

        var target = Math.Max(0d, seconds).ToString("F3", CultureInfo.InvariantCulture);
        lock (commandLock)
        {
            var result = mpv_command(mpvContext, ["seek", target, "absolute", null]);
            if (result < 0)
            {
                AepLog.Warning($"[MPV] Could not seek to {target}s: {ErrorText(result)}");
            }
        }
    }

    private void SetProperty(string name, string value)
    {
        if (!IsRunning)
        {
            return;
        }

        lock (commandLock)
        {
            _ = mpv_command(mpvContext, ["set", name, value, null]);
        }
    }

    internal MpvPlaybackInfo ReadPlaybackInfo()
    {
        if (!IsRunning)
        {
            return default;
        }

        lock (commandLock)
        {
            if (mpvContext == IntPtr.Zero)
            {
                return default;
            }

            _ = mpv_get_property(mpvContext, "time-pos", FormatDouble, out double position);
            _ = mpv_get_property(mpvContext, "duration", FormatDouble, out double duration);
            _ = mpv_get_property(mpvContext, "pause", FormatFlag, out int paused);
            _ = mpv_get_property(mpvContext, "core-idle", FormatFlag, out int coreIdle);
            _ = mpv_get_property(mpvContext, "seeking", FormatFlag, out int seeking);
            return new MpvPlaybackInfo(position, duration, paused == 1, coreIdle == 1, seeking == 1);
        }
    }

    internal string? ReadMediaTitle() => ReadStringProperty("media-title");

    internal string? CurrentUrl => currentUrl;

    private string? ReadStringProperty(string name)
    {
        if (!IsRunning)
        {
            return null;
        }

        lock (commandLock)
        {
            if (mpvContext == IntPtr.Zero)
            {
                return null;
            }

            var pointer = mpv_get_property_string(mpvContext, name);
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8(pointer);
            }
            finally
            {
                mpv_free(pointer);
            }
        }
    }

    internal byte[]? CopyLatestFrame(out int frameWidth, out int frameHeight)
    {
        frameWidth = width;
        frameHeight = height;
        lock (snapshotLock)
        {
            if (latestSnapshot == IntPtr.Zero || frameBytes == 0)
            {
                return null;
            }

            var frame = new byte[frameBytes];
            Marshal.Copy(latestSnapshot, frame, 0, frameBytes);
            return frame;
        }
    }

    internal bool TryCopyLatestFrame(byte[] destination)
    {
        lock (snapshotLock)
        {
            if (latestSnapshot == IntPtr.Zero || frameBytes == 0 || destination.Length < frameBytes)
            {
                return false;
            }

            Marshal.Copy(latestSnapshot, destination, 0, frameBytes);
            return true;
        }
    }

    internal int FrameVersion => Volatile.Read(ref frameVersion);

    private int frameVersion;

    private void RenderLoop()
    {
        while (!disposed)
        {
            if (!frameReady.Wait(RenderWaitMilliseconds))
            {
                continue;
            }

            frameReady.Reset();
            if (disposed)
            {
                return;
            }

            RenderPendingFrame();
        }
    }

    private void RenderPendingFrame()
    {
        var flags = mpv_render_context_update(renderContext);
        if ((flags & 1) == 0)
        {
            return;
        }

        try
        {
            var result = mpv_render_context_render(renderContext, renderParameters);
            if (disposed)
            {
                return;
            }

            if (result < 0)
            {
                NoteRenderFailure($"render failed: {ErrorText(result)}");
                return;
            }

            var texture = targetTexture;
            if (texture is null)
            {
                return;
            }

            var snapshot = useSnapshotA ? snapshotA : snapshotB;
            useSnapshotA = !useSnapshotA;

            unsafe
            {
                System.Buffer.MemoryCopy((void*)frameBuffer, (void*)snapshot, frameBytes, frameBytes);
            }

            lock (snapshotLock)
            {
                latestSnapshot = snapshot;
            }

            Interlocked.Increment(ref frameVersion);
            Interlocked.Exchange(ref consecutiveRenderFailures, 0);

            var rowPitch = width * 4;
            DxHandler.RunOnRenderThread(RenderKey, () =>
            {
                DxHandler.Device?.ImmediateContext.UpdateSubresource(texture, 0, null, snapshot, rowPitch, 0);
            });
        }
        catch (Exception exception)
        {
            NoteRenderFailure(exception.Message);
        }
    }

    private void NoteRenderFailure(string reason)
    {
        var failures = Interlocked.Increment(ref consecutiveRenderFailures);
        if (failures == 1 || failures % MaxConsecutiveRenderFailures == 0)
        {
            AepLog.Warning($"[MPV] Frame {reason} ({failures} in a row)");
        }
    }

    private void EventLoop()
    {
        try
        {
            while (!disposed)
            {
                var handle = mpv_wait_event(mpvContext, EventWaitSeconds);
                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                var eventId = Marshal.ReadInt32(handle);
                switch (eventId)
                {
                    case EventShutdown:
                        return;
                    case EventLogMessage:
                        HandleLogMessage(handle);
                        break;
                    case EventStartFile:
                        AepLog.Verbose("[MPV] Loading a file");
                        break;
                    case EventFileLoaded:
                        FileLoaded?.Invoke();
                        break;
                    case EventPlaybackRestart:
                        AepLog.Verbose("[MPV] Playback running");
                        break;
                    case EventEndFile:
                        HandleEndFile(handle);
                        break;
                    default:
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[MPV] Event loop stopped: {exception.Message}");
        }
    }

    private void HandleLogMessage(IntPtr handle)
    {
        var data = Marshal.ReadIntPtr(handle + 16);
        if (data == IntPtr.Zero)
        {
            return;
        }

        var prefix = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(data));
        var level = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(data + 8));
        var text = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(data + 16))?.Trim();
        if (text is null || text.Length == 0)
        {
            return;
        }

        if (level is "error" or "fatal")
        {
            AepLog.Warning($"[MPV/{prefix}] {text}");
            return;
        }

        AepLog.Verbose($"[MPV/{prefix}/{level}] {text}");
    }

    private void HandleEndFile(IntPtr handle)
    {
        var data = Marshal.ReadIntPtr(handle + 16);
        var reasonCode = 0;
        var errorCode = 0;
        if (data != IntPtr.Zero)
        {
            reasonCode = Marshal.ReadInt32(data);
            errorCode = Marshal.ReadInt32(data + 4);
        }

        var reason = reasonCode switch
        {
            2 => MpvEndReason.Stopped,
            3 => MpvEndReason.Quit,
            4 => MpvEndReason.Failed,
            5 => MpvEndReason.Redirect,
            _ => MpvEndReason.Finished,
        };

        var detail = reason == MpvEndReason.Failed ? ErrorText(errorCode) : null;
        if (reason == MpvEndReason.Failed)
        {
            AepLog.Warning($"[MPV] Playback failed: {detail}");
        }

        FileEnded?.Invoke(reason, detail);
    }

    private static string ErrorText(int error)
    {
        try
        {
            var pointer = mpv_error_string(error);
            return pointer == IntPtr.Zero
                ? error.ToString(CultureInfo.InvariantCulture)
                : Marshal.PtrToStringAnsi(pointer) ?? error.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return error.ToString(CultureInfo.InvariantCulture);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        started = false;
        currentUrl = null;
        frameReady.Set();
        DxHandler.CancelRenderThreadWork(RenderKey);

        renderThread?.Join(2000);
        eventThread?.Join(2000);

        lock (snapshotLock)
        {
            latestSnapshot = IntPtr.Zero;
        }

        if (renderContext != IntPtr.Zero)
        {
            mpv_render_context_free(renderContext);
            renderContext = IntPtr.Zero;
        }

        if (updateCallbackHandle.IsAllocated)
        {
            updateCallbackHandle.Free();
        }

        if (mpvContext != IntPtr.Zero)
        {
            mpv_terminate_destroy(mpvContext);
            mpvContext = IntPtr.Zero;
        }

        FreeUnmanaged(ref frameBuffer);
        FreeUnmanaged(ref snapshotA);
        FreeUnmanaged(ref snapshotB);
        FreeUnmanaged(ref sizeParameter);
        FreeUnmanaged(ref strideParameter);
        FreeUnmanaged(ref formatParameter);
        FreeUnmanaged(ref renderParameters);

        targetTexture = null;
        frameReady.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void FreeUnmanaged(ref IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeHGlobal(pointer);
        pointer = IntPtr.Zero;
    }
}

internal readonly record struct MpvPlaybackInfo(
    double PositionSeconds,
    double DurationSeconds,
    bool Paused,
    bool CoreIdle,
    bool Seeking);
