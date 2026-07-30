using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CaseArt
{
    private const uint Opaque = 0xFFFFFFFFu;

    public static bool IsLandscape(Rect body) => body.Width > body.Height;

    public static void Quad(ImDrawListPtr drawList, ImTextureID texture, Rect body, bool landscape, uint tint)
    {
        if (!landscape)
        {
            drawList.AddImage(texture, body.Min, body.Max, Vector2.Zero, Vector2.One, tint);
            return;
        }

        var topRight = new Vector2(body.Max.X, body.Min.Y);
        var bottomLeft = new Vector2(body.Min.X, body.Max.Y);
        drawList.AddImageQuad(texture, body.Min, topRight, body.Max, bottomLeft, new Vector2(0f, 1f), Vector2.Zero,
            new Vector2(1f, 0f), Vector2.One, tint);
    }

    public static void QuadExcluding(ImDrawListPtr drawList, ImTextureID texture, Rect body, Rect exclude,
        bool landscape)
    {
        Clipped(drawList, texture, body, new Rect(body.Min, new Vector2(body.Max.X, exclude.Min.Y)), landscape);
        Clipped(drawList, texture, body, new Rect(new Vector2(body.Min.X, exclude.Max.Y), body.Max), landscape);
        Clipped(drawList, texture, body,
            new Rect(new Vector2(body.Min.X, exclude.Min.Y), new Vector2(exclude.Min.X, exclude.Max.Y)), landscape);
        Clipped(drawList, texture, body,
            new Rect(new Vector2(exclude.Max.X, exclude.Min.Y), new Vector2(body.Max.X, exclude.Max.Y)), landscape);
    }

    public static uint Tint(float alpha) =>
        alpha >= 1f ? Opaque : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));

    private static void Clipped(ImDrawListPtr drawList, ImTextureID texture, Rect body, Rect clip, bool landscape)
    {
        if (clip.Width <= 0.5f || clip.Height <= 0.5f)
        {
            return;
        }

        drawList.PushClipRect(clip.Min, clip.Max, true);
        Quad(drawList, texture, body, landscape, Opaque);
        drawList.PopClipRect();
    }
}
