namespace Aetherphone.Core.Theme;

internal enum RoleKind
{
    None = 0,
    Management,
    Developer,
    Moderator,
    Verified,
    Patreon,
    Supporter,
    Contributor,
}

internal static class RoleInk
{
    private static readonly Vector4 ManagementDark = new(0.78f, 0.58f, 1.00f, 1.00f);
    private static readonly Vector4 ManagementLight = new(0.44f, 0.18f, 0.76f, 1.00f);
    private static readonly Vector4 DeveloperDark = new(0.35f, 0.84f, 0.86f, 1.00f);
    private static readonly Vector4 DeveloperLight = new(0.03f, 0.44f, 0.50f, 1.00f);
    private static readonly Vector4 ModeratorDark = new(0.46f, 0.86f, 0.56f, 1.00f);
    private static readonly Vector4 ModeratorLight = new(0.09f, 0.48f, 0.20f, 1.00f);
    private static readonly Vector4 VerifiedDark = new(0.40f, 0.68f, 0.98f, 1.00f);
    private static readonly Vector4 VerifiedLight = new(0.05f, 0.42f, 0.85f, 1.00f);
    private static readonly Vector4 PatreonDark = new(0.98f, 0.76f, 0.35f, 1.00f);
    private static readonly Vector4 PatreonLight = new(0.66f, 0.42f, 0.02f, 1.00f);
    private static readonly Vector4 SupporterDark = new(0.98f, 0.56f, 0.72f, 1.00f);
    private static readonly Vector4 SupporterLight = new(0.70f, 0.13f, 0.38f, 1.00f);
    private static readonly Vector4 ContributorDark = new(0.76f, 0.85f, 0.40f, 1.00f);
    private static readonly Vector4 ContributorLight = new(0.38f, 0.47f, 0.06f, 1.00f);
    private static readonly Vector4 NeutralDark = new(0.97f, 0.97f, 0.98f, 1.00f);
    private static readonly Vector4 NeutralLight = new(0.10f, 0.10f, 0.11f, 1.00f);

    public static bool IsLight(PhoneTheme theme) => Palette.Luminance(theme.AppBackground) > 0.5f;

    public static Vector4 For(RoleKind kind, bool light)
    {
        return kind switch
        {
            RoleKind.Management => light ? ManagementLight : ManagementDark,
            RoleKind.Developer => light ? DeveloperLight : DeveloperDark,
            RoleKind.Moderator => light ? ModeratorLight : ModeratorDark,
            RoleKind.Verified => light ? VerifiedLight : VerifiedDark,
            RoleKind.Patreon => light ? PatreonLight : PatreonDark,
            RoleKind.Supporter => light ? SupporterLight : SupporterDark,
            RoleKind.Contributor => light ? ContributorLight : ContributorDark,
            _ => light ? NeutralLight : NeutralDark,
        };
    }

    public static Vector4 For(RoleKind kind, PhoneTheme theme) => For(kind, IsLight(theme));
}
