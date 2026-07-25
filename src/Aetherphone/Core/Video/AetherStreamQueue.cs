namespace Aetherphone.Core.Video;

// Title/Source/Duration/ThumbnailUrl start out as just the raw URL and get filled in
// asynchronously - see AetherStreamQueue's enrichment step. Mutable (not a record) precisely so
// that fill-in can update the same instance already sitting in the queue/UI rather than needing
// every caller to track down and replace it after a reorder. Reference equality (the class
// default) is also the correct behavior for List.Remove here, unlike a record's value equality,
// which could match the wrong entry if two queued items happen to share every field.
internal sealed class VideoQueueEntry
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Url { get; }
    public string Title { get; set; }
    public string Source { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? ThumbnailUrl { get; set; }

    public VideoQueueEntry(string url, string title, string source, TimeSpan? duration, string? thumbnailUrl)
    {
        Url = url;
        Title = title;
        Source = source;
        Duration = duration;
        ThumbnailUrl = thumbnailUrl;
    }
}

// Aetherphone's own queue - VideoPlayer has no knowledge this exists. One item plays at a time;
// the next is pushed when VideoPlayer.IsIdle() reports the current one has naturally ended.
// Stage 0 established that end-of-playback is only observable by polling mpv's idle-active
// property (docs/video-pipeline.md in the AlphaChannel repo, §"when is state published" for the
// equivalent finding on the reference implementation) - there's no event to subscribe to, so
// this polls, but on a tick-throttled interval, never every frame.
internal sealed class AetherStreamQueue
{
    private const int PollEveryTicks = 30; // roughly twice a second at 60fps, not per-frame

    private readonly VideoPlayer video;
    private readonly ScreenController screen;
    private readonly VideoUrlResolver metadataResolver = new();
    private readonly List<VideoQueueEntry> entries = new();
    private int tickCounter;
    private bool wasIdle = true;
    private bool autoAdvanceArmed;

    public AetherStreamQueue(VideoPlayer video, ScreenController screen)
    {
        this.video = video;
        this.screen = screen;
    }

    public IReadOnlyList<VideoQueueEntry> Entries => entries;
    public VideoQueueEntry? Current { get; private set; }

    public void Add(VideoQueueEntry entry)
    {
        entries.Add(entry);
        EnrichIfYouTube(entry);
    }

    public void PlayNow(VideoQueueEntry entry)
    {
        entries.Remove(entry);
        entries.Insert(0, entry);
        EnrichIfYouTube(entry);
        Advance();
    }

    public void PlayNext(VideoQueueEntry entry)
    {
        entries.Remove(entry);
        var insertAt = Current is null ? 0 : Math.Min(1, entries.Count);
        entries.Insert(insertAt, entry);
        EnrichIfYouTube(entry);
    }

    public void Remove(VideoQueueEntry entry) => entries.Remove(entry);

    public void Clear()
    {
        entries.Clear();
        Current = null;
        video.Stop();
        screen.ClearActive();
    }

    public void Reorder(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= entries.Count || toIndex < 0 || toIndex >= entries.Count ||
            fromIndex == toIndex)
        {
            return;
        }

        var item = entries[fromIndex];
        entries.RemoveAt(fromIndex);
        entries.Insert(toIndex, item);
    }

    // Plays the next queued entry now, whether or not something was already playing - used both
    // for the user's own "skip" action and for auto-advance on natural end.
    public void Advance()
    {
        if (entries.Count == 0)
        {
            Current = null;
            video.Stop();
            screen.ClearActive();
            return;
        }

        Current = entries[0];
        entries.RemoveAt(0);
        autoAdvanceArmed = false;
        EnrichIfYouTube(Current);
        video.Play(Current.Url);

        // Marks the local player's own companion as the one to trigger the screen VFX on -
        // without this, video decodes and reaches the screen texture fine (Plugin's own
        // framework tick pushes frames unconditionally), but the screen itself never actually
        // appears, since ScreenController only invokes the VFX for whichever entity is "active".
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is not null)
        {
            screen.SetActive(localPlayer.EntityId);
        }
    }

    private void EnrichIfYouTube(VideoQueueEntry entry)
    {
        if (!VideoUrlResolver.IsYouTubeUrl(entry.Url))
        {
            return;
        }

        _ = EnrichAsync(entry);
    }

    private async Task EnrichAsync(VideoQueueEntry entry)
    {
        var metadata = await metadataResolver.ResolveMetadataAsync(entry.Url, CancellationToken.None)
            .ConfigureAwait(false);
        if (metadata is null)
        {
            return;
        }

        entry.Title = metadata.Title;
        entry.Source = metadata.Source;
        entry.Duration = metadata.Duration;
        entry.ThumbnailUrl = metadata.ThumbnailUrl;
    }

    public void OnFrameworkUpdate()
    {
        tickCounter++;
        if (tickCounter < PollEveryTicks)
        {
            return;
        }

        tickCounter = 0;

        if (video.State == VideoPlaybackState.Playing)
        {
            autoAdvanceArmed = true;
        }

        var idle = video.IsIdle();
        if (idle && !wasIdle && autoAdvanceArmed && Current is not null)
        {
            autoAdvanceArmed = false;
            Advance();
        }

        wasIdle = idle;
    }
}
