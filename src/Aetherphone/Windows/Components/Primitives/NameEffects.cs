using Aetherphone.Core.Animation;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class NameEffects
{
    private const double BreathPeriod = Pulse.Breath;
    private const double SweepPeriod = Pulse.Orbit;
    private const double GlintPeriod = 3000.0;
    private const double FlowPeriod = 4200.0;
    private const double RipplePeriod = 3600.0;
    private const double WavePeriod = 2400.0;
    private const double EmberPeriod = 2100.0;
    private const double FrostPeriod = 4600.0;
    private const double AuroraPeriod = 6500.0;
    private const double PrismPeriod = 1900.0;
    private const double GlitchPeriod = 2400.0;
    private const double StarfallPeriod = 2600.0;
    private const double EclipsePeriod = 3800.0;
    private const double HeartbeatPeriod = 2200.0;
    private const double PulsePeriod = 4800.0;

    public static TextEffect For(RoleKind role, bool light)
    {
        var kind = KindFor(role);
        if (kind == NameEffectKind.None)
        {
            return default;
        }

        if (kind == NameEffectKind.Wave)
        {
            return new TextEffect(kind, RoleInk.Highlight(role, light), Phase(kind), RoleInk.Ramp(role, light));
        }

        return new TextEffect(kind, RoleInk.Highlight(role, light), Phase(kind));
    }

    public static TextEffect For(BadgeStyle badge, bool light)
    {
        if (badge.Effect == NameEffectKind.None)
        {
            return default;
        }

        var crest = RoleInk.Highlight(badge.Colors[badge.Colors.Length > 1 ? 1 : 0], light);
        var phase = Phase(badge.Effect);
        if (Decorrelated(badge.Effect))
        {
            phase = Fraction(phase + Seed(badge.Id));
        }

        if (UsesRamp(badge.Effect, badge.Colors.Length))
        {
            return new TextEffect(badge.Effect, crest, phase, RampFrom(badge.Colors, light));
        }

        return new TextEffect(badge.Effect, crest, phase);
    }

    private static bool UsesRamp(NameEffectKind kind, int colorCount)
    {
        if (kind == NameEffectKind.Gradient || kind == NameEffectKind.Pulse)
        {
            return colorCount > 1;
        }

        return kind == NameEffectKind.Wave
            || kind == NameEffectKind.Aurora
            || kind == NameEffectKind.Prism
            || kind == NameEffectKind.Glitch;
    }

    private static bool Decorrelated(NameEffectKind kind)
    {
        return kind == NameEffectKind.Glitch || kind == NameEffectKind.Starfall;
    }

    private static float Seed(string badgeId)
    {
        var hash = 2166136261u;
        for (var index = 0; index < badgeId.Length; index++)
        {
            hash = (hash ^ badgeId[index]) * 16777619u;
        }

        return (hash % 1000u) / 1000f;
    }

    private static float Fraction(float value) => value - MathF.Floor(value);

    private static WaveRamp RampFrom(Vector4[] colors, bool light)
    {
        if (colors.Length == 1)
        {
            var fill = RoleInk.For(colors[0], light);
            var crest = RoleInk.Highlight(colors[0], light);
            return new WaveRamp(fill, crest, fill, crest);
        }

        if (colors.Length == 2)
        {
            var first = RoleInk.For(colors[0], light);
            var second = RoleInk.For(colors[1], light);
            return new WaveRamp(first, second, first, second);
        }

        Span<Vector4> stops = stackalloc Vector4[WaveRamp.MaxStops];
        var count = Math.Min(colors.Length, WaveRamp.MaxStops);
        for (var stopIndex = 0; stopIndex < count; stopIndex++)
        {
            stops[stopIndex] = RoleInk.For(colors[stopIndex], light);
        }

        return new WaveRamp(stops[..count]);
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
            RoleKind.Aide => NameEffectKind.Wave,
            RoleKind.Aurelia => NameEffectKind.Wave,
            RoleKind.Verified => NameEffectKind.Gradient,
            _ => NameEffectKind.None,
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
            NameEffectKind.Wave => Pulse.Phase(WavePeriod),
            NameEffectKind.Ember => Pulse.Phase(EmberPeriod),
            NameEffectKind.Frost => Pulse.Phase(FrostPeriod),
            NameEffectKind.Aurora => Pulse.Phase(AuroraPeriod),
            NameEffectKind.Prism => Pulse.Phase(PrismPeriod),
            NameEffectKind.Glitch => Pulse.Phase(GlitchPeriod),
            NameEffectKind.Starfall => Pulse.Phase(StarfallPeriod),
            NameEffectKind.Eclipse => Pulse.Phase(EclipsePeriod),
            NameEffectKind.Heartbeat => Pulse.Phase(HeartbeatPeriod),
            NameEffectKind.Pulse => Pulse.Phase(PulsePeriod),
            _ => 0f,
        };
    }
}
