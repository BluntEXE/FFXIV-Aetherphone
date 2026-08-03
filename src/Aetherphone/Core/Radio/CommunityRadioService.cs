using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Radio;

internal sealed class CommunityRadioService : IDisposable
{
    private const long ActiveIntervalMilliseconds = 30 * 1000;
    private const long IdleIntervalMilliseconds = 5 * 60 * 1000;
    private const long RetryIntervalMilliseconds = 60 * 1000;

    private readonly AethernetApi api;
    private readonly CancellationTokenSource cancellation = new();
    private volatile CommunityStationDto[] stations = Array.Empty<CommunityStationDto>();
    private volatile MyCommunityStationDto? mine;
    private volatile bool loading;
    private volatile bool loaded;
    private volatile bool mineChecked;
    private long lastFetchTick = long.MinValue / 2;
    private long retryAfterTick;
    private int fetching;
    private int fetchingMine;

    public CommunityRadioService(AethernetApi api)
    {
        this.api = api;
    }

    public CommunityStationDto[] Stations => stations;
    public MyCommunityStationDto? Mine => mine;
    public bool Loading => loading;
    public bool Loaded => loaded;
    public bool OwnsStation => mine is not null;

    public int LiveCount
    {
        get
        {
            var snapshot = stations;
            var count = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index].IsLive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // The interval belongs to the caller, not to whoever fetched last. Storing a single "next fetch"
    // stamp let a background refresh from the home screen schedule five minutes out and then hold the
    // radio screens to that, so a station could go live and the list would keep saying off air.
    public void EnsureFresh(bool active)
    {
        var now = Environment.TickCount64;
        if (now < Volatile.Read(ref retryAfterTick))
        {
            return;
        }

        var interval = active ? ActiveIntervalMilliseconds : IdleIntervalMilliseconds;
        if (now - Volatile.Read(ref lastFetchTick) < interval)
        {
            return;
        }

        Refresh(active);
    }

    public void Refresh(bool active = true)
    {
        if (Interlocked.Exchange(ref fetching, 1) == 1)
        {
            return;
        }

        Volatile.Write(ref retryAfterTick, 0);
        loading = !loaded;
        _ = FetchAsync(cancellation.Token);
    }

    public void EnsureMine()
    {
        if (mineChecked || Interlocked.Exchange(ref fetchingMine, 1) == 1)
        {
            return;
        }

        _ = FetchMineAsync(cancellation.Token);
    }

    public async Task<bool> SaveMineAsync(UpdateCommunityStationRequest request)
    {
        try
        {
            var updated = await api.Radio.UpdateMineAsync(request, cancellation.Token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            mine = updated;
            Volatile.Write(ref lastFetchTick, long.MinValue / 2);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Radio] station update failed: {exception.Message}");
            return false;
        }
    }

    public bool TryFind(string stationId, out CommunityStationDto station)
    {
        var snapshot = stations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (string.Equals(snapshot[index].Id, stationId, StringComparison.Ordinal))
            {
                station = snapshot[index];
                return true;
            }
        }

        station = null!;
        return false;
    }

    public static RadioStation ToStation(CommunityStationDto station)
    {
        return new RadioStation(station.Name, station.StreamUrl, "MP3", 0, string.Empty, string.Empty, station.Id);
    }

    public static RadioStation[] ToQueue(CommunityStationDto[] source)
    {
        var queue = new RadioStation[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            queue[index] = ToStation(source[index]);
        }

        return queue;
    }

    private async Task FetchAsync(CancellationToken token)
    {
        try
        {
            var page = await api.Radio.StationsAsync(token).ConfigureAwait(false);
            if (page?.Items is { } items)
            {
                stations = items;
                loaded = true;
                Volatile.Write(ref lastFetchTick, Environment.TickCount64);
                return;
            }

            Volatile.Write(ref retryAfterTick, Environment.TickCount64 + RetryIntervalMilliseconds);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Radio] community fetch failed: {exception.Message}");
            Volatile.Write(ref retryAfterTick, Environment.TickCount64 + RetryIntervalMilliseconds);
        }
        finally
        {
            loading = false;
            Volatile.Write(ref fetching, 0);
        }
    }

    private async Task FetchMineAsync(CancellationToken token)
    {
        try
        {
            mine = await api.Radio.MineAsync(token).ConfigureAwait(false);
            mineChecked = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Radio] station lookup failed: {exception.Message}");
        }
        finally
        {
            Volatile.Write(ref fetchingMine, 0);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
