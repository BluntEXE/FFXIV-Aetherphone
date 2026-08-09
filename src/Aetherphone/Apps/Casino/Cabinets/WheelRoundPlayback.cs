using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum WheelStage
{
    Waiting,
    Betting,
    Locking,
    Spinning,
    Settling,
}

// The room owns the clock and this reads it. Nothing here counts a phase down on frame time: the
// stage is whatever the last snapshot said, and where the spin has got to is derived from the
// deadline that snapshot carried, so a phone that just woke up, one that cold-opened mid spin,
// and one that watched from the lock all show the same wheel at the same moment.
//
// That derivation is the whole reason a joiner never replays. The spin is staged to land a beat
// before the locked window closes, so its progress is the distance from that landing instant, not
// the time since this client happened to learn the result. Arriving with three seconds left picks
// the wheel up three seconds from rest; arriving after the window shows it already stopped.
internal sealed class WheelRoundPlayback
{
    public const float SettleHoldSeconds = 1.2f;

    private const float LockSpinRadiansPerSecond = 6.5f;

    private string roundId = string.Empty;
    private WheelStage stage = WheelStage.Waiting;
    private float angle;
    private float restAngle;
    private float spinFromAngle;
    private float sweep;
    private float spinElapsedSeconds;
    private float lockElapsedSeconds;
    private float stageSeconds;
    private int segment = -1;
    private int lastSegment = -1;
    private bool spinBegun;

    public WheelStage Stage => stage;

    public string RoundId => roundId;

    public float Angle => angle;

    public int Segment => segment;

    public int LastSegment => lastSegment;

    public float StageSeconds => stageSeconds;

    public bool Landed => spinBegun && spinElapsedSeconds >= WheelChoreography.SpinSeconds;

    public float SpinProgress => WheelChoreography.Progress(spinElapsedSeconds);

    public void Reset()
    {
        roundId = string.Empty;
        stage = WheelStage.Waiting;
        angle = 0f;
        restAngle = 0f;
        spinFromAngle = 0f;
        sweep = 0f;
        spinElapsedSeconds = 0f;
        lockElapsedSeconds = 0f;
        stageSeconds = 0f;
        segment = -1;
        lastSegment = -1;
        spinBegun = false;
    }

    public void Update(CasinoRoomSnapshotDto? snapshot, long remainingMilliseconds, float deltaSeconds)
    {
        stageSeconds += deltaSeconds;
        if (snapshot is null)
        {
            return;
        }

        if (!string.Equals(snapshot.RoundId, roundId, StringComparison.Ordinal))
        {
            OpenRound(snapshot.RoundId);
        }

        var drawn = DrawnSegment(snapshot);
        if (drawn >= 0 && !spinBegun)
        {
            BeginSpin(drawn, snapshot.Phase, remainingMilliseconds);
        }

        if (spinBegun)
        {
            AdvanceSpin(deltaSeconds);
            return;
        }

        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            angle = restAngle;
            EnterStage(WheelStage.Betting);
            return;
        }

        lockElapsedSeconds += deltaSeconds;
        angle = restAngle + lockElapsedSeconds * LockSpinRadiansPerSecond;
        EnterStage(WheelStage.Locking);
    }

    internal static int DrawnSegment(CasinoRoomSnapshotDto snapshot)
    {
        var numbers = snapshot.Numbers;
        if (numbers is null || numbers.Length == 0)
        {
            return -1;
        }

        var drawn = numbers[0];
        return WheelRules.IsSegment(drawn) ? drawn : -1;
    }

    // A window longer than the spin hands back a negative elapsed on purpose: the choreography
    // reads that as the free run before the deceleration, so the rim is already turning the
    // instant bets close instead of sitting still until the curve is due to start.
    internal static float InitialSpinElapsed(int phase, long remainingMilliseconds)
    {
        if (phase != CasinoRoomPhases.Locked)
        {
            return WheelChoreography.SpinSeconds;
        }

        var remainingSeconds = remainingMilliseconds / 1000f;
        var elapsed = WheelChoreography.SpinSeconds - (remainingSeconds - SettleHoldSeconds);
        return elapsed > WheelChoreography.SpinSeconds ? WheelChoreography.SpinSeconds : elapsed;
    }

    private void OpenRound(string nextRoundId)
    {
        roundId = nextRoundId;
        restAngle = WheelChoreography.Normalize(angle);
        angle = restAngle;
        spinFromAngle = restAngle;
        sweep = 0f;
        spinElapsedSeconds = 0f;
        lockElapsedSeconds = 0f;
        segment = -1;
        spinBegun = false;
    }

    private void BeginSpin(int drawn, int phase, long remainingMilliseconds)
    {
        segment = drawn;
        lastSegment = drawn;
        spinFromAngle = angle;
        sweep = WheelChoreography.SweepFor(spinFromAngle, drawn);
        spinElapsedSeconds = InitialSpinElapsed(phase, remainingMilliseconds);
        spinBegun = true;
        AdvanceSpin(0f);
    }

    private void AdvanceSpin(float deltaSeconds)
    {
        spinElapsedSeconds += deltaSeconds;
        if (spinElapsedSeconds > WheelChoreography.SpinSeconds)
        {
            spinElapsedSeconds = WheelChoreography.SpinSeconds;
        }

        angle = WheelChoreography.AngleAt(spinFromAngle, sweep, spinElapsedSeconds);
        if (spinElapsedSeconds < WheelChoreography.SpinSeconds)
        {
            EnterStage(WheelStage.Spinning);
            return;
        }

        restAngle = WheelChoreography.Normalize(angle);
        EnterStage(WheelStage.Settling);
    }

    private void EnterStage(WheelStage next)
    {
        if (stage == next)
        {
            return;
        }

        stage = next;
        stageSeconds = 0f;
    }
}
