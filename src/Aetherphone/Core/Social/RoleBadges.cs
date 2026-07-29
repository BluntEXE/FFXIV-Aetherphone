using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Core.Social;

[Flags]
internal enum AccountBadges
{
    None = 0,
    Verified = 1 << 0,
    Patreon = 1 << 1,
}

internal readonly record struct RoleBadge(RoleKind Kind, FontAwesomeIcon Glyph, LocString Tooltip);

internal static class RoleBadges
{
    private static readonly RoleBadge[] ByPrecedence =
    {
        new(RoleKind.Verified, FontAwesomeIcon.CheckCircle, L.Social.RoleVerified),
        new(RoleKind.Patreon, FontAwesomeIcon.Star, L.Social.RolePatreon),
    };

    public static RoleBadge? Top(int badges)
    {
        var held = (AccountBadges)badges;
        for (var index = 0; index < ByPrecedence.Length; index++)
        {
            if (Held(held, ByPrecedence[index].Kind))
            {
                return ByPrecedence[index];
            }
        }

        return null;
    }

    public static int Count(int badges)
    {
        var held = (AccountBadges)badges;
        var count = 0;
        for (var index = 0; index < ByPrecedence.Length; index++)
        {
            if (Held(held, ByPrecedence[index].Kind))
            {
                count++;
            }
        }

        return count;
    }

    public static RoleBadge At(int badges, int position)
    {
        var held = (AccountBadges)badges;
        var seen = 0;
        for (var index = 0; index < ByPrecedence.Length; index++)
        {
            if (!Held(held, ByPrecedence[index].Kind))
            {
                continue;
            }

            if (seen == position)
            {
                return ByPrecedence[index];
            }

            seen++;
        }

        return default;
    }

    private static bool Held(AccountBadges badges, RoleKind kind)
    {
        return kind switch
        {
            RoleKind.Verified => (badges & AccountBadges.Verified) != 0,
            RoleKind.Patreon => (badges & AccountBadges.Patreon) != 0,
            _ => false,
        };
    }
}
