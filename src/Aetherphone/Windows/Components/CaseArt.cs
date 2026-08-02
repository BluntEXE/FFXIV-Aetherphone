using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CaseArt
{
    private const uint Opaque = 0xFFFFFFFFu;

    public const float MarginFraction = 0.25f;

    public static bool IsLandscape(Rect body) => body.Width > body.Height;

    public static Rect RectFor(Rect body)
    {
        var margin = body.Width * MarginFraction;
        return new Rect(body.Min - new Vector2(margin, margin), body.Max + new Vector2(margin, margin));
    }

    public static void Quad(ImDrawListPtr drawList, ImTextureID texture, Rect art, bool landscape, uint tint)
    {
        drawList.PushClipRectFullScreen();
        QuadClipped(drawList, texture, art, landscape, tint);
        drawList.PopClipRect();
    }

    public static void QuadClipped(ImDrawListPtr drawList, ImTextureID texture, Rect art, bool landscape, uint tint)
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

        drawList.PushClipRect(clip.Min, clip.Max, false);
        QuadClipped(drawList, texture, art, landscape, Opaque);
        drawList.PopClipRect();
    }
}
