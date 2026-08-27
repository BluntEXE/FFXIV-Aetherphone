using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Shell;

internal enum MinimizePhase : byte
{
    None,
    Collapsing,
    Minimized,
    Expanding,
}

internal sealed class MinimizeTransition
{
    private const float CollapseSmoothTime = 0.19f;
    private const float ExpandSmoothTime = 0.16f;
    private const float SettleProgress = 0.98f;

    private Spring progress;
    private MinimizePhase phase = MinimizePhase.None;

    public MinimizePhase Phase => phase;
    public bool MorphActive => phase is MinimizePhase.Collapsing or MinimizePhase.Expanding;
    public bool MinimizedResting => phase == MinimizePhase.Minimized;
    public float EasedProgress =>
        Easing.SmoothStep(Easing.Segment(progress.Value, 1f - SettleProgress, SettleProgress));

    public void BeginCollapse()
    {
        if (phase is MinimizePhase.None or MinimizePhase.Expanding)
        {
            phase = MinimizePhase.Collapsing;
            UiFeedback.Play(UiSound.Minimize);
        }
    }

    public void BeginExpand()
    {
        if (phase is MinimizePhase.Minimized or MinimizePhase.Collapsing)
        {
            phase = MinimizePhase.Expanding;
            UiFeedback.Play(UiSound.Maximize);
        }
    }

    public void SnapFull()
    {
        phase = MinimizePhase.None;
        progress.SnapTo(0f);
    }

    public void SnapMinimized()
    {
        phase = MinimizePhase.Minimized;
        progress.SnapTo(1f);
    }

    public void Advance(float delta)
    {
        switch (phase)
        {
            case MinimizePhase.None:
                progress.SnapTo(0f);
                break;
            case MinimizePhase.Minimized:
                progress.SnapTo(1f);
                break;
            case MinimizePhase.Collapsing:
                progress.Step(1f, CollapseSmoothTime, delta);
                if (progress.Value >= SettleProgress)
                {
                    progress.SnapTo(1f);
                    phase = MinimizePhase.Minimized;
                }

                break;
            case MinimizePhase.Expanding:
                progress.Step(0f, ExpandSmoothTime, delta);
                if (progress.Value <= 1f - SettleProgress)
                {
                    progress.SnapTo(0f);
                    phase = MinimizePhase.None;
                }

                break;
        }
    }
}
