using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Dalamud.Interface.Textures.TextureWraps;
using YoutubeExplode.Videos;

namespace Aetherphone.Core.Video;

internal static class VideoThumbnailResolver
{
    public static IDalamudTextureWrap? Get(RemoteImageCache cache, HttpService http, string? url,
        string? fallbackThumbnailUrl = null)
    {
        var id = url is not null ? VideoId.TryParse(url)?.Value : null;
        return id is not null
            ? cache.GetKeyed($"ytthumb:{id}", token => FetchAsync(http, id, token))
            : cache.Get(fallbackThumbnailUrl);
    }

    // maxresdefault.jpg only exists for videos uploaded at 720p or higher; hqdefault.jpg is
    // generated for every upload, so a miss on the first falls back to the second rather than
    // leaving the row without a thumbnail at all.
    private static async Task<byte[]?> FetchAsync(HttpService http, string videoId, CancellationToken token)
    {
        var maxres = await http.GetBytesAsync(new Uri($"https://i.ytimg.com/vi/{videoId}/maxresdefault.jpg"), token)
            .ConfigureAwait(false);
        if (maxres is not null)
        {
            return maxres;
        }

        return await http.GetBytesAsync(new Uri($"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg"), token)
            .ConfigureAwait(false);
    }
}
