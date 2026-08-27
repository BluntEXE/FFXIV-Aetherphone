using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Apps;

internal enum ShellMotion
{
    None,
    Present,
    Dismiss
}

internal sealed class NavigationStack : INavigator
{
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly AppInstaller installer;
    private readonly SuspensionGate suspensions;
    private readonly Stack<IPhoneApp> history = new();
    private Spring cover;
    private float coverTarget;
    private float coverSmoothTime;
    private IPhoneApp? current;
    private IPhoneApp? motionOver;
    private IPhoneApp? motionUnder;
    private ShellMotion motion = ShellMotion.None;
    private Rect? pendingOrigin;
    private LaunchOrigin pendingOriginKind;
    private Rect? motionOrigin;
    private LaunchOrigin motionOriginKind;

    public NavigationStack(IReadOnlyList<IPhoneApp> apps, AppInstaller installer, SuspensionGate suspensions)
    {
        this.apps = apps;
        this.installer = installer;
        this.suspensions = suspensions;
    }

    public event Action<string>? AppOpened;
    public event Action<string>? ReturningHome;
    public IPhoneApp? Current => current;
    public bool AtHome => current is null;
    public bool IsTransitioning => motion != ShellMotion.None;
    public ShellMotion Motion => motion;
    public float MotionProgress => cover.Value;
    public IPhoneApp MotionOver => motionOver!;
    public IPhoneApp? MotionUnder => motionUnder;
    public Rect? MotionOrigin => motionOrigin;
    public LaunchOrigin MotionOriginKind => motionOriginKind;

    public void Advance(float deltaSeconds)
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        cover.Step(coverTarget, coverSmoothTime, MathF.Min(deltaSeconds, TransitionTiming.MotionFrameSeconds));
        if (MathF.Abs(cover.Value - coverTarget) <= TransitionTiming.MotionSettleEpsilon)
        {
            cover.SnapTo(coverTarget);
            FinalizeMotion();
        }
    }

    public void OpenAppFrom(IPhoneApp app, Rect origin, LaunchOrigin kind)
    {
        pendingOrigin = origin;
        pendingOriginKind = kind;
        OpenApp(app);
        pendingOrigin = null;
        pendingOriginKind = LaunchOrigin.Icon;
    }

    public void OpenApp(IPhoneApp app)
    {
        if (suspensions.Blocks(app.Id))
        {
            suspensions.ReportBlocked();
            return;
        }

        if (motion == ShellMotion.None && ReferenceEquals(current, app))
        {
            NotifyOpened(app);
            return;
        }

        if (motion == ShellMotion.Dismiss && ReferenceEquals(motionOver, app))
        {
            ReverseToPresent();
            NotifyOpened(app);
            return;
        }

        SettleAny();
        var under = current;

        if (under is not null)
        {
            history.Push(under);
        }

        current = app;
        NotifyOpened(app);
        BeginPresent(app, under);
    }

    private void NotifyOpened(IPhoneApp app)
    {
        app.OnOpened();
        AppOpened?.Invoke(app.Id);
        UiFeedback.Play(UiSound.AppOpen);
    }

    public bool IsAvailable(string appId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId)
            {
                return apps[index].IsAvailable;
            }
        }

        return false;
    }

    public void Open(string appId)
    {
        if (!installer.IsInstalled(appId))
        {
            return;
        }

        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].Id == appId && apps[index].IsAvailable)
            {
                OpenApp(apps[index]);
                return;
            }
        }
    }

    public void Back()
    {
        if (motion == ShellMotion.Present && ReferenceEquals(motionOver, current))
        {
            ReverseToDismiss();
            return;
        }

        if (current is null)
        {
            return;
        }

        SettleAny();
        var leaving = current;
        var under = history.Count > 0 ? history.Pop() : null;
        current = under;
        under?.OnOpened();
        if (under is null)
        {
            ReturningHome?.Invoke(leaving.Id);
        }

        BeginDismiss(leaving, under);
    }

    public void GoHome()
    {
        SettleAny();

        if (current is null)
        {
            return;
        }

        var leaving = current;
        history.Clear();
        current = null;
        ReturningHome?.Invoke(leaving.Id);
        BeginDismiss(leaving, null);
    }

    private void BeginPresent(IPhoneApp over, IPhoneApp? under)
    {
        AppVisits.NoteOpened(over.Id);
        motion = ShellMotion.Present;
        motionOver = over;
        motionUnder = under;
        motionOrigin = under is null ? pendingOrigin : null;
        motionOriginKind = under is null ? pendingOriginKind : LaunchOrigin.Icon;
        coverTarget = 1f;
        coverSmoothTime = under is null ? TransitionTiming.ZoomPresentSmoothTime : TransitionTiming.PresentSmoothTime;
        cover.Launch(0f, TransitionTiming.LaunchVelocity(coverSmoothTime));
    }

    private void BeginDismiss(IPhoneApp over, IPhoneApp? under)
    {
        UiFeedback.Play(UiSound.AppClose);
        motion = ShellMotion.Dismiss;
        motionOver = over;
        motionUnder = under;
        motionOrigin = null;
        motionOriginKind = LaunchOrigin.Icon;
        coverTarget = 0f;
        coverSmoothTime = under is null ? TransitionTiming.ZoomDismissSmoothTime : TransitionTiming.DismissSmoothTime;
        cover.Launch(1f, -TransitionTiming.LaunchVelocity(coverSmoothTime));
    }

    private void ReverseToPresent()
    {
        if (motionUnder is not null)
        {
            history.Push(motionUnder);
        }

        current = motionOver;
        motion = ShellMotion.Present;
        coverTarget = 1f;
        coverSmoothTime = motionUnder is null ? TransitionTiming.ZoomPresentSmoothTime : TransitionTiming.PresentSmoothTime;
    }

    private void ReverseToDismiss()
    {
        var under = motionUnder;

        if (under is not null && history.Count > 0 && ReferenceEquals(history.Peek(), under))
        {
            history.Pop();
        }

        current = under;
        motion = ShellMotion.Dismiss;
        coverTarget = 0f;
        coverSmoothTime = motionUnder is null ? TransitionTiming.ZoomDismissSmoothTime : TransitionTiming.DismissSmoothTime;
    }

    private void SettleAny()
    {
        if (motion == ShellMotion.None)
        {
            return;
        }

        cover.SnapTo(motion == ShellMotion.Present ? 1f : 0f);
        FinalizeMotion();
    }

    private void FinalizeMotion()
    {
        if (motion == ShellMotion.Present)
        {
            motionUnder?.OnClosed();
        }
        else if (motion == ShellMotion.Dismiss)
        {
            motionOver?.OnClosed();
        }

        motion = ShellMotion.None;
        motionOver = null;
        motionUnder = null;
        motionOrigin = null;
        motionOriginKind = LaunchOrigin.Icon;
    }
}
