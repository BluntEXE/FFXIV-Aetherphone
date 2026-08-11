using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BetComposerTests
{
    private const long Minimum = 10;
    private const long Maximum = 500;

    [Fact]
    public void TheStackIsAHardCeilingEvenUnderTheTableMaximum()
    {
        Assert.Equal(120, BetComposer.Ceiling(Maximum, 120));
        Assert.Equal(Maximum, BetComposer.Ceiling(Maximum, 900));
        Assert.Equal(0, BetComposer.Ceiling(Maximum, -5));
    }

    [Fact]
    public void AmountsClimbToTheMinimumAndStopAtTheCeiling()
    {
        Assert.Equal(Minimum, BetComposer.Clamp(0, Minimum, Maximum, 1000));
        Assert.Equal(Minimum, BetComposer.Clamp(3, Minimum, Maximum, 1000));
        Assert.Equal(250, BetComposer.Clamp(250, Minimum, Maximum, 1000));
        Assert.Equal(Maximum, BetComposer.Clamp(9000, Minimum, Maximum, 1000));
        Assert.Equal(120, BetComposer.Clamp(9000, Minimum, Maximum, 120));
    }

    [Fact]
    public void AStackTooThinForTheMinimumCanComposeNothingAtAll()
    {
        Assert.Equal(0, BetComposer.Clamp(50, Minimum, Maximum, 5));
        Assert.Equal(0, BetComposer.Clamp(Minimum, Minimum, Maximum, 0));
        Assert.Equal(0, BetComposer.Clamp(Minimum, Minimum, Maximum, -40));
    }

    [Fact]
    public void HalfIsHalfOfWhatCouldBeStakedAndNeverBelowTheMinimum()
    {
        Assert.Equal(250, BetComposer.Half(Minimum, Maximum, 1000));
        Assert.Equal(60, BetComposer.Half(Minimum, Maximum, 120));
        Assert.Equal(Minimum, BetComposer.Half(Minimum, Maximum, 15));
        Assert.Equal(0, BetComposer.Half(Minimum, Maximum, 9));
    }

    [Fact]
    public void NegativeEntriesCannotWalkPastTheMinimum()
    {
        Assert.Equal(Minimum, BetComposer.Clamp(-900, Minimum, Maximum, 1000));
    }

    [Fact]
    public void TheComposerHeightIsTheThreeRowsItActuallyDraws()
    {
        var expected = (BetComposer.FieldHeight + BetComposer.QuickHeight + BetComposer.ConfirmHeight + 16f) * 2f;
        Assert.Equal(expected, BetComposer.HeightFor(2f), 3);
    }
}
