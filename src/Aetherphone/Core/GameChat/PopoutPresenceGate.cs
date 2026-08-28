namespace Aetherphone.Core.GameChat;

internal readonly record struct PresenceSettings(bool HideInCombat, bool HideInDuty, bool FieldOperationsExempt);

internal readonly record struct PresenceState(bool InCombat, bool BoundByDuty, bool InFieldOperation);

internal static class PopoutPresenceGate
{
    public static bool ShouldSuppress(in PresenceState state, in PresenceSettings settings)
    {
        if (settings.HideInCombat && state.InCombat)
        {
            return true;
        }

        if (!settings.HideInDuty || !state.BoundByDuty)
        {
            return false;
        }

        return !settings.FieldOperationsExempt || !state.InFieldOperation;
    }
}

internal struct PresenceDebounce
{
    private float held;

    public bool Value { get; private set; }

    public bool Step(bool target, float deltaSeconds, float delaySeconds)
    {
        if (target == Value)
        {
            held = 0f;
            return false;
        }

        held += deltaSeconds;
        if (held < delaySeconds)
        {
            return false;
        }

        held = 0f;
        Value = target;
        return true;
    }
}
