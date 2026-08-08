namespace Aetherphone.Core.Theme;

// Fills are sampled from an OKLCH ring: 13 hues at least 22 degrees apart, every one solved to relative
// luminance 0.285 so the whole set carries a white glyph at 3.13:1 and hue stays the only variable between
// tiles. Chroma is 94 percent of the sRGB gamut edge at that luminance, which is why cyan and teal read
// softer than red or orange: the gamut simply holds less color there. Nothing here may be brightened
// without breaking white ink, so regenerate rather than hand-edit; see docs/design-accents.md.
internal static class AccentRing
{
    public const float TileLuminance = 0.285f;

    public static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);

    public static readonly Vector4 Rose = new(0.978f, 0.333f, 0.536f, 1f);
    public static readonly Vector4 Red = new(0.977f, 0.362f, 0.324f, 1f);
    public static readonly Vector4 Orange = new(0.883f, 0.453f, 0.114f, 1f);
    public static readonly Vector4 Gold = new(0.746f, 0.531f, 0.114f, 1f);
    public static readonly Vector4 Lime = new(0.503f, 0.612f, 0.113f, 1f);
    public static readonly Vector4 Green = new(0.128f, 0.658f, 0.215f, 1f);
    public static readonly Vector4 Emerald = new(0.130f, 0.645f, 0.490f, 1f);
    public static readonly Vector4 Teal = new(0.130f, 0.634f, 0.616f, 1f);
    public static readonly Vector4 Cyan = new(0.129f, 0.623f, 0.715f, 1f);
    public static readonly Vector4 Azure = new(0.122f, 0.589f, 0.944f, 1f);
    public static readonly Vector4 Indigo = new(0.446f, 0.540f, 0.975f, 1f);
    public static readonly Vector4 Violet = new(0.654f, 0.472f, 0.975f, 1f);
    public static readonly Vector4 Orchid = new(0.925f, 0.258f, 0.974f, 1f);
    public static readonly Vector4 Slate = new(0.541f, 0.561f, 0.612f, 1f);

    public static readonly Vector4 Fallback = Slate;
}
