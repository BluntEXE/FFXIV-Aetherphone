namespace Aetherphone.Core.Photos;

/// <summary>Trips once too many events land within a sliding window. Pure, no I/O; caller supplies the clock.</summary>
internal sealed class ImportBurstGuard
{
    private readonly int maxEventsInWindow;
    private readonly TimeSpan window;
    private readonly Queue<DateTime> timestamps = new();
    private bool tripped;

    public ImportBurstGuard(int maxEventsInWindow, TimeSpan window)
    {
        this.maxEventsInWindow = maxEventsInWindow;
        this.window = window;
    }

    public bool IsTripped => tripped;

    /// <summary>Records an event at <paramref name="now"/>. Returns false if the burst limit was just exceeded.</summary>
    public bool Allow(DateTime now)
    {
        if (tripped)
        {
            return false;
        }

        while (timestamps.Count > 0 && now - timestamps.Peek() > window)
        {
            timestamps.Dequeue();
        }

        timestamps.Enqueue(now);
        if (timestamps.Count > maxEventsInWindow)
        {
            tripped = true;
            return false;
        }

        return true;
    }

    public void Reset()
    {
        tripped = false;
        timestamps.Clear();
    }
}
