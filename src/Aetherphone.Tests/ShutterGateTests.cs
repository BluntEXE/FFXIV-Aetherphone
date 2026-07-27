using Aetherphone.Apps.Camera;
using Xunit;

namespace Aetherphone.Tests;

public class ShutterGateTests
{
    [Fact]
    public void SinglePress_CompletesExactlyOneCapture()
    {
        var gate = new ShutterGate(0.05f, 0.5f);

        Assert.True(gate.TryStart());
        var completions = 0;
        for (var frame = 0; frame < 10; frame++)
        {
            if (gate.Tick(0.016f))
            {
                completions++;
            }
        }

        Assert.Equal(1, completions);
    }

    [Fact]
    public void SecondPressWhilePending_IsIgnored()
    {
        var gate = new ShutterGate(0.05f, 0.5f);

        Assert.True(gate.TryStart());
        Assert.False(gate.TryStart());
    }

    [Fact]
    public void PressDuringCooldown_IsIgnored()
    {
        var gate = new ShutterGate(0.05f, 0.5f);

        gate.TryStart();
        gate.Tick(0.06f);

        Assert.False(gate.TryStart());
    }

    [Fact]
    public void SimulatedPresentBurst_StillCompletesExactlyOneCaptureFromOnePress()
    {
        // Mirrors the reported bug: a single physical shutter click, but Draw() (and hence
        // AdvanceTimers/Tick) fires many times in a short real-time window because something
        // upstream (e.g. an external present flood) is calling it far faster than 60fps.
        var gate = new ShutterGate(0.05f, 0.5f);

        Assert.True(gate.TryStart());
        var completions = 0;
        for (var frame = 0; frame < 200; frame++)
        {
            // Each "frame" advances real time by only 2ms, so 200 frames span 400ms total,
            // well inside both the capture delay and the cooldown.
            if (gate.Tick(0.002f))
            {
                completions++;
            }
        }

        Assert.Equal(1, completions);
    }

    [Fact]
    public void AfterCooldownElapses_NextPressIsAllowed()
    {
        var gate = new ShutterGate(0.05f, 0.5f);

        gate.TryStart();
        gate.Tick(0.06f);

        for (var frame = 0; frame < 30; frame++)
        {
            gate.Tick(0.02f);
        }

        Assert.True(gate.TryStart());
    }

    [Fact]
    public void Cancel_DoesNotStartCooldown()
    {
        var gate = new ShutterGate(0.05f, 0.5f);

        gate.TryStart();
        gate.Cancel();

        Assert.True(gate.TryStart());
    }
}
