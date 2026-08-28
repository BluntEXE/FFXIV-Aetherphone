using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NumberTextTests
{
    private static string Expected(long value) => value.ToString("N0", Loc.Culture);

    [Fact]
    public void GroupMatchesTheUncachedFormat()
    {
        foreach (var value in new long[]
                 {
                     0, 1, 9, 10, 99, 100, 999, 1000, 1001, 9999, 10_000, 123_456,
                     1_000_000, 999_999_999, long.MaxValue,
                     -1, -999, -1000, -123_456, long.MinValue,
                 })
        {
            Assert.Equal(Expected(value), NumberText.Group(value));
        }
    }

    [Fact]
    public void GroupStaysCorrectPastTheCacheLimit()
    {
        for (long value = 0; value < 5000; value++)
        {
            Assert.Equal(Expected(value), NumberText.Group(value));
        }
    }

    [Fact]
    public void GroupIsStableAcrossRepeatedCalls()
    {
        var first = NumberText.Group(1_234_567);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Assert.Equal(first, NumberText.Group(1_234_567));
        }

        Assert.Equal(Expected(1_234_567), first);
    }

    [Fact]
    public void GroupSeparatesNeighbouringValues()
    {
        Assert.NotEqual(NumberText.Group(1000), NumberText.Group(1001));
        Assert.NotEqual(NumberText.Group(-1000), NumberText.Group(1000));
    }
}
