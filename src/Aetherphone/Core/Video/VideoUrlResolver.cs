using Aetherphone.Core.Net;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YouTubeVideo = YoutubeExplode.Videos.Video;

namespace Aetherphone.Core.Video;

internal sealed record VideoMetadata(string Title, string Source, TimeSpan? Duration, string? ThumbnailUrl);

internal sealed class VideoUrlResolver : IDisposable
{
    private readonly YoutubeClient youtube;
    private readonly RequestThrottle throttle = new(1, TimeSpan.FromMilliseconds(400));

    internal VideoUrlResolver(YoutubeClient youtube)
    {
        this.youtube = youtube;
    }

    internal static bool IsYouTubeUrl(string url) => VideoId.TryParse(url) is not null;

    internal async Task<VideoMetadata?> ResolveMetadataAsync(string url, CancellationToken token)
    {
        try
        {
            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var video = await youtube.Videos.GetAsync(url, token).ConfigureAwait(false);
                var thumbnail = BestThumbnail(video);
                return new VideoMetadata(video.Title, video.Author.ChannelTitle, video.Duration, thumbnail);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] Could not read details for {url}: {exception.Message}");
            return null;
        }
    }

    private static string? BestThumbnail(YouTubeVideo video)
    {
        var thumbnails = video.Thumbnails;
        string? best = null;
        var bestArea = 0;
        for (var index = 0; index < thumbnails.Count; index++)
        {
            var area = thumbnails[index].Resolution.Area;
            if (area <= bestArea)
            {
                continue;
            }

            bestArea = area;
            best = thumbnails[index].Url;
        }

        return best;
    }

    public void Dispose() => throttle.Dispose();
}
