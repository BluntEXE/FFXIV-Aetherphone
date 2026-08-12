namespace Aetherphone.Core.Video;

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

internal sealed class AetherStreamQueue
{
    private const int PollEveryTicks = 30;

    private readonly VideoPlayer video;
    private readonly VideoUrlResolver metadataResolver = new();
    private readonly List<VideoQueueEntry> entries = new();
    private int tickCounter;
    private bool wasIdle = true;
    private bool autoAdvanceArmed;

    public AetherStreamQueue(VideoPlayer video)
    {
        this.video = video;
        var persisted = Plugin.Cfg.VideoQueue;
        for (var recordIndex = 0; recordIndex < persisted.Count; recordIndex++)
        {
            var record = persisted[recordIndex];
            entries.Add(new VideoQueueEntry(record.Url, record.Title, record.Source,
                record.DurationSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null, record.ThumbnailUrl));
        }
    }

    public IReadOnlyList<VideoQueueEntry> Entries => entries;
    public VideoQueueEntry? Current { get; private set; }

    public VideoQueueEntry CreateDisplayEntry(string url)
    {
        var entry = new VideoQueueEntry(url, url, string.Empty, null, null);
        EnrichIfYouTube(entry);
        return entry;
    }

    public void Add(VideoQueueEntry entry)
    {
        entries.Add(entry);
        EnrichIfYouTube(entry);
        Persist();
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
        Persist();
    }

    public void Remove(VideoQueueEntry entry)
    {
        entries.Remove(entry);
        Persist();
    }

    public void Clear()
    {
        entries.Clear();
        Current = null;
        video.Stop();
        Persist();
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
        Persist();
    }

    private void Persist()
    {
        var records = new List<VideoQueueRecord>(entries.Count);
        for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            var entry = entries[entryIndex];
            records.Add(new VideoQueueRecord
            {
                Url = entry.Url,
                Title = entry.Title,
                Source = entry.Source,
                DurationSeconds = entry.Duration?.TotalSeconds,
                ThumbnailUrl = entry.ThumbnailUrl,
            });
        }

        Plugin.Cfg.VideoQueue = records;
        Plugin.Cfg.Save();
    }

    public void Advance()
    {
        if (entries.Count == 0)
        {
            Current = null;
            Persist();
            return;
        }

        Current = entries[0];
        entries.RemoveAt(0);
        autoAdvanceArmed = false;
        EnrichIfYouTube(Current);
        video.Play(Current.Url);
        Persist();
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
        Persist();
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
