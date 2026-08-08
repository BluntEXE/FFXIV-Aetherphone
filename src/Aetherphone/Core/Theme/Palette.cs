namespace Aetherphone.Core.Theme;

internal static class Palette
{
    private const float MinInkContrast = 3.0f;
    private const float InkDarkening = 0.78f;

    private static readonly Vector4 WhiteInk = new(1f, 1f, 1f, 1f);

    public static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };
    public static Vector4 Mix(Vector4 from, Vector4 to, float amount) => Vector4.Lerp(from, to, amount);
    public static float Luminance(Vector4 color) => color.X * 0.299f + color.Y * 0.587f + color.Z * 0.114f;

    public static float RelativeLuminance(Vector4 color) =>
        0.2126f * LinearChannel(color.X) + 0.7152f * LinearChannel(color.Y) + 0.0722f * LinearChannel(color.Z);

    public static float ContrastRatio(Vector4 first, Vector4 second)
    {
        var one = RelativeLuminance(first);
        var other = RelativeLuminance(second);
        return one > other ? (one + 0.05f) / (other + 0.05f) : (other + 0.05f) / (one + 0.05f);
    }

    // Scaling in gamma space keeps the hue and lets every app backdrop land at the same darkness no matter
    // how luminous its accent is, which a fixed Darken factor cannot do (yellow would sit far brighter than blue).
    public static Vector4 ShadeToLuminance(Vector4 color, float target)
    {
        var luminance = RelativeLuminance(color);
        if (luminance <= target || luminance <= 0f)
        {
            return color;
        }

        var factor = MathF.Pow(target / luminance, 1f / 2.4f);
        return new Vector4(color.X * factor, color.Y * factor, color.Z * factor, color.W);
    }

    public static Vector4 ReadableInk(Vector4 surface)
    {
        if (ContrastRatio(surface, WhiteInk) >= MinInkContrast)
        {
            return WhiteInk;
        }

        return Darken(surface, InkDarkening) with { W = 1f };
    }

    private static float LinearChannel(float channel) =>
        channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);

    public static Vector4 Lighten(Vector4 color, float amount) =>
        Vector4.Lerp(color, new Vector4(1f, 1f, 1f, color.W), amount);

    public static Vector4 Darken(Vector4 color, float amount) =>
        Vector4.Lerp(color, new Vector4(0f, 0f, 0f, color.W), amount);
}
