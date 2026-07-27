using Aetherphone.Core.Photos;
using Xunit;

namespace Aetherphone.Tests;

public class ImportBurstGuardTests
{
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EventsWithinLimit_AreAllAllowed()
    {
        var guard = new ImportBurstGuard(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        Assert.True(guard.Allow(Epoch));
        Assert.True(guard.Allow(Epoch.AddMilliseconds(100)));
        Assert.True(guard.Allow(Epoch.AddMilliseconds(200)));
        Assert.False(guard.IsTripped);
    }

    [Fact]
    public void EventBeyondLimitWithinWindow_Trips()
    {
        var guard = new ImportBurstGuard(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

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
        var guard = new ImportBurstGuard(20, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
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
    public void WithinCooldown_StaysTripped()
    {
        var guard = new ImportBurstGuard(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        guard.Allow(Epoch);
        guard.Allow(Epoch.AddMilliseconds(1));
        Assert.True(guard.IsTripped);

        Assert.False(guard.Allow(Epoch.AddSeconds(10)));
        Assert.True(guard.IsTripped);
    }

    [Fact]
    public void AfterCooldownElapses_AutoResumesWithoutManualReset()
    {
        var guard = new ImportBurstGuard(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        guard.Allow(Epoch);
        guard.Allow(Epoch.AddMilliseconds(1));
        Assert.True(guard.IsTripped);

        var afterCooldown = guard.Allow(Epoch.AddSeconds(31));

        Assert.True(afterCooldown);
        Assert.False(guard.IsTripped);
    }

    [Fact]
    public void LargeLegitimateBacklog_ImportsInThrottledWaves()
    {
        // A user bulk-importing hundreds of real screenshots should eventually get
        // everything, just spread across cooldown windows instead of all at once.
        var guard = new ImportBurstGuard(20, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        var allowedCount = 0;
        var current = Epoch;

        for (var index = 0; index < 200; index++)
        {
            if (guard.Allow(current))
            {
                allowedCount++;
            }
            else
            {
                // Skip past the cooldown so the backlog keeps draining instead of stalling forever.
                current = current.AddSeconds(31);
                if (guard.Allow(current))
                {
                    allowedCount++;
                }
            }

            current = current.AddMilliseconds(20);
        }

        Assert.Equal(200, allowedCount);
    }

    [Fact]
    public void ManualReset_ClearsTripImmediately()
    {
        var guard = new ImportBurstGuard(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        guard.Allow(Epoch);
        guard.Allow(Epoch.AddMilliseconds(1));
        Assert.True(guard.IsTripped);

        guard.Reset();
        Assert.False(guard.IsTripped);
        Assert.True(guard.Allow(Epoch.AddMilliseconds(2)));
    }

    [Fact]
    public void EventsSpreadBeyondWindow_NeverTrip()
    {
        var guard = new ImportBurstGuard(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        for (var index = 0; index < 50; index++)
        {
            Assert.True(guard.Allow(Epoch.AddSeconds(index * 2)));
        }

        Assert.False(guard.IsTripped);
    }
}
