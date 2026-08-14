using System.Collections.Concurrent;

namespace Aetherphone.Core.Video;

internal enum VideoPlaybackState : byte
{
    Idle,
    Loading,
    Playing,
    Paused,
    Failed,
}

internal readonly record struct PlaybackProgress(float Position, float Duration, bool Paused)
{
    internal float Fraction => Duration > 0f ? Math.Clamp(Position / Duration, 0f, 1f) : 0f;
}

internal sealed class VideoPlayer : IDisposable
{
    private readonly VideoEngine engine;
    private readonly ConcurrentQueue<(MpvEndReason Reason, string? Detail)> pendingEndings = new();
    private volatile bool pendingLoaded;

    internal VideoPlayer(VideoEngine engine)
    {
        this.engine = engine;
        engine.PlaybackEnded += OnEngineEnded;
        engine.PlaybackLoaded += OnEngineLoaded;
    }

    internal event Action? Finished;
    internal event Action<string?>? Failed;

    internal VideoPlaybackState State { get; private set; } = VideoPlaybackState.Idle;
    internal PlaybackProgress Progress { get; private set; }
    internal string? LastError { get; private set; }
    internal string? RecoveryNotice => engine.RecoveryNotice;

    internal bool HasMedia => State is VideoPlaybackState.Loading or VideoPlaybackState.Playing
        or VideoPlaybackState.Paused;

    internal bool HardwareDecoding
    {
        get => engine.HardwareDecoding;
        set => engine.HardwareDecoding = value;
    }

    internal bool AllowInsecureDirectUrls
    {
        get => engine.AllowInsecureDirectUrls;
        set => engine.AllowInsecureDirectUrls = value;
    }

    internal int MaxQualityHeight
    {
        get => engine.MaxQualityHeight;
        set => engine.MaxQualityHeight = value;
    }

    internal void SetVolume(int volumePercent) => engine.SetVolume(volumePercent);

    internal void Play(string url, double startSeconds = 0d, bool playing = true)
    {
        LastError = null;
        State = VideoPlaybackState.Loading;
        engine.ClearError();
        engine.Play(url, startSeconds, playing);
    }

    internal void Pause(bool paused)
    {
        engine.Pause(paused);
        if (State is VideoPlaybackState.Playing or VideoPlaybackState.Paused)
        {
            State = paused ? VideoPlaybackState.Paused : VideoPlaybackState.Playing;
        }
    }

    internal void Seek(double seconds) => engine.Seek(seconds);

    internal void Stop()
    {
        engine.StopVideo();
        State = VideoPlaybackState.Idle;
        Progress = default;
        while (pendingEndings.TryDequeue(out _))
        {
        }
    }

    internal byte[]? TryGetFrame(out int width, out int height) => engine.TryGetFrame(out width, out height);

    internal bool TryCopyLatestFrame(byte[] destination) => engine.TryCopyLatestFrame(destination);

    internal int FrameVersion => engine.FrameVersion;

    internal void OnFrameworkUpdate()
    {
        if (pendingLoaded)
        {
            pendingLoaded = false;
            if (State == VideoPlaybackState.Loading)
            {
                State = VideoPlaybackState.Playing;
            }
        }

        while (pendingEndings.TryDequeue(out var ending))
        {
            ApplyEnding(ending.Reason, ending.Detail);
        }

        if (!HasMedia)
        {
            Progress = default;
            return;
        }

        var info = engine.ReadPlaybackInfo();
        Progress = new PlaybackProgress((float)info.PositionSeconds, (float)info.DurationSeconds, info.Paused);

        if (State == VideoPlaybackState.Playing && info.Paused)
        {
            State = VideoPlaybackState.Paused;
            return;
        }

        if (State == VideoPlaybackState.Paused && !info.Paused)
        {
            State = VideoPlaybackState.Playing;
        }
    }

    private void ApplyEnding(MpvEndReason reason, string? detail)
    {
        switch (reason)
        {
            case MpvEndReason.Finished:
                State = VideoPlaybackState.Idle;
                Progress = default;
                Finished?.Invoke();
                return;
            case MpvEndReason.Failed:
                State = VideoPlaybackState.Failed;
                LastError = detail ?? engine.LastError ?? "This video could not be played.";
                Progress = default;
                Failed?.Invoke(LastError);
                return;
            case MpvEndReason.Redirect:
                return;
            default:
                State = VideoPlaybackState.Idle;
                Progress = default;
                return;
        }
    }

    private void OnEngineLoaded() => pendingLoaded = true;

    private void OnEngineEnded(MpvEndReason reason, string? detail) => pendingEndings.Enqueue((reason, detail));

    public void Dispose()
    {
        engine.PlaybackEnded -= OnEngineEnded;
        engine.PlaybackLoaded -= OnEngineLoaded;
        engine.Shutdown();
        State = VideoPlaybackState.Idle;
    }
}
