namespace Aetherphone.Core.Video;

internal sealed class VideoQueueEntry
{
    internal VideoQueueEntry(string url, string title, string source, TimeSpan? duration, string? thumbnailUrl)
    {
        Url = url;
        Title = title;
        Source = source;
        Duration = duration;
        ThumbnailUrl = thumbnailUrl;
    }

    internal Guid Id { get; } = Guid.NewGuid();
    internal string Url { get; }
    internal string Title { get; set; }
    internal string Source { get; set; }
    internal TimeSpan? Duration { get; set; }
    internal string? ThumbnailUrl { get; set; }
    internal bool EnrichRequested { get; set; }
}

internal sealed class AetherStreamQueue : IDisposable
{
    private const int MaxConsecutiveFailures = 3;

    private readonly VideoPlayer video;
    private readonly VideoUrlResolver metadata;
    private readonly List<VideoQueueEntry> entries = [];

    private int consecutiveFailures;
    private bool suspended;

    internal AetherStreamQueue(VideoPlayer video, VideoUrlResolver metadata)
    {
        this.video = video;
        this.metadata = metadata;
        video.Finished += OnPlaybackFinished;
        video.Failed += OnPlaybackFailed;

        var persisted = Plugin.Cfg.VideoQueue;
        for (var recordIndex = 0; recordIndex < persisted.Count; recordIndex++)
        {
            var record = persisted[recordIndex];
            entries.Add(new VideoQueueEntry(record.Url, record.Title, record.Source,
                record.DurationSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null, record.ThumbnailUrl)
            {
                EnrichRequested = record.DurationSeconds is not null,
            });
        }
    }

    internal IReadOnlyList<VideoQueueEntry> Entries => entries;
    internal VideoQueueEntry? Current { get; private set; }
    internal bool IsSuspended => suspended;

    internal event Action? Changed;

    internal VideoQueueEntry CreateDisplayEntry(string url)
    {
        var entry = new VideoQueueEntry(url, TitleFromUrl(url), string.Empty, null, null);
        EnrichIfYouTube(entry);
        return entry;
    }

    private static string TitleFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return url;
        }

        var name = Path.GetFileName(parsed.LocalPath);
        return name.Length > 0 && Path.HasExtension(name) ? name : url;
    }

    internal void Suspend()
    {
        if (suspended)
        {
            return;
        }

        suspended = true;
        if (Current is not null)
        {
            entries.Insert(0, Current);
            Current = null;
            Persist();
        }

        video.Stop();
    }

    internal void Resume() => suspended = false;

    internal void Add(VideoQueueEntry entry)
    {
        entries.Add(entry);
        EnrichIfYouTube(entry);
        Persist();
    }

    internal void PlayNow(VideoQueueEntry entry)
    {
        entries.Remove(entry);
        entries.Insert(0, entry);
        EnrichIfYouTube(entry);
        Advance();
    }

    internal void Remove(VideoQueueEntry entry)
    {
        entries.Remove(entry);
        Persist();
    }

    internal void Clear()
    {
        entries.Clear();
        Current = null;
        video.Stop();
        Persist();
    }

    internal void StopPlayback()
    {
        if (Current is { } stopped)
        {
            entries.Insert(0, stopped);
            Current = null;
            Persist();
        }

        video.Stop();
        Changed?.Invoke();
    }

    internal void Reorder(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= entries.Count || toIndex < 0 || toIndex >= entries.Count
            || fromIndex == toIndex)
        {
            return;
        }

        var item = entries[fromIndex];
        entries.RemoveAt(fromIndex);
        entries.Insert(toIndex, item);
        Persist();
    }

    internal bool HasNext => entries.Count > 0;

    internal void Advance()
    {
        if (suspended)
        {
            return;
        }

        if (entries.Count == 0)
        {
            Current = null;
            video.Stop();
            Persist();
            return;
        }

        Current = entries[0];
        entries.RemoveAt(0);
        EnrichIfYouTube(Current);
        video.Play(Current.Url);
        Persist();
    }

    internal void Restart()
    {
        if (Current is null)
        {
            return;
        }

        video.Seek(0d);
    }

    private void OnPlaybackFinished()
    {
        consecutiveFailures = 0;
        if (suspended)
        {
            return;
        }

        Advance();
    }

    private void OnPlaybackFailed(string? reason)
    {
        if (suspended)
        {
            return;
        }

        consecutiveFailures++;
        if (consecutiveFailures >= MaxConsecutiveFailures || entries.Count == 0)
        {
            Current = null;
            Changed?.Invoke();
            return;
        }

        Advance();
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
        Changed?.Invoke();
    }

    private void EnrichIfYouTube(VideoQueueEntry entry)
    {
        if (!VideoUrlResolver.IsYouTubeUrl(entry.Url) || entry.EnrichRequested)
        {
            return;
        }

        entry.EnrichRequested = true;
        _ = EnrichAsync(entry);
    }

    private async Task EnrichAsync(VideoQueueEntry entry)
    {
        var resolved = await metadata.ResolveMetadataAsync(entry.Url, CancellationToken.None).ConfigureAwait(false);
        if (resolved is null)
        {
            entry.EnrichRequested = false;
            return;
        }

        entry.Title = resolved.Title;
        entry.Source = resolved.Source;
        entry.Duration = resolved.Duration;
        entry.ThumbnailUrl = resolved.ThumbnailUrl;
        Persist();
    }

    public void Dispose()
    {
        video.Finished -= OnPlaybackFinished;
        video.Failed -= OnPlaybackFailed;
    }
}
