using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Theme;

internal readonly struct ChassisGeometry
{
    private const float CapsuleMetalWidth = 3f;
    private const float CapsuleGlassWidth = 4f;

    public readonly Rect Body;
    public readonly Rect Glass;
    public readonly Rect Screen;
    public readonly float BodyRadius;
    public readonly float GlassRadius;
    public readonly float ScreenRadius;

    private ChassisGeometry(Rect body, float bodyRadius, float metalWidth, float glassWidth)
    {
        var snapped = Snap(body);
        var limit = MathF.Max(MathF.Min(snapped.Width, snapped.Height) * 0.5f, 0f);
        var metal = SnapBand(metalWidth, limit);
        var glass = SnapBand(glassWidth, limit - metal);
        Body = snapped;
        Glass = snapped.Inset(metal);
        Screen = Glass.Inset(glass);
        BodyRadius = Math.Clamp(bodyRadius, 0f, limit);
        GlassRadius = MathF.Max(BodyRadius - metal, 0f);
        ScreenRadius = MathF.Max(GlassRadius - glass, 0f);
    }

    public static ChassisGeometry Device(Rect window, PhoneTheme theme, float scale)
    {
        var body = BodyRect(window, theme, scale);
        return new ChassisGeometry(body, theme.DeviceRounding * scale, theme.MetalWidth * scale,
            theme.GlassWidth * scale);
    }

    public static Rect BodyRect(Rect window, PhoneTheme theme, float scale)
    {
        var rail = theme.RailWidth * scale;
        if (window.IsLandscape())
        {
            return new Rect(new Vector2(window.Min.X, window.Min.Y + rail),
                new Vector2(window.Max.X, window.Max.Y - rail));
        }

        return new Rect(new Vector2(window.Min.X + rail, window.Min.Y),
            new Vector2(window.Max.X - rail, window.Max.Y));
    }

    public static ChassisGeometry Capsule(Rect body, float scale) =>
        new(body, MathF.Min(body.Width, body.Height), CapsuleMetalWidth * scale, CapsuleGlassWidth * scale);

    public static float CapsuleBand(float scale) =>
        (MathF.Max(MathF.Round(CapsuleMetalWidth * scale), 1f) + MathF.Max(MathF.Round(CapsuleGlassWidth * scale), 1f)) *
        2f;

    public static ChassisGeometry Morph(Rect body, PhoneTheme theme, float deviceScale, float capsuleScale,
        float eased) =>
        new(body, Easing.Lerp(theme.DeviceRounding * deviceScale, CapsuleRounding(body), eased),
            Easing.Lerp(theme.MetalWidth * deviceScale, CapsuleMetalWidth * capsuleScale, eased),
            Easing.Lerp(theme.GlassWidth * deviceScale, CapsuleGlassWidth * capsuleScale, eased));

    public static ChassisGeometry Preview(Rect body, PhoneCaseKind kind)
    {
        var metrics = ChassisMetrics.ForBody(kind, body.Width);
        return new ChassisGeometry(body, metrics.DeviceRounding, metrics.MetalWidth, metrics.GlassWidth);
    }

    private static float CapsuleRounding(Rect body) => MathF.Min(body.Width, body.Height) * 0.5f;

    private static float SnapBand(float width, float limit)
    {
        if (width <= 0f || limit <= 0f)
        {
            return 0f;
        }

        return MathF.Min(MathF.Max(MathF.Round(width), 1f), limit);
    }

    private static Rect Snap(Rect rect) =>
        new(new Vector2(MathF.Round(rect.Min.X), MathF.Round(rect.Min.Y)),
            new Vector2(MathF.Round(rect.Max.X), MathF.Round(rect.Max.Y)));
}
