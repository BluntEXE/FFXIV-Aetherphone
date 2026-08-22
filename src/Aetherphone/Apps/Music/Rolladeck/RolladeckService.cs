using Aetherphone.Core.Net;

namespace Aetherphone.Apps.Music.Rolladeck;

internal sealed class RolladeckService(HttpService http)
{
    private const string LiveApiUrl    = "https://us-central1-xiv-rolladeck.cloudfunctions.net/apiV1Live";
    private const int    LiveCacheSecs = 120;

    private LiveResponse? data;
    private DateTime      lastFetch = DateTime.MinValue;
    private bool          fetching;
    private bool          fetchFailed;
    private int           liveCountWithAddress;

    public IReadOnlyList<LiveDjEntry>    LiveDJs    => data?.LiveDJs    ?? (IReadOnlyList<LiveDjEntry>)[];
    public IReadOnlyList<OpenVenueEntry> OpenVenues => data?.OpenVenues ?? (IReadOnlyList<OpenVenueEntry>)[];

    public int  LiveCount            => data?.LiveDJs.Count ?? 0;
    public int  LiveCountWithAddress => liveCountWithAddress;
    public bool Loading              => fetching;
    public bool HasData              => data != null;
    public bool Failed               => fetchFailed;

    public void EnsureFresh(bool force = false)
    {
        if (!fetching && (force || data == null || (DateTime.UtcNow - lastFetch).TotalSeconds >= LiveCacheSecs))
        {
            fetching = true;
            _ = Task.Run(FetchLiveAsync);
        }
    }

    private async Task FetchLiveAsync()
    {
        try
        {
            var result = await http.GetJsonAsync(
                LiveApiUrl,
                RolladeckJsonContext.Default.LiveResponse,
                bearer: null,
                token:  default);

            if (result != null)
            {
                for (var index = 0; index < result.LiveDJs.Count; index++)
                {
                    result.LiveDJs[index].InitNormalized();
                }

                var count = 0;
                for (var index = 0; index < result.LiveDJs.Count; index++)
                {
                    if (result.LiveDJs[index].HasLocation)
                    {
                        count++;
                    }
                }

                liveCountWithAddress = count;
                fetchFailed          = false;
                data                 = result;
                lastFetch            = DateTime.UtcNow;
            }
        }
        catch
        {
            fetchFailed = true;
        }
        finally { fetching = false; }
    }
}
