namespace Aetherphone.Core.Photos;

/// <summary>Trips once too many events land within a sliding window, then auto-resumes after a cooldown.
/// Pure, no I/O; caller supplies the clock.</summary>
internal sealed class ImportBurstGuard
{
    private readonly int maxEventsInWindow;
    private readonly TimeSpan window;
    private readonly TimeSpan cooldown;
    private readonly Queue<DateTime> timestamps = new();
    private bool tripped;
    private DateTime trippedAt;

    public ImportBurstGuard(int maxEventsInWindow, TimeSpan window, TimeSpan cooldown)
    {
        this.maxEventsInWindow = maxEventsInWindow;
        this.window = window;
        this.cooldown = cooldown;
    }

    public bool IsTripped => tripped;

    /// <summary>Records an event at <paramref name="now"/>. Returns false if the burst limit is exceeded
    /// or the cooldown from a previous trip hasn't elapsed yet.</summary>
    public bool Allow(DateTime now)
    {
        if (tripped)
        {
            if (now - trippedAt < cooldown)
            {
                return false;
            }

            Reset();
        }

        while (timestamps.Count > 0 && now - timestamps.Peek() > window)
        {
            timestamps.Dequeue();
        }

        timestamps.Enqueue(now);
        if (timestamps.Count > maxEventsInWindow)
        {
            tripped = true;
            trippedAt = now;
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
