using System.Numerics;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NameEffectSyncTests
{
    private static readonly string[] ServerEffectKeys =
    {
        "none", "gradient", "breath", "ripple", "flow", "glint", "sweep", "wave",
        "ember", "frost", "aurora", "prism", "glitch", "starfall", "eclipse", "heartbeat",
    };

    private static readonly string[] SellableEffectKeys =
    {
        "flow", "ember", "frost", "aurora", "prism", "glitch", "starfall", "eclipse", "heartbeat",
    };

    private static BadgeStyle Badge(string effect, params string[] colors) =>
        BadgeStyle.From(new BadgeDescriptorDto("shop-flair-" + effect, effect, "0xF06D", string.Empty, string.Empty,
            colors, effect));

    [Fact]
    public void EveryEffectKeyTheServerCanSendResolvesToItsOwnKind()
    {
        var seen = new Dictionary<NameEffectKind, string>();
        for (var index = 0; index < ServerEffectKeys.Length; index++)
        {
            var key = ServerEffectKeys[index];
            var kind = Badge(key, "0xFF8A3D").Effect;
            if (key != "none")
            {
                Assert.True(kind != NameEffectKind.None, key + " fell through to None");
            }

            Assert.False(seen.ContainsKey(kind), key + " collides with " + seen.GetValueOrDefault(kind));
            seen[kind] = key;
        }

        Assert.Equal(ServerEffectKeys.Length, seen.Count);
    }

    [Fact]
    public void EveryKindTheClientKnowsHasAServerKey()
    {
        foreach (NameEffectKind kind in Enum.GetValues<NameEffectKind>())
        {
            var matched = false;
            for (var index = 0; index < ServerEffectKeys.Length; index++)
            {
                if (Badge(ServerEffectKeys[index], "0xFF8A3D").Effect == kind)
                {
                    matched = true;
                    break;
                }
            }

            Assert.True(matched, kind + " has no server key, so it can never reach the client");
        }
    }

    [Fact]
    public void EverySellableEffectAnimates()
    {
        for (var index = 0; index < SellableEffectKeys.Length; index++)
        {
            var key = SellableEffectKeys[index];
            var effect = NameEffects.For(Badge(key, "0xFF8A3D", "0x0000EF"), false);
            Assert.True(effect.Kind != NameEffectKind.None, key + " produced no effect");
            Assert.True(effect.Crest.W > 0f, key + " has a transparent crest");
        }
    }

    [Fact]
    public void TheSecondColourDrivesTheAccentSoBothColoursShowUp()
    {
        var twoTone = NameEffects.For(Badge("flow", "0xFF8A3D", "0x0000EF"), false);
        var accentOnly = RoleInk.Highlight(new Vector4(0f, 0f, 0xEF / 255f, 1f), false);
        Assert.Equal(accentOnly.X, twoTone.Crest.X, 3);
        Assert.Equal(accentOnly.Y, twoTone.Crest.Y, 3);
        Assert.Equal(accentOnly.Z, twoTone.Crest.Z, 3);
    }

    [Fact]
    public void ASingleColourBadgeStillGetsAReadableCrest()
    {
        var single = NameEffects.For(Badge("ember", "0xFF8A3D"), false);
        var expected = RoleInk.Highlight(new Vector4(1f, 0x8A / 255f, 0x3D / 255f, 1f), false);
        Assert.Equal(expected.X, single.Crest.X, 3);
        Assert.Equal(expected.Y, single.Crest.Y, 3);
        Assert.Equal(expected.Z, single.Crest.Z, 3);
    }

    [Fact]
    public void RampEffectsCarryAllFourStops()
    {
        var ramped = NameEffects.For(Badge("aurora", "0xFF0000", "0x00FF00", "0x0000FF", "0xFFFF00"), false);
        Assert.NotEqual(ramped.Ramp.Start, ramped.Ramp.Quarter);
        Assert.NotEqual(ramped.Ramp.Quarter, ramped.Ramp.Half);
        Assert.NotEqual(ramped.Ramp.Half, ramped.Ramp.ThreeQuarter);
    }

    [Fact]
    public void NoSellableEffectIsClaimedByARole()
    {
        foreach (RoleKind role in Enum.GetValues<RoleKind>())
        {
            var roleEffect = NameEffects.KindFor(role);
            for (var index = 0; index < SellableEffectKeys.Length; index++)
            {
                var sellable = Badge(SellableEffectKeys[index], "0xFF8A3D").Effect;
                Assert.True(roleEffect != sellable,
                    SellableEffectKeys[index] + " is sold but " + role + " already wears it");
            }
        }
    }

    [Fact]
    public void GlitchAndStarfallStaggerAcrossBadgesSoAFeedDoesNotFireInLockstep()
    {
        var first = BadgeStyle.From(new BadgeDescriptorDto("shop-flair-a", "A", "0xF06D", string.Empty, string.Empty,
            new[] { "0xFF8A3D" }, "glitch"));
        var second = BadgeStyle.From(new BadgeDescriptorDto("shop-flair-b", "B", "0xF06D", string.Empty, string.Empty,
            new[] { "0xFF8A3D" }, "glitch"));
        Assert.NotEqual(NameEffects.For(first, false).Phase, NameEffects.For(second, false).Phase);
    }
}
