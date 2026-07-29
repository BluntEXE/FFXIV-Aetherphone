using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RoleBadgeTests
{
    private const int None = 0;
    private const int Verified = (int)AccountBadges.Verified;
    private const int Patreon = (int)AccountBadges.Patreon;
    private const int Both = Verified | Patreon;

    [Fact]
    public void AnUnbadgedAccountHasNothingToDraw()
    {
        Assert.Null(RoleBadges.Top(None));
        Assert.Equal(0, RoleBadges.Count(None));
    }

    [Fact]
    public void EachBadgeResolvesToItsOwnKind()
    {
        Assert.Equal(RoleKind.Verified, RoleBadges.Top(Verified)!.Value.Kind);
        Assert.Equal(RoleKind.Patreon, RoleBadges.Top(Patreon)!.Value.Kind);
    }

    [Fact]
    public void VerifiedOutranksPatreonWhenBothAreHeld()
    {
        Assert.Equal(RoleKind.Verified, RoleBadges.Top(Both)!.Value.Kind);
        Assert.Equal(2, RoleBadges.Count(Both));
        Assert.Equal(RoleKind.Verified, RoleBadges.At(Both, 0).Kind);
        Assert.Equal(RoleKind.Patreon, RoleBadges.At(Both, 1).Kind);
    }

    [Fact]
    public void APatreonOnlyAccountIsIndexedFromZero()
    {
        Assert.Equal(1, RoleBadges.Count(Patreon));
        Assert.Equal(RoleKind.Patreon, RoleBadges.At(Patreon, 0).Kind);
    }

    [Fact]
    public void UnknownBitsAreIgnored()
    {
        const int future = 1 << 7;
        Assert.Equal(0, RoleBadges.Count(future));
        Assert.Null(RoleBadges.Top(future));
        Assert.Equal(RoleKind.Verified, RoleBadges.Top(Verified | future)!.Value.Kind);
        Assert.Equal(1, RoleBadges.Count(Verified | future));
    }

    [Fact]
    public void EveryBadgeCarriesAGlyphAndATooltipKey()
    {
        foreach (var badges in new[] { Verified, Patreon })
        {
            var badge = RoleBadges.Top(badges)!.Value;
            Assert.NotEqual(default, badge.Glyph);
            Assert.False(string.IsNullOrWhiteSpace(badge.Tooltip.Key));
        }
    }

    [Fact]
    public void RoleColorsDifferBetweenLightAndDarkSoBothStayLegible()
    {
        foreach (var kind in new[] { RoleKind.Verified, RoleKind.Patreon })
        {
            var dark = RoleInk.For(kind, false);
            var light = RoleInk.For(kind, true);
            Assert.NotEqual(dark, light);
            Assert.True(Palette.Luminance(dark) > Palette.Luminance(light));
        }
    }
}
