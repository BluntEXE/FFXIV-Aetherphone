using Aetherphone.Core.Photos;
using Xunit;

namespace Aetherphone.Tests;

public class ImportBurstGuardTests
{
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EventsWithinLimit_AreAllAllowed()
    {
        var guard = new ImportBurstGuard(3, TimeSpan.FromSeconds(1));

        Assert.True(guard.Allow(Epoch));
        Assert.True(guard.Allow(Epoch.AddMilliseconds(100)));
        Assert.True(guard.Allow(Epoch.AddMilliseconds(200)));
        Assert.False(guard.IsTripped);
    }

    [Fact]
    public void EventBeyondLimitWithinWindow_Trips()
    {
        var guard = new ImportBurstGuard(3, TimeSpan.FromSeconds(1));

        guard.Allow(Epoch);
        guard.Allow(Epoch.AddMilliseconds(100));
        guard.Allow(Epoch.AddMilliseconds(200));
        var fourth = guard.Allow(Epoch.AddMilliseconds(300));

        Assert.False(fourth);
        Assert.True(guard.IsTripped);
    }

    [Fact]
    public void SimulatedBurst_TripsAfterThreshold()
    {
        // Mirrors the reported bug: dozens of file events landing within a couple seconds
        // for a single physical action.
        var guard = new ImportBurstGuard(20, TimeSpan.FromSeconds(5));
        var allowedCount = 0;

        for (var index = 0; index < 100; index++)
        {
            if (guard.Allow(Epoch.AddMilliseconds(index * 20)))
            {
                allowedCount++;
            }
        }

        Assert.True(guard.IsTripped);
        Assert.Equal(20, allowedCount);
    }

    [Fact]
    public void OnceTripped_StaysTrippedUntilReset()
    {
        var guard = new ImportBurstGuard(1, TimeSpan.FromSeconds(1));

        guard.Allow(Epoch);
        guard.Allow(Epoch.AddMilliseconds(1));
        Assert.True(guard.IsTripped);

        Assert.False(guard.Allow(Epoch.AddHours(1)));
        Assert.True(guard.IsTripped);

        guard.Reset();
        Assert.False(guard.IsTripped);
        Assert.True(guard.Allow(Epoch.AddHours(2)));
    }

    [Fact]
    public void EventsSpreadBeyondWindow_NeverTrip()
    {
        var guard = new ImportBurstGuard(3, TimeSpan.FromSeconds(1));

        for (var index = 0; index < 50; index++)
        {
            Assert.True(guard.Allow(Epoch.AddSeconds(index * 2)));
        }

        Assert.False(guard.IsTripped);
    }
}
