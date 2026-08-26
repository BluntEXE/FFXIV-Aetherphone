namespace Aetherphone.Core.Animation;

internal readonly struct LayerTransform
{
    private const uint ColorMask = 0x00FFFFFF;
    private const int AlphaShift = 24;

    public readonly float Scale;
    public readonly Vector2 Anchor;
    public readonly Vector2 Target;
    public readonly Rect Clip;
    public readonly float Alpha;

    public LayerTransform(float scale, Vector2 anchor, Vector2 target, Rect clip, float alpha)
    {
        Scale = scale;
        Anchor = anchor;
        Target = target;
        Clip = clip;
        Alpha = Math.Clamp(alpha, 0f, 1f);
    }

    public static LayerTransform Identity(Rect clip) => new(1f, Vector2.Zero, Vector2.Zero, clip, 1f);

    public static LayerTransform Translate(Vector2 offset, Rect clip) => new(1f, Vector2.Zero, offset, clip, 1f);

    public static LayerTransform ScaleAbout(Vector2 pivot, float scale, Rect clip, float alpha = 1f) =>
        new(scale, pivot, pivot, clip, alpha);

    public static LayerTransform Fit(Rect source, Rect target, Rect clip, float alpha = 1f)
    {
        var scale = source.Width > 0f ? target.Width / source.Width : 1f;
        return new LayerTransform(scale, source.Min, target.Min, clip, alpha);
    }

    public static LayerTransform Cover(Rect source, Rect target, Rect clip, float alpha = 1f)
    {
        var widthScale = source.Width > 0f ? target.Width / source.Width : 1f;
        var heightScale = source.Height > 0f ? target.Height / source.Height : 1f;
        return new LayerTransform(MathF.Max(widthScale, heightScale), source.Center, target.Center, clip, alpha);
    }

    public bool IsIdentity => Scale == 1f && Anchor == Target && Alpha >= 1f;

    public Vector2 Map(Vector2 point) => Target + (point - Anchor) * Scale;

    public Rect Map(Rect rect) => new(Map(rect.Min), Map(rect.Max));

    public Vector4 MapClip(Vector4 clip)
    {
        var min = Map(new Vector2(clip.X, clip.Y));
        var max = Map(new Vector2(clip.Z, clip.W));
        var left = MathF.Max(min.X, Clip.Min.X);
        var top = MathF.Max(min.Y, Clip.Min.Y);
        var right = MathF.Min(max.X, Clip.Max.X);
        var bottom = MathF.Min(max.Y, Clip.Max.Y);
        if (right <= left || bottom <= top)
        {
            return new Vector4(left, top, left, top);
        }

        return new Vector4(left, top, right, bottom);
    }

    public uint MapColor(uint color)
    {
        if (Alpha >= 1f)
        {
            return color;
        }

        var alpha = (uint)((color >> AlphaShift) * Alpha + 0.5f);
        return (color & ColorMask) | (alpha << AlphaShift);
    }
}
