namespace Aetherphone.Core.Theme;

internal enum RoleKind
{
    None = 0,
    Verified,
    Patreon,
}

internal static class RoleInk
{
    private static readonly Vector4 VerifiedDark = new(0.40f, 0.68f, 0.98f, 1.00f);
    private static readonly Vector4 VerifiedLight = new(0.05f, 0.42f, 0.85f, 1.00f);
    private static readonly Vector4 PatreonDark = new(0.98f, 0.76f, 0.35f, 1.00f);
    private static readonly Vector4 PatreonLight = new(0.66f, 0.42f, 0.02f, 1.00f);
    private static readonly Vector4 NeutralDark = new(0.97f, 0.97f, 0.98f, 1.00f);
    private static readonly Vector4 NeutralLight = new(0.10f, 0.10f, 0.11f, 1.00f);

    public static bool IsLight(PhoneTheme theme) => Palette.Luminance(theme.AppBackground) > 0.5f;

    public static Vector4 For(RoleKind kind, bool light)
    {
        return kind switch
        {
            RoleKind.Verified => light ? VerifiedLight : VerifiedDark,
            RoleKind.Patreon => light ? PatreonLight : PatreonDark,
            _ => light ? NeutralLight : NeutralDark,
        };
    }

    public static Vector4 For(RoleKind kind, PhoneTheme theme) => For(kind, IsLight(theme));
}
