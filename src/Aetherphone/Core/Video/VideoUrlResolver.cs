using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Aetherphone.Core.Video;

internal sealed record VideoMetadata(string Title, string Source, TimeSpan? Duration, string? ThumbnailUrl);

internal sealed record ResolvedStream(string VideoUrl, string? AudioUrl, string QualityLabel);

internal sealed class VideoUrlResolver
{
    private const int MuxedCeiling = 720;

    private readonly YoutubeClient youtube = new();

    public static bool IsYouTubeUrl(string url) => VideoId.TryParse(url) is not null;

    public async Task<(ResolvedStream? Stream, string? Error)> ResolveAsync(string url, int maxHeight,
        CancellationToken token)
    {
        try
        {
            var manifest = await youtube.Videos.Streams.GetManifestAsync(url, token).ConfigureAwait(false);

            var video = manifest.GetVideoOnlyStreams()
                .Where(stream => stream.VideoQuality.MaxHeight <= maxHeight)
                .OrderByDescending(stream => stream.VideoQuality.MaxHeight)
                .ThenByDescending(stream => stream.Bitrate)
                .FirstOrDefault();
            var audio = manifest.GetAudioOnlyStreams().OrderByDescending(stream => stream.Bitrate).FirstOrDefault();

            var muxed = manifest.GetMuxedStreams().Where(stream => stream.VideoQuality.MaxHeight <= maxHeight)
                .OrderByDescending(stream => stream.VideoQuality.MaxHeight).FirstOrDefault();

            if (maxHeight > MuxedCeiling && video is not null && audio is not null)
            {
                var label = video.VideoQuality.Label;
                AepLog.Debug($"[Video] Resolved {url} -> video={video.Url} audio={audio.Url} ({label}, adaptive)");
                return (new ResolvedStream(video.Url, audio.Url, label), null);
            }

            muxed ??= manifest.GetMuxedStreams().OrderBy(stream => stream.VideoQuality.MaxHeight).FirstOrDefault();
            if (muxed is not null)
            {
                AepLog.Debug($"[Video] Resolved {url} -> {muxed.Url} ({muxed.VideoQuality.Label}, muxed)");
                return (new ResolvedStream(muxed.Url, null, muxed.VideoQuality.Label), null);
            }

            return (null, "No playable stream found for this video.");
        }
        catch (OperationCanceledException)
        {
            return (null, null);
        }
        catch (Exception exception)
        {
            return (null, $"Failed to resolve YouTube URL: {exception.Message}");
        }
    }

    public async Task<VideoMetadata?> ResolveMetadataAsync(string url, CancellationToken token)
    {
        try
        {
            var video = await youtube.Videos.GetAsync(url, token).ConfigureAwait(false);
            var thumbnail = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault();
            return new VideoMetadata(video.Title, video.Author.ChannelTitle, video.Duration, thumbnail?.Url);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] Failed to fetch metadata for {url}: {exception.Message}");
            return null;
        }
    }
}
