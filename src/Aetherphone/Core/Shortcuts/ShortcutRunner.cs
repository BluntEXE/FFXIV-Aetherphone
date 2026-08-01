using Aetherphone.Core.Linkpearl;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Shortcuts;

internal enum ShortcutRunOutcome : byte
{
    Completed,
    Cancelled,
    CommandRejected,
    PluginUnavailable,
}

internal sealed class ShortcutRunner : IDisposable
{
    public const float MaxWaitSeconds = 60f;
    private const float StepGapSeconds = 0.05f;

    private readonly List<ShortcutStep> queue = new();
    private Guid runningId;
    private string runningName = string.Empty;
    private int cursor;
    private float wait;
    private bool subscribed;

    public event Action<Guid, string, ShortcutRunOutcome>? Finished;

    public Guid RunningId => runningId;

    public bool IsRunning => runningId != Guid.Empty;

    public int RunningStep => cursor;

    public int RunningStepCount => queue.Count;

    public void Run(ShortcutEntry entry)
    {
        if (entry.Steps.Count == 0)
        {
            return;
        }

        if (!Plugin.Framework.IsInFrameworkUpdateThread)
        {
            var snapshot = entry.Copy();
            snapshot.Id = entry.Id;
            _ = Plugin.Framework.RunOnFrameworkThread(() => Run(snapshot));
            return;
        }

        Cancel();
        queue.Clear();
        for (var index = 0; index < entry.Steps.Count; index++)
        {
            queue.Add(entry.Steps[index]);
        }

        runningId = entry.Id;
        runningName = entry.Name;
        cursor = 0;
        wait = 0f;
        Subscribe();
    }

    public void Cancel()
    {
        if (runningId == Guid.Empty)
        {
            return;
        }

        if (!Plugin.Framework.IsInFrameworkUpdateThread)
        {
            _ = Plugin.Framework.RunOnFrameworkThread(Cancel);
            return;
        }

        Stop(ShortcutRunOutcome.Cancelled);
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        subscribed = true;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        subscribed = false;
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (runningId == Guid.Empty)
        {
            Unsubscribe();
            return;
        }

        var delta = (float)framework.UpdateDelta.TotalSeconds;
        if (wait > 0f)
        {
            wait -= delta;
            if (wait > 0f)
            {
                return;
            }

            wait = 0f;
        }

        if (cursor >= queue.Count)
        {
            Stop(ShortcutRunOutcome.Completed);
            return;
        }

        var step = queue[cursor];
        cursor++;
        if (!Execute(step, out var failure))
        {
            Stop(failure);
            return;
        }

        if (cursor >= queue.Count && wait <= 0f)
        {
            Stop(ShortcutRunOutcome.Completed);
        }
    }

    private bool Execute(ShortcutStep step, out ShortcutRunOutcome failure)
    {
        failure = ShortcutRunOutcome.Completed;
        switch (step.Kind)
        {
            case ShortcutStepKind.Wait:
                wait = Math.Clamp(step.Seconds, 0f, MaxWaitSeconds);
                return true;
            case ShortcutStepKind.OpenPlugin:
                if (!PluginCatalog.TryOpenMainUi(step.Text))
                {
                    failure = ShortcutRunOutcome.PluginUnavailable;
                    return false;
                }

                wait = StepGapSeconds;
                return true;
            default:
                return ExecuteCommand(step, out failure);
        }
    }

    private bool ExecuteCommand(ShortcutStep step, out ShortcutRunOutcome failure)
    {
        failure = ShortcutRunOutcome.Completed;
        var line = ShortcutCommandText.Split(step.Text, out var inlineWait);
        if (line.Length == 0)
        {
            wait = Math.Clamp(inlineWait, 0f, MaxWaitSeconds);
            return true;
        }

        if (!ChatSender.TrySendSanitised(line))
        {
            AepLog.Warning($"Shortcut \"{runningName}\" could not send: {line}");
            failure = ShortcutRunOutcome.CommandRejected;
            return false;
        }

        wait = inlineWait > 0f ? Math.Clamp(inlineWait, 0f, MaxWaitSeconds) : StepGapSeconds;
        return true;
    }

    private void Stop(ShortcutRunOutcome outcome)
    {
        var finishedId = runningId;
        var finishedName = runningName;
        runningId = Guid.Empty;
        runningName = string.Empty;
        queue.Clear();
        cursor = 0;
        wait = 0f;
        Unsubscribe();
        if (finishedId != Guid.Empty)
        {
            Finished?.Invoke(finishedId, finishedName, outcome);
        }
    }

    public void Dispose()
    {
        runningId = Guid.Empty;
        queue.Clear();
        Unsubscribe();
    }
}
