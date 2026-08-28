using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PopoutPlacementTests
{
    private const float Tolerance = 1e-4f;

    private static readonly Rect Viewport = new(Vector2.Zero, new Vector2(2560f, 1440f));

    [Fact]
    public void WindowInTheLowerHalfAnchorsItsBottomEdge()
    {
        var frame = new Rect(new Vector2(1900f, 900f), new Vector2(2236f, 1330f));

        Assert.True(PopoutPlacement.AnchorsBottomEdge(frame, Viewport));
    }

    [Fact]
    public void WindowInTheUpperHalfAnchorsItsTopEdge()
    {
        var frame = new Rect(new Vector2(200f, 80f), new Vector2(536f, 510f));

        Assert.False(PopoutPlacement.AnchorsBottomEdge(frame, Viewport));
    }

    [Fact]
    public void CollapsingAgainstTheBottomEdgeKeepsThatEdgeStill()
    {
        var frame = new Rect(new Vector2(1900f, 900f), new Vector2(2236f, 1330f));

        var position = PopoutPlacement.AnchoredPosition(frame, true, 44f);

        Assert.Equal(1900f, position.X, Tolerance);
        Assert.Equal(1330f, position.Y + 44f, Tolerance);
    }

    [Fact]
    public void CollapsingAgainstTheTopEdgeLeavesThePositionAlone()
    {
        var frame = new Rect(new Vector2(200f, 80f), new Vector2(536f, 510f));

        var position = PopoutPlacement.AnchoredPosition(frame, false, 44f);

        Assert.Equal(frame.Min, position);
    }
}
