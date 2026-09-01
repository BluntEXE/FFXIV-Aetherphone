namespace Aetherphone.Core.VenueSync;

internal enum VenueSyncLoadState
{
    Idle,
    Loading,
    Ready,
    Failed,
}

internal sealed record VenueSyncShiftsSnapshot(VenueSyncShiftsResponse? Shifts, DateTime RefreshedAtUtc);

internal sealed class VenueSyncState : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly VenueSyncShiftsSnapshot EmptySnapshot = new(null, DateTime.MinValue);
    private readonly VenueSyncApiClient client;
    private readonly Configuration configuration;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object salesLock = new();
    private int refreshing;
    private volatile VenueSyncShiftsSnapshot shiftsSnapshot = EmptySnapshot;
    private volatile VenueSyncLoadState shiftsState = VenueSyncLoadState.Idle;

    private int sessionSalesCount;
    private decimal sessionSalesTotal;

    public VenueSyncState(VenueSyncApiClient client, Configuration configuration)
    {
        this.client = client;
        this.configuration = configuration;
    }

    // Read Shifts and LastRefreshUtc together via ShiftsSnapshot when both must agree
    // (see VenueSyncApp.Shifts.cs); the two convenience properties below are for call
    // sites that only ever need one and can tolerate it moving independently.
    public VenueSyncShiftsSnapshot ShiftsSnapshot => shiftsSnapshot;
    public VenueSyncShiftsResponse? Shifts => shiftsSnapshot.Shifts;
    public DateTime LastRefreshUtc => shiftsSnapshot.RefreshedAtUtc;

    public int SessionSalesCount
    {
        get { lock (salesLock) { return sessionSalesCount; } }
    }

    public decimal SessionSalesTotal
    {
        get { lock (salesLock) { return sessionSalesTotal; } }
    }

    public void EnsureShiftsFresh(bool force)
    {
        var venueId = configuration.VenueSyncSelectedVenueId;
        if (string.IsNullOrEmpty(venueId))
        {
            return;
        }

        if (Volatile.Read(ref refreshing) == 1)
        {
            return;
        }

        var stale = shiftsState == VenueSyncLoadState.Idle ||
                    DateTime.UtcNow - shiftsSnapshot.RefreshedAtUtc >= RefreshInterval;
        if (!force && !stale)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0)
        {
            return;
        }

        if (shiftsState != VenueSyncLoadState.Ready)
        {
            shiftsState = VenueSyncLoadState.Loading;
        }

        _ = RefreshShiftsAsync(venueId);
    }

    private async Task RefreshShiftsAsync(string venueId)
    {
        try
        {
            var token = cancellation.Token;
            var response = await client.GetShiftsAsync(venueId, token).ConfigureAwait(false);
            if (response is not null)
            {
                shiftsSnapshot = new VenueSyncShiftsSnapshot(response, DateTime.UtcNow);
                shiftsState = VenueSyncLoadState.Ready;
            }
            else if (shiftsState != VenueSyncLoadState.Ready)
            {
                shiftsState = VenueSyncLoadState.Failed;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (shiftsState != VenueSyncLoadState.Ready)
            {
                shiftsState = VenueSyncLoadState.Failed;
            }

            AepLog.Warning($"VenueSync shifts refresh failed: {exception.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref refreshing, 0);
        }
    }

    public void RecordSale(decimal amount)
    {
        lock (salesLock)
        {
            sessionSalesCount++;
            sessionSalesTotal += amount;
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
