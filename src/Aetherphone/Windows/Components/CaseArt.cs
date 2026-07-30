using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CaseArt
{
    private const uint Opaque = 0xFFFFFFFFu;

    /// <summary>Free space around the phone body, as a fraction of body width, where a case may hang charms,
    /// ears, straps or a figure past the silhouette. Matches the margin baked into the art canvas.</summary>
    public const float MarginFraction = 0.25f;

    public static bool IsLandscape(Rect body) => body.Width > body.Height;

    /// <summary>The art quad: the phone body plus the overflow margin the canvas reserves around it.</summary>
    public static Rect RectFor(Rect body)
    {
        var margin = body.Width * MarginFraction;
        return new Rect(body.Min - new Vector2(margin, margin), body.Max + new Vector2(margin, margin));
    }

    public static void Quad(ImDrawListPtr drawList, ImTextureID texture, Rect art, bool landscape, uint tint)
    {
        // Overflow art lies outside the phone window, which ImGui would otherwise clip away. Escaping to the
        // viewport keeps the window its own size, so the margin costs no screen space and eats no clicks.
        drawList.PushClipRectFullScreen();
        Draw(drawList, texture, art, landscape, tint);
        drawList.PopClipRect();
    }

    private static void Draw(ImDrawListPtr drawList, ImTextureID texture, Rect art, bool landscape, uint tint)
    {
        if (!landscape)
        {
            drawList.AddImage(texture, art.Min, art.Max, Vector2.Zero, Vector2.One, tint);
            return;
        }

        var topRight = new Vector2(art.Max.X, art.Min.Y);
        var bottomLeft = new Vector2(art.Min.X, art.Max.Y);
        drawList.AddImageQuad(texture, art.Min, topRight, art.Max, bottomLeft, new Vector2(0f, 1f), Vector2.Zero,
            new Vector2(1f, 0f), Vector2.One, tint);
    }

    public static void QuadExcluding(ImDrawListPtr drawList, ImTextureID texture, Rect art, Rect exclude,
        bool landscape)
    {
        Clipped(drawList, texture, art, new Rect(art.Min, new Vector2(art.Max.X, exclude.Min.Y)), landscape);
        Clipped(drawList, texture, art, new Rect(new Vector2(art.Min.X, exclude.Max.Y), art.Max), landscape);
        Clipped(drawList, texture, art,
            new Rect(new Vector2(art.Min.X, exclude.Min.Y), new Vector2(exclude.Min.X, exclude.Max.Y)), landscape);
        Clipped(drawList, texture, art,
            new Rect(new Vector2(exclude.Max.X, exclude.Min.Y), new Vector2(art.Max.X, exclude.Max.Y)), landscape);
    }

    public static uint Tint(float alpha) =>
        alpha >= 1f ? Opaque : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));

    private static void Clipped(ImDrawListPtr drawList, ImTextureID texture, Rect art, Rect clip, bool landscape)
    {
        if (clip.Width <= 0.5f || clip.Height <= 0.5f)
        {
            return;
        }

        // Replaces the clip rather than intersecting, so a band region outside the window still draws.
        drawList.PushClipRect(clip.Min, clip.Max, false);
        Draw(drawList, texture, art, landscape, Opaque);
        drawList.PopClipRect();
    }
}
