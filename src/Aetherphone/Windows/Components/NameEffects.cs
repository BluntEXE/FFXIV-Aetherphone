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
    private const double GlyphPeriod = 5200.0;

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

        var crest = RoleInk.Highlight(badge.Colors[0], light);
        var phase = Phase(badge.Effect);
        if (Decorrelated(badge.Effect))
        {
            phase = Fraction(phase + Seed(badge.Id));
        }

        if (UsesRamp(badge.Effect))
        {
            return new TextEffect(badge.Effect, crest, phase, RampFrom(badge.Colors, light));
        }

        return new TextEffect(badge.Effect, crest, phase);
    }

    public static float GlyphPhase(BadgeStyle badge)
    {
        if (badge.Effect == NameEffectKind.None || badge.Colors.Length <= 1)
        {
            return 0f;
        }

        return Fraction(Pulse.Phase(GlyphPeriod) + Seed(badge.Id));
    }

    private static bool UsesRamp(NameEffectKind kind)
    {
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

        return new WaveRamp(
            RoleInk.For(colors[0], light),
            RoleInk.For(colors[1 % colors.Length], light),
            RoleInk.For(colors[2 % colors.Length], light),
            RoleInk.For(colors[3 % colors.Length], light));
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
            _ => 0f,
        };
    }
}
