using System.Numerics;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Interface;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RoleBadgeTests
{
    private const int None = 0;
    private const int Verified = (int)AccountBadges.Verified;
    private const int Patreon = (int)AccountBadges.Patreon;
    private const int Management = (int)AccountBadges.Management;
    private const int Moderator = (int)AccountBadges.Moderator;
    private const int Developer = (int)AccountBadges.Developer;
    private const int Supporter = (int)AccountBadges.Supporter;
    private const int Both = Verified | Patreon;

    private static readonly RoleKind[] ExpectedPrecedence =
    {
        RoleKind.Management, RoleKind.Developer, RoleKind.Moderator,
        RoleKind.Verified, RoleKind.Patreon, RoleKind.Supporter,
    };

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
    public void StaffRolesOutrankIdentityWhichOutranksPerks()
    {
        const int everything = Management | Developer | Moderator | Verified | Patreon | Supporter;

        Assert.Equal(ExpectedPrecedence.Length, RoleBadges.Count(everything));
        for (var index = 0; index < ExpectedPrecedence.Length; index++)
        {
            Assert.Equal(ExpectedPrecedence[index], RoleBadges.At(everything, index).Kind);
        }

        Assert.Equal(RoleKind.Management, RoleBadges.Top(everything)!.Value.Kind);
    }

    [Fact]
    public void EveryBadgeHasItsOwnGlyphAndColour()
    {
        var all = new[] { Management, Developer, Moderator, Verified, Patreon, Supporter };
        var glyphs = new HashSet<FontAwesomeIcon>();
        var darkInks = new HashSet<Vector4>();
        foreach (var badges in all)
        {
            var badge = RoleBadges.Top(badges)!.Value;
            Assert.True(glyphs.Add(badge.Glyph), $"duplicate glyph {badge.Glyph}");
            Assert.True(darkInks.Add(RoleInk.For(badge.Kind, false)), $"duplicate ink for {badge.Kind}");
        }

        Assert.Equal(all.Length, glyphs.Count);
        Assert.Equal(all.Length, darkInks.Count);
    }

    [Fact]
    public void EachFlagMapsToItsOwnKind()
    {
        Assert.Equal(RoleKind.Management, RoleBadges.Top(Management)!.Value.Kind);
        Assert.Equal(RoleKind.Developer, RoleBadges.Top(Developer)!.Value.Kind);
        Assert.Equal(RoleKind.Moderator, RoleBadges.Top(Moderator)!.Value.Kind);
        Assert.Equal(RoleKind.Supporter, RoleBadges.Top(Supporter)!.Value.Kind);
    }

    [Fact]
    public void UnknownBitsAreIgnored()
    {
        const int future = 1 << 9;
        Assert.Equal(0, RoleBadges.Count(future));
        Assert.Null(RoleBadges.Top(future));
        Assert.Equal(RoleKind.Verified, RoleBadges.Top(Verified | future)!.Value.Kind);
        Assert.Equal(1, RoleBadges.Count(Verified | future));
    }

    [Fact]
    public void EveryBadgeCarriesAGlyphAndATooltipKey()
    {
        foreach (var badges in new[] { Management, Developer, Moderator, Verified, Patreon, Supporter })
        {
            var badge = RoleBadges.Top(badges)!.Value;
            Assert.NotEqual(default, badge.Glyph);
            Assert.False(string.IsNullOrWhiteSpace(badge.Tooltip.Key));
        }
    }

    [Fact]
    public void RoleColorsDifferBetweenLightAndDarkSoBothStayLegible()
    {
        foreach (var kind in ExpectedPrecedence)
        {
            var dark = RoleInk.For(kind, false);
            var light = RoleInk.For(kind, true);
            Assert.NotEqual(dark, light);
            Assert.True(Palette.Luminance(dark) > Palette.Luminance(light), $"{kind} dark is not brighter than light");
        }
    }
}
