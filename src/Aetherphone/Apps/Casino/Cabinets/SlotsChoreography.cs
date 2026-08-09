using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

// Choreography of a known result: the server grid is final before the first reel moves, and
// these timings only stage how it is revealed. Stops run left to right from 0.9 s in 0.25 s
// steps, so the first, middle, and last reels land at the ratified 0.9 / 1.4 / 1.9 marks,
// and the only permitted drama is the 0.6 s hold on the last reel when the settled outcome
// is a bonus or a big win. Never a fabricated near miss.
internal sealed class SlotsChoreography
{
    public const float FirstStopSeconds = 0.9f;
    public const float StopStaggerSeconds = 0.25f;
    public const float AnticipationHoldSeconds = 0.6f;
    public const float LandingSeconds = 0.3f;

    private float firstStopSeconds = FirstStopSeconds;
    private float stopStaggerSeconds = StopStaggerSeconds;
    private bool holdLastReel;
    private bool begun;
    private bool running;
    private float elapsedSeconds;

    public bool Running => running;

    public float ElapsedSeconds => elapsedSeconds;

    public bool HoldsLastReel => holdLastReel;

    public bool Settled => begun && elapsedSeconds >= StopSeconds(SlotsRules.ReelCount - 1);

    public bool Anticipating => holdLastReel && begun && !Settled && ReelStopped(SlotsRules.ReelCount - 2);

    public void Begin(bool anticipation)
    {
        Begin(anticipation, FirstStopSeconds, StopStaggerSeconds);
    }

    public void Begin(bool anticipation, float firstStop, float stagger)
    {
        holdLastReel = anticipation;
        firstStopSeconds = firstStop;
        stopStaggerSeconds = stagger;
        elapsedSeconds = 0f;
        begun = true;
        running = true;
    }

    public void Update(float deltaSeconds)
    {
        if (!running)
        {
            return;
        }

        elapsedSeconds += deltaSeconds;
        if (Settled)
        {
            running = false;
        }
    }

    public float StopSeconds(int reel)
    {
        var stop = firstStopSeconds + stopStaggerSeconds * reel;
        if (holdLastReel && reel == SlotsRules.ReelCount - 1)
        {
            stop += AnticipationHoldSeconds;
        }

        return stop;
    }

    public bool ReelStopped(int reel)
    {
        return elapsedSeconds >= StopSeconds(reel);
    }

    public float LandingProgress(int reel)
    {
        var start = StopSeconds(reel) - LandingSeconds;
        if (elapsedSeconds <= start)
        {
            return 0f;
        }

        var progress = (elapsedSeconds - start) / LandingSeconds;
        return progress >= 1f ? 1f : progress;
    }

    public void Finish()
    {
        elapsedSeconds = StopSeconds(SlotsRules.ReelCount - 1);
        begun = true;
        running = false;
    }
}
