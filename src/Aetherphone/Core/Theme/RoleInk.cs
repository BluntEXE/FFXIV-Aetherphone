namespace Aetherphone.Core.Theme;

internal enum RoleKind
{
    None = 0,
    Management,
    Developer,
    Moderator,
    Verified,
    Patreon,
    Support,
    Contributor,
    Aide,
}

internal static class RoleInk
{
    private static readonly Vector4 ManagementBase = new(0.4275f, 0.2039f, 0.4118f, 1.00f);
    private static readonly Vector4 ModeratorBase = new(0.8784f, 0.4627f, 0.1490f, 1.00f);
    private static readonly Vector4 DeveloperBase = new(0.2706f, 0.2706f, 0.6667f, 1.00f);
    private static readonly Vector4 SupportBase = new(0.0000f, 1.0000f, 0.4471f, 1.00f);
    private static readonly Vector4 PatreonBase = new(0.9098f, 0.1216f, 0.3843f, 1.00f);
    private static readonly Vector4 VerifiedBase = new(0.2039f, 0.5961f, 0.8588f, 1.00f);
    private static readonly Vector4 ContributorBase = new(0.1843f, 0.7647f, 0.4392f, 1.00f);
    private static readonly Vector4 AideBase = new(0.9804f, 0.5373f, 0.7137f, 1.00f);
    private static readonly Vector4 AideCrestBase = new(1.0000f, 1.0000f, 1.0000f, 1.00f);
    private static readonly Vector4 AideTroughBase = new(0.3569f, 0.8078f, 0.9804f, 1.00f);
    private static readonly Vector4 NeutralDark = new(0.97f, 0.97f, 0.98f, 1.00f);
    private static readonly Vector4 NeutralLight = new(0.10f, 0.10f, 0.11f, 1.00f);

    private const float MinDarkLuminance = 0.42f;
    private const float MaxLightLuminance = 0.34f;
    private const float DarkHighlightLift = 1.22f;
    private const float LightHighlightLift = 0.72f;

    public static bool IsLight(PhoneTheme theme) => Palette.Luminance(theme.AppBackground) > 0.5f;

    public static Vector4 For(RoleKind kind, bool light)
    {
        var brand = Brand(kind);
        if (brand.W <= 0f)
        {
            return light ? NeutralLight : NeutralDark;
        }

        return light ? Darkened(brand, MaxLightLuminance) : Lifted(brand, MinDarkLuminance);
    }

    public static Vector4 For(RoleKind kind, PhoneTheme theme) => For(kind, IsLight(theme));

    public static Vector4 Highlight(RoleKind kind, bool light)
    {
        var fill = For(kind, light);
        var lift = light ? LightHighlightLift : DarkHighlightLift;
        return new Vector4(MathF.Min(1f, fill.X * lift), MathF.Min(1f, fill.Y * lift), MathF.Min(1f, fill.Z * lift),
            fill.W);
    }

    public static Vector4 Highlight(RoleKind kind, PhoneTheme theme) => Highlight(kind, IsLight(theme));

    public static Vector4 WaveCrest(RoleKind kind, bool light)
    {
        if (kind != RoleKind.Aide)
        {
            return Highlight(kind, light);
        }

        return light ? Darkened(AideCrestBase, MaxLightLuminance) : AideCrestBase;
    }

    public static Vector4 WaveTrough(RoleKind kind, bool light)
    {
        if (kind != RoleKind.Aide)
        {
            return For(kind, light);
        }

        return light ? Darkened(AideTroughBase, MaxLightLuminance) : Lifted(AideTroughBase, MinDarkLuminance);
    }

    private static Vector4 Brand(RoleKind kind)
    {
        return kind switch
        {
            RoleKind.Management => ManagementBase,
            RoleKind.Moderator => ModeratorBase,
            RoleKind.Developer => DeveloperBase,
            RoleKind.Support => SupportBase,
            RoleKind.Patreon => PatreonBase,
            RoleKind.Verified => VerifiedBase,
            RoleKind.Contributor => ContributorBase,
            RoleKind.Aide => AideBase,
            _ => default,
        };
    }

    private static Vector4 Lifted(Vector4 color, float floor)
    {
        var luminance = Palette.Luminance(color);
        if (luminance >= floor)
        {
            return color;
        }

        var scale = floor / MathF.Max(luminance, 0.0001f);
        return new Vector4(MathF.Min(1f, color.X * scale), MathF.Min(1f, color.Y * scale),
            MathF.Min(1f, color.Z * scale), color.W);
    }

    private static Vector4 Darkened(Vector4 color, float ceiling)
    {
        var luminance = Palette.Luminance(color);
        if (luminance <= ceiling)
        {
            return color;
        }

        var scale = ceiling / MathF.Max(luminance, 0.0001f);
        return new Vector4(color.X * scale, color.Y * scale, color.Z * scale, color.W);
    }
}
