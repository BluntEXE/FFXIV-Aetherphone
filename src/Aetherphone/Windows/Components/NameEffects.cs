using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class NameEffects
{
    private const double BreathPeriod = Pulse.Breath;
    private const double SweepPeriod = Pulse.Orbit;
    private const double GlintPeriod = 3000.0;
    private const double FlowPeriod = 4200.0;
    private const double RipplePeriod = 3600.0;

    public static TextEffect For(RoleKind role, bool light)
    {
        var assigned = KindFor(role);
        var kind = Motion.Reduced ? Settled(assigned) : assigned;
        if (kind == NameEffectKind.None)
        {
            return default;
        }

        return new TextEffect(kind, RoleInk.Highlight(role, light), Phase(kind));
    }

    public static NameEffectKind KindFor(RoleKind role)
    {
        return role switch
        {
            RoleKind.Management => NameEffectKind.Sweep,
            RoleKind.Patreon => NameEffectKind.Sweep,
            RoleKind.Moderator => NameEffectKind.Glint,
            RoleKind.Developer => NameEffectKind.Ripple,
            RoleKind.Support => NameEffectKind.Breath,
            RoleKind.Verified => NameEffectKind.Gradient,
            _ => NameEffectKind.None,
        };
    }

    private static NameEffectKind Settled(NameEffectKind kind)
    {
        return kind switch
        {
            NameEffectKind.Breath => NameEffectKind.None,
            NameEffectKind.Sweep => NameEffectKind.None,
            NameEffectKind.Glint => NameEffectKind.None,
            NameEffectKind.Flow => NameEffectKind.Gradient,
            NameEffectKind.Ripple => NameEffectKind.Gradient,
            _ => kind,
        };
    }

    private static float Phase(NameEffectKind kind)
    {
        return kind switch
        {
            NameEffectKind.Breath => Pulse.Phase(BreathPeriod),
            NameEffectKind.Sweep => Pulse.Phase(SweepPeriod),
            NameEffectKind.Glint => Pulse.Phase(GlintPeriod),
            NameEffectKind.Flow => Pulse.Phase(FlowPeriod),
            NameEffectKind.Ripple => Pulse.Phase(RipplePeriod),
            _ => 0f,
        };
    }
}
