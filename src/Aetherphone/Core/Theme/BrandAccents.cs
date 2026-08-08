namespace Aetherphone.Core.Theme;

// Brand identities that predate the accent ring and are deliberately kept off it. They still clear the
// 3:1 white-glyph floor, so tiles stay uniform in ink, but they do not honour the ring's hue spacing:
// Velvet and Aethergram sit close together on purpose. Do not fold these into AccentRing.
internal static class BrandAccents
{
    public static readonly Vector4 Chirper = new(0.16f, 0.52f, 0.94f, 1f);
    public static readonly Vector4 Velvet = new(0.898f, 0.102f, 0.357f, 1f);
    public static readonly Vector4 Aethergram = new(0.92f, 0.30f, 0.38f, 1f);
}
