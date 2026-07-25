using System.Runtime.InteropServices;

namespace Aetherphone.Core.Video;

internal enum VideoPlaybackState : byte
{
    Idle,
    Loading,
    Playing,
    Paused,
    Failed,
}

internal sealed class VideoPlayer : IDisposable
{
    private const string Dll = "libmpv-2";

    private const int Width = 1920;
    private const int Height = 1080;

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern nint mpv_create();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_initialize(nint ctx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_set_option_string(nint ctx, string name, string data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_command(nint ctx, string[] args);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_render_context_create(ref nint res, nint ctx, nint parms);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_render_context_render(nint ctx, nint parms);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern void mpv_render_context_free(nint ctx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern void mpv_render_context_set_update_callback(nint ctx, MpvRenderUpdateFn callback, nint callbackCtx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern ulong mpv_render_context_update(nint ctx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern nint mpv_wait_event(nint ctx, double timeout);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_request_log_messages(nint ctx, string minLevel);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern void mpv_terminate_destroy(nint ctx);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_get_property(nint ctx, string name, int format, out double data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int mpv_get_property(nint ctx, string name, int format, nint data);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern void mpv_free(nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct MpvRenderParam
    {
        public int Type;
        public nint Data;
    }

    private delegate void MpvRenderUpdateFn(nint callbackCtx);

    private nint mpvCtx;
    private nint renderCtx;
    private nint bufferPtr;
    private nint sizePtr;
    private nint stridePtr;
    private nint formatPtr;
    private nint renderParamsPtr;
    private readonly int frameBytes = Width * Height * 4;
    private readonly ManualResetEventSlim frameReady = new(false);
    private readonly Lock gate = new();
    private readonly object frameLock = new();
    private byte[]? latestFrame;
    private MpvRenderUpdateFn? updateCallback;
    private Thread? renderThread;
    private Thread? eventThread;
    private CancellationTokenSource? cancellation;
    private CancellationTokenSource? resolveCancellation;
    private volatile bool closed = true;
    private readonly VideoUrlResolver resolver = new();
    private int pendingVolume = 60;

    public VideoPlaybackState State { get; private set; } = VideoPlaybackState.Idle;
    public string? LastError { get; private set; }

    // Not measured on this project's Wine/RADV setup - mpv has no GPU render path here either
    // way (see docs/video-pipeline.md in the AlphaChannel repo), only decode could benefit, and
    // that hasn't been benchmarked. Off is the safe default; read fresh at Play() time so a
    // settings change takes effect on the next video, not the current one.
    public bool HardwareDecoding { get; set; }

    // Off by default, Wine-only, and only reachable when the config setting is also on - see
    // Configuration.VideoAllowInsecureDirectUrls for why. Never applies on real Windows.
    public bool AllowInsecureDirectUrls { get; set; }

    public int MaxQualityHeight { get; set; } = 720;

    public void SetVolume(int volumePercent)
    {
        pendingVolume = Math.Clamp(volumePercent, 0, 100);
        if (closed)
        {
            return;
        }

        lock (gate)
        {
            _ = mpv_command(mpvCtx,
                ["set", "volume", pendingVolume.ToString(System.Globalization.CultureInfo.InvariantCulture), null!]);
        }
    }

    // True once mpv has nothing left to play (natural end, with keep-open=yes so it doesn't
    // reset position) or before anything has ever been loaded. Callers polling this for
    // auto-advance should throttle - see AetherStreamQueue, which does not poll every frame.
    public bool IsIdle()
    {
        if (closed)
        {
            return true;
        }

        lock (gate)
        {
            if (mpvCtx == nint.Zero)
            {
                return true;
            }

            var ptr = Marshal.AllocHGlobal(4);
            try
            {
                _ = mpv_get_property(mpvCtx, "idle-active", 3, ptr);
                return Marshal.ReadInt32(ptr) == 1;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    public void Play(string url)
    {
        Stop();

        if (VideoUrlResolver.IsYouTubeUrl(url))
        {
            State = VideoPlaybackState.Loading;
            LastError = null;
            resolveCancellation = new CancellationTokenSource();
            _ = ResolveAndPlayAsync(url, resolveCancellation.Token);
            return;
        }

        // A local file never needs page extraction - only remote page URLs (non-YouTube sites)
        // do. Leaving ytdl_hook enabled for a local path risked mpv handing it to yt-dlp to
        // "extract from" instead of just opening it, the same class of bug the useYtdl scoping
        // above already exists to avoid for resolved YouTube CDN URLs.
        PlayDirect(url, useYtdl: !File.Exists(url));
    }

    private async Task ResolveAndPlayAsync(string url, CancellationToken token)
    {
        var (stream, error) = await resolver.ResolveAsync(url, MaxQualityHeight, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (stream is null)
        {
            State = VideoPlaybackState.Failed;
            LastError = error;
            if (error is not null)
            {
                AepLog.Warning($"[Video] {error}");
            }

            return;
        }

        // useYtdl: false - this URL is already a resolved, signed CDN stream URL from
        // YoutubeExplode, not a page URL yt-dlp needs to resolve. Best-effort diagnosis, not
        // confirmed by a log: enabling ytdl_hook unconditionally lined up with when "no volume"
        // started, and a googlevideo.com CDN URL is exactly the kind of thing yt-dlp's own
        // extractor matching could misfire on and re-process instead of passing through. Leaving
        // ytdl_hook enabled only for the raw-URL path (non-YouTube sites, where it's actually
        // needed) removes that risk either way, whether or not this was the real cause - report
        // back if it's still silent and I'll look further.
        PlayDirect(stream.VideoUrl, stream.AudioUrl, useYtdl: false);
    }

    // audioUrl is set when the resolved quality only exists as separate video-only/audio-only
    // streams (anything above what YouTube serves muxed, i.e. above ~720p) - mpv plays them
    // together via an external audio track rather than needing them muxed into one file first.
    private void PlayDirect(string url, string? audioUrl = null, bool useYtdl = false)
    {
        VideoNativeLibrary.EnsureRegistered();

        try
        {
            LastError = null;
            State = VideoPlaybackState.Loading;
            Initialize(useYtdl);

            // audio-file has to be a per-file loadfile option, not a global option set before
            // loadfile - the previous approach (mpv_set_option_string("audio-file", ...) ahead of
            // the loadfile command) silently never opened the external audio URL at all: mpv's own
            // log showed only one "Opening ..." request (the video URL), current-ao stayed
            // "(none)", and playback settled into "audio=eof" immediately. This is the same
            // mechanism mpv's bundled ytdl_hook.lua uses to attach an external audio track.
            // loadfile's actual signature is loadfile <url> <flags> <index> <options> - the
            // options string is the 4th positional argument, not the 3rd; putting it there
            // (skipping <index>) made mpv try to parse it as the integer index and fail the
            // whole command, confirmed via mpv's own error: "The loadfile option must be an
            // integer: audio-file=...".
            var loadArgs = audioUrl is not null
                ? new[] { "loadfile", url, "replace", "0", $"audio-file={audioUrl},aid=1", null! }
                : new[] { "loadfile", url, "replace", null! };
            _ = mpv_command(mpvCtx, loadArgs);
            closed = false;

            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            renderThread = new Thread(() => RenderLoop(token)) { IsBackground = true, Name = "Aetherphone.Video.Render" };
            renderThread.Start();
            eventThread = new Thread(() => EventLoop(token)) { IsBackground = true, Name = "Aetherphone.Video.Events" };
            eventThread.Start();
        }
        catch (Exception exception)
        {
            State = VideoPlaybackState.Failed;
            LastError = exception.Message;
            AepLog.Warning($"[Video] Failed to start playback: {exception.Message}");
            Stop();
        }
    }

    public void Pause(bool pause)
    {
        if (closed)
        {
            return;
        }

        lock (gate)
        {
            _ = mpv_command(mpvCtx, ["set", "pause", pause ? "yes" : "no", null!]);
        }

        State = pause ? VideoPlaybackState.Paused : VideoPlaybackState.Playing;
    }

    public void Seek(float seconds)
    {
        if (closed)
        {
            return;
        }

        lock (gate)
        {
            _ = mpv_command(mpvCtx,
                ["seek", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "absolute", null!]);
        }
    }

    public (float Position, float Duration, bool Paused) GetProgress()
    {
        if (closed)
        {
            return (0f, 0f, true);
        }

        lock (gate)
        {
            if (mpvCtx == nint.Zero)
            {
                return (0f, 0f, true);
            }

            _ = mpv_get_property(mpvCtx, "time-pos", 5, out double position);
            _ = mpv_get_property(mpvCtx, "duration", 5, out double duration);
            var pausedPtr = Marshal.AllocHGlobal(4);
            bool paused;
            try
            {
                _ = mpv_get_property(mpvCtx, "pause", 3, pausedPtr);
                paused = Marshal.ReadInt32(pausedPtr) == 1;
            }
            finally
            {
                Marshal.FreeHGlobal(pausedPtr);
            }

            return ((float)position, (float)duration, paused);
        }
    }

    public byte[]? TryGetFrame(out int width, out int height)
    {
        width = Width;
        height = Height;
        lock (frameLock)
        {
            return latestFrame;
        }
    }

    public void Stop()
    {
        resolveCancellation?.Cancel();
        resolveCancellation?.Dispose();
        resolveCancellation = null;

        if (closed)
        {
            State = VideoPlaybackState.Idle;
            return;
        }

        closed = true;
        cancellation?.Cancel();
        frameReady.Set();
        renderThread?.Join(TimeSpan.FromSeconds(2));
        eventThread?.Join(TimeSpan.FromSeconds(2));
        renderThread = null;
        eventThread = null;
        cancellation?.Dispose();
        cancellation = null;

        lock (gate)
        {
            if (renderCtx != nint.Zero)
            {
                mpv_render_context_free(renderCtx);
                renderCtx = nint.Zero;
            }

            if (mpvCtx != nint.Zero)
            {
                mpv_terminate_destroy(mpvCtx);
                mpvCtx = nint.Zero;
            }

            FreeUnmanaged();
        }

        lock (frameLock)
        {
            latestFrame = null;
        }

        State = VideoPlaybackState.Idle;
    }

    private void Initialize(bool useYtdl = false)
    {
        bufferPtr = Marshal.AllocHGlobal(frameBytes);

        mpvCtx = mpv_create();
        if (mpvCtx == nint.Zero)
        {
            throw new InvalidOperationException("mpv_create failed.");
        }

        _ = mpv_set_option_string(mpvCtx, "vo", "libmpv");
        _ = mpv_set_option_string(mpvCtx, "hwdec", HardwareDecoding ? "auto-safe" : "no");
        _ = mpv_set_option_string(mpvCtx, "profile", "sw-fast");

        // No audio output driver was ever set explicitly - mpv was picking its own default,
        // which under Windows is normally WASAPI. WASAPI through Wine's emulation is a known
        // trouble spot; video keeps working regardless since vo=libmpv's software render path
        // doesn't touch Wine's audio stack at all, only decode+render does, which is why picture
        // was unaffected while sound silently failed. Preferring PulseAudio (which Wine on Linux
        // generally bridges more reliably) with explicit fallbacks, instead of mpv's own default
        // pick, which never surfaced a clear failure for this to have been caught on sooner.
        _ = mpv_set_option_string(mpvCtx, "ao", "pulse,alsa,wasapi,");

        // Only for the raw-URL path (non-YouTube sites) - YouTube itself is resolved separately
        // via YoutubeExplode (VideoUrlResolver), which already gives quality-aware adaptive
        // stream selection matched to the TV's fixed 1080p texture and hands PlayDirect an
        // already-resolved CDN URL. Enabling ytdl_hook for that resolved-URL path too caused a
        // "no volume" regression - see the comment at the ResolveAndPlayAsync call site.
        if (useYtdl)
        {
            _ = mpv_set_option_string(mpvCtx, "ytdl", "yes");
            var ytdlPath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
                "Native", "yt-dlp.exe");
            _ = mpv_set_option_string(mpvCtx, "script-opts", $"ytdl_hook-ytdl_path={ytdlPath}");
        }
        else
        {
            _ = mpv_set_option_string(mpvCtx, "ytdl", "no");
        }

        _ = mpv_set_option_string(mpvCtx, "terminal", "yes");
        // ao=v specifically, not "all=v" - the warn-level run showed mpv reports zero ao errors
        // even with no audible sound, so the failure (if any) is at the info/verbose tier mpv
        // reserves for driver selection/open banners ("AO: [pulse] ..."), which "all=warn" was
        // filtering out before it ever reached mpv_request_log_messages. Keeping everything else
        // at warn avoids flooding the log with decode-path chatter unrelated to audio.
        _ = mpv_set_option_string(mpvCtx, "msg-level", "all=warn,ao=v,ffmpeg=error");
        _ = mpv_set_option_string(mpvCtx, "idle", "yes");
        _ = mpv_set_option_string(mpvCtx, "keep-open", "yes");
        _ = mpv_set_option_string(mpvCtx, "volume",
            pendingVolume.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Wine's own certificate store is essentially empty by default, and mpv's bundled curl
        // build appears to use Schannel (Windows' native TLS) rather than OpenSSL - pointing it
        // at a CA bundle file (tls-ca-file, CURL_CA_BUNDLE, SSL_CERT_FILE) was tested directly
        // against this project's Wine setup and did not fix it; only disabling verification did.
        // Never applies on real Windows, and only when the user has explicitly opted in.
        if (WineEnvironment.IsWine && AllowInsecureDirectUrls)
        {
            _ = mpv_set_option_string(mpvCtx, "tls-verify", "no");
        }
        var initResult = mpv_initialize(mpvCtx);
        if (initResult < 0)
        {
            throw new InvalidOperationException($"mpv_initialize failed: {initResult}");
        }

        // terminal=yes only makes mpv print to its own process's stdout, which for a DLL
        // embedded via libmpv (no console attached) goes nowhere Dalamud can see - none of mpv's
        // own [ao]/[demux]/[ffmpeg] diagnostics were ever reaching dalamud.log. Subscribing to
        // MPV_EVENT_LOG_MESSAGE and forwarding it through AepLog (see EventLoop) is the only way
        // to actually capture that output.
        _ = mpv_request_log_messages(mpvCtx, "v");

        var apiTypePtr = Marshal.StringToHGlobalAnsi("sw");
        var paramsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MpvRenderParam>() * 2);
        try
        {
            Marshal.StructureToPtr(new MpvRenderParam { Type = 1, Data = apiTypePtr }, paramsPtr, false);
            Marshal.StructureToPtr(new MpvRenderParam { Type = 0, Data = nint.Zero },
                paramsPtr + Marshal.SizeOf<MpvRenderParam>(), false);

            var createResult = mpv_render_context_create(ref renderCtx, mpvCtx, paramsPtr);
            if (createResult < 0)
            {
                throw new InvalidOperationException($"mpv_render_context_create failed: {createResult}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(apiTypePtr);
            Marshal.FreeHGlobal(paramsPtr);
        }

        sizePtr = Marshal.AllocHGlobal(8);
        Marshal.WriteInt32(sizePtr, Width);
        Marshal.WriteInt32(sizePtr + 4, Height);

        stridePtr = Marshal.AllocHGlobal(nint.Size);
        Marshal.WriteIntPtr(stridePtr, new nint(Width * 4));

        formatPtr = Marshal.StringToHGlobalAnsi("bgra");

        var paramSize = Marshal.SizeOf<MpvRenderParam>();
        renderParamsPtr = Marshal.AllocHGlobal(paramSize * 5);
        Marshal.StructureToPtr(new MpvRenderParam { Type = 17, Data = sizePtr }, renderParamsPtr, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = 18, Data = formatPtr }, renderParamsPtr + paramSize, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = 19, Data = stridePtr }, renderParamsPtr + paramSize * 2, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = 20, Data = bufferPtr }, renderParamsPtr + paramSize * 3, false);
        Marshal.StructureToPtr(new MpvRenderParam { Type = 0, Data = nint.Zero }, renderParamsPtr + paramSize * 4, false);

        updateCallback = _ => frameReady.Set();
        mpv_render_context_set_update_callback(renderCtx, updateCallback, nint.Zero);
    }

    private void RenderLoop(CancellationToken token)
    {
        while (!closed && !token.IsCancellationRequested)
        {
            try
            {
                frameReady.Wait(token);
                frameReady.Reset();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (closed || token.IsCancellationRequested)
            {
                return;
            }

            var flags = mpv_render_context_update(renderCtx);
            if ((flags & 1) == 0)
            {
                continue;
            }

            var rc = mpv_render_context_render(renderCtx, renderParamsPtr);
            if (rc != 0 || closed || token.IsCancellationRequested)
            {
                continue;
            }

            var frame = new byte[frameBytes];
            Marshal.Copy(bufferPtr, frame, 0, frameBytes);
            lock (frameLock)
            {
                latestFrame = frame;
            }

            State = VideoPlaybackState.Playing;
        }
    }

    private void EventLoop(CancellationToken token)
    {
        while (!closed && !token.IsCancellationRequested)
        {
            var ev = mpv_wait_event(mpvCtx, 1);
            if (ev == nint.Zero)
            {
                continue;
            }

            var eventId = Marshal.ReadInt32(ev);
            if (eventId == 1) // MPV_EVENT_SHUTDOWN
            {
                return;
            }

            if (eventId == 2) // MPV_EVENT_LOG_MESSAGE
            {
                LogMpvMessage(Marshal.ReadIntPtr(ev, 16));
            }

            if (eventId == 8) // MPV_EVENT_FILE_LOADED
            {
                LogAudioDiagnostics();
            }
        }
    }

    // Fired once per load, right when mpv has settled on tracks/output - current-ao being empty
    // or "null" here (rather than an explicit ao failure elsewhere in the log) would mean no
    // audio device ever opened at all, distinct from "opened but produced no sound".
    private void LogAudioDiagnostics()
    {
        var ao = GetStringProperty("current-ao") ?? "(none)";
        var aid = GetStringProperty("aid") ?? "(none)";

        var mutedPtr = Marshal.AllocHGlobal(4);
        bool muted;
        try
        {
            _ = mpv_get_property(mpvCtx, "mute", 3, mutedPtr);
            muted = Marshal.ReadInt32(mutedPtr) == 1;
        }
        finally
        {
            Marshal.FreeHGlobal(mutedPtr);
        }

        _ = mpv_get_property(mpvCtx, "volume", 5, out double volume);
        AepLog.Warning($"[Video][diag] current-ao={ao} aid={aid} mute={muted} volume={volume}");
    }

    private string? GetStringProperty(string name)
    {
        var ptr = Marshal.AllocHGlobal(nint.Size);
        try
        {
            var result = mpv_get_property(mpvCtx, name, 1, ptr); // MPV_FORMAT_STRING
            if (result < 0)
            {
                return null;
            }

            var strPtr = Marshal.ReadIntPtr(ptr);
            if (strPtr == nint.Zero)
            {
                return null;
            }

            var value = Marshal.PtrToStringAnsi(strPtr);
            mpv_free(strPtr);
            return value;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    // mpv_event layout on 64-bit: int event_id, int error, ulong reply_userdata, void* data -
    // data starts at offset 16. mpv_event_log_message layout: char* prefix, char* level,
    // char* text, mpv_log_level log_level.
    private static void LogMpvMessage(nint dataPtr)
    {
        if (dataPtr == nint.Zero)
        {
            return;
        }

        var prefixPtr = Marshal.ReadIntPtr(dataPtr, 0);
        var levelPtr = Marshal.ReadIntPtr(dataPtr, 8);
        var textPtr = Marshal.ReadIntPtr(dataPtr, 16);
        var prefix = prefixPtr == nint.Zero ? string.Empty : Marshal.PtrToStringAnsi(prefixPtr) ?? string.Empty;
        var level = levelPtr == nint.Zero ? string.Empty : Marshal.PtrToStringAnsi(levelPtr) ?? string.Empty;
        var text = textPtr == nint.Zero ? string.Empty : Marshal.PtrToStringAnsi(textPtr) ?? string.Empty;
        AepLog.Warning($"[Video][mpv/{prefix}/{level}] {text.TrimEnd()}");
    }

    private void FreeUnmanaged()
    {
        if (bufferPtr != nint.Zero)
        {
            Marshal.FreeHGlobal(bufferPtr);
            bufferPtr = nint.Zero;
        }

        if (sizePtr != nint.Zero)
        {
            Marshal.FreeHGlobal(sizePtr);
            sizePtr = nint.Zero;
        }

        if (stridePtr != nint.Zero)
        {
            Marshal.FreeHGlobal(stridePtr);
            stridePtr = nint.Zero;
        }

        if (formatPtr != nint.Zero)
        {
            Marshal.FreeHGlobal(formatPtr);
            formatPtr = nint.Zero;
        }

        if (renderParamsPtr != nint.Zero)
        {
            Marshal.FreeHGlobal(renderParamsPtr);
            renderParamsPtr = nint.Zero;
        }
    }

    public void Dispose()
    {
        Stop();
        frameReady.Dispose();
    }
}
