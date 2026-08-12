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
    private readonly VideoEngine engine;

    public VideoPlayer(VideoEngine engine)
    {
        this.engine = engine;
    }

    public VideoPlaybackState State { get; private set; } = VideoPlaybackState.Idle;
    public string? LastError { get; private set; }

    public bool HardwareDecoding
    {
        get => engine.HardwareDecoding;
        set => engine.HardwareDecoding = value;
    }

    public bool AllowInsecureDirectUrls
    {
        get => engine.AllowInsecureDirectUrls;
        set => engine.AllowInsecureDirectUrls = value;
    }

    public int MaxQualityHeight
    {
        get => engine.MaxQualityHeight;
        set => engine.MaxQualityHeight = value;
    }

    public void SetVolume(int volumePercent) => engine.SetVolume(volumePercent);

    public bool IsIdle() => engine.GetIdle();

    public void Play(string url)
    {
        try
        {
            LastError = null;
            State = VideoPlaybackState.Loading;
            engine.PlayVideo(url);
            State = VideoPlaybackState.Playing;
        }
        catch (Exception exception)
        {
            State = VideoPlaybackState.Failed;
            LastError = exception.Message;
            AepLog.Warning($"[Video] Failed to start playback: {exception.Message}");
        }
    }

    public void Pause(bool pause)
    {
        engine.Pause(pause);
        State = pause ? VideoPlaybackState.Paused : VideoPlaybackState.Playing;
    }

    public void Seek(float seconds) => engine.Seek((int)MathF.Round(seconds));

    public (float Position, float Duration, bool Paused) GetProgress()
    {
        if (State != VideoPlaybackState.Idle && engine.LastError is { } error && LastError != error)
        {
            State = VideoPlaybackState.Failed;
            LastError = error;
        }

        var info = engine.GetInfo();
        return ((float)info[0], (float)info[1], engine.GetPaused());
    }

    public byte[]? TryGetFrame(out int width, out int height) => engine.TryGetFrame(out width, out height);

    public void Stop()
    {
        engine.StopVideo();
        State = VideoPlaybackState.Idle;
    }

    public void Dispose()
    {
        Stop();
    }
}
