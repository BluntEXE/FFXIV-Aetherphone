namespace Aetherphone.Apps.Camera;

/// <summary>Debounces the camera shutter using real elapsed time instead of frame count, so a burst of
/// Draw() calls in a short real-time window (e.g. from an external present flood) can't take more than
/// one photo per press. Pure, no ImGui; caller supplies delta time.</summary>
internal sealed class ShutterGate
{
    private readonly float captureDelaySeconds;
    private readonly float cooldownSeconds;
    private float pendingElapsed;
    private float cooldownRemaining;
    private bool pending;

    public ShutterGate(float captureDelaySeconds, float cooldownSeconds)
    {
        this.captureDelaySeconds = captureDelaySeconds;
        this.cooldownSeconds = cooldownSeconds;
    }

    public bool IsPending => pending;

    /// <summary>Starts a capture if none is pending and the post-capture cooldown has elapsed.</summary>
    public bool TryStart()
    {
        if (pending || cooldownRemaining > 0f)
        {
            return false;
        }

        pending = true;
        pendingElapsed = 0f;
        return true;
    }

    /// <summary>Advances time. Returns true exactly once, on the tick the pending capture delay completes.</summary>
    public bool Tick(float deltaSeconds)
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining = MathF.Max(0f, cooldownRemaining - deltaSeconds);
        }

        if (!pending)
        {
            return false;
        }

        pendingElapsed += deltaSeconds;
        if (pendingElapsed < captureDelaySeconds)
        {
            return false;
        }

        pending = false;
        cooldownRemaining = cooldownSeconds;
        return true;
    }

    /// <summary>Abandons a pending capture without starting the cooldown, for a forced-detach safety net.</summary>
    public void Cancel()
    {
        pending = false;
    }

    public void Reset()
    {
        pending = false;
        pendingElapsed = 0f;
        cooldownRemaining = 0f;
    }
}
