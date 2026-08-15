using Aetherphone.Core.Net;

namespace Aetherphone.Apps.Music.Rolladeck;

internal sealed class RolladeckService(HttpService http)
{
    private const string LiveApiUrl      = "https://us-central1-xiv-rolladeck.cloudfunctions.net/apiV1Live";
    private const string ScheduleApiUrl  = "https://us-central1-xiv-rolladeck.cloudfunctions.net/apiV1Schedule";
    private const int    LiveCacheSecs   = 120;
    private const int    SchedCacheSecs  = 300;

    private LiveResponse?        data;
    private List<ScheduleEntry>? scheduleData;
    private DateTime             lastFetch         = DateTime.MinValue;
    private DateTime             scheduleLastFetch = DateTime.MinValue;
    private bool                 fetching;
    private bool                 scheduleFetching;

    public IReadOnlyList<LiveDjEntry>    LiveDJs   => data?.LiveDJs    ?? (IReadOnlyList<LiveDjEntry>)[];
    public IReadOnlyList<OpenVenueEntry> OpenVenues => data?.OpenVenues ?? (IReadOnlyList<OpenVenueEntry>)[];
    public IReadOnlyList<ScheduleEntry>  Schedule  => scheduleData      ?? (IReadOnlyList<ScheduleEntry>)[];

    public int  LiveCount      => data?.LiveDJs.Count ?? 0;

    public int LiveCountWithAddress
    {
        get
        {
            if (data == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < data.LiveDJs.Count; index++)
            {
                var dj = data.LiveDJs[index];
                if (dj.VenueName != null || dj.FormattedAddress.Length > 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public bool Loading        => fetching;
    public bool ScheduleLoading => scheduleFetching;
    public bool HasData        => data != null;
    public bool HasSchedule    => scheduleData != null;

    public void EnsureFresh(bool force = false)
    {
        if (!fetching && (force || data == null || (DateTime.UtcNow - lastFetch).TotalSeconds >= LiveCacheSecs))
        {
            fetching = true;
            _ = Task.Run(FetchLiveAsync);
        }

        if (!scheduleFetching && (force || scheduleData == null || (DateTime.UtcNow - scheduleLastFetch).TotalSeconds >= SchedCacheSecs))
        {
            scheduleFetching = true;
            _ = Task.Run(FetchScheduleAsync);
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
                data      = result;
                lastFetch = DateTime.UtcNow;
            }
        }
        catch { }
        finally { fetching = false; }
    }

    private async Task FetchScheduleAsync()
    {
        try
        {
            var result = await http.GetJsonAsync(
                ScheduleApiUrl,
                RolladeckJsonContext.Default.ScheduleResponse,
                bearer: null,
                token:  default);

            if (result != null)
            {
                scheduleData      = result.Schedule;
                scheduleLastFetch = DateTime.UtcNow;
            }
        }
        catch { }
        finally { scheduleFetching = false; }
    }
}
