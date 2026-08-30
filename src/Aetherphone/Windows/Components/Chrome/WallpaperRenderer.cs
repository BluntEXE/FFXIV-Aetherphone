using Aetherphone.Core;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class WallpaperRenderer
{
    public static void Draw(ImDrawListPtr drawList, Rect shape, Rect quad, float radius, WallpaperEntry light,
        WallpaperEntry dark, float aspect, float darkness, Vector4 fallback, float blur)
    {
        DrawSingle(drawList, shape, quad, radius, light, aspect, 1f, fallback);
        if (darkness > 0.001f)
        {
            DrawSingle(drawList, shape, quad, radius, dark, aspect, darkness, null);
        }

        if (blur <= 0.001f)
        {
            return;
        }

        DrawBlurred(drawList, shape, quad, light, aspect, blur);
        if (darkness > 0.001f)
        {
            DrawBlurred(drawList, shape, quad, dark, aspect, blur * darkness);
        }
    }

    private static void DrawBlurred(ImDrawListPtr drawList, Rect shape, Rect quad, WallpaperEntry entry, float aspect,
        float alpha)
    {
        var library = Plugin.Wallpapers;
        if (library.BlurredHandlePath(entry.FilePath) is not { } handle)
        {
            return;
        }

        DrawTexture(drawList, shape, quad, handle, library.BlurredSizeOfPath(entry.FilePath), entry, aspect, alpha);
    }

    public static void DrawSingle(ImDrawListPtr drawList, Rect rect, float radius, WallpaperEntry entry, float aspect,
        float alpha, Vector4? fallback) =>
        DrawSingle(drawList, rect, rect, radius, entry, aspect, alpha, fallback);

    public static void DrawSingle(ImDrawListPtr drawList, Rect shape, Rect quad, float radius, WallpaperEntry entry,
        float aspect, float alpha, Vector4? fallback)
    {
        var library = Plugin.Wallpapers;
        if (library.HandlePath(entry.FilePath) is not { } handle)
        {
            if (fallback is { } color)
            {
                Squircle.Fill(drawList, shape.Min, shape.Max, radius, ImGui.GetColorU32(color));
            }

            return;
        }

        DrawTexture(drawList, shape, quad, handle, library.SizeOfPath(entry.FilePath), entry, aspect, alpha);
    }

    private static void DrawTexture(ImDrawListPtr drawList, Rect shape, Rect quad, ImTextureID handle,
        Vector2 textureSize, WallpaperEntry entry, float aspect, float alpha)
    {
        var (uv0, uv1) = entry.Crop.ComputeUv(textureSize, aspect);
        var tint = alpha >= 1f ? 0xFFFFFFFFu : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        var clipped = quad.Min != shape.Min || quad.Max != shape.Max;
        if (clipped)
        {
            drawList.PushClipRect(shape.Min, shape.Max, true);
        }

        drawList.AddImage(handle, quad.Min, quad.Max, uv0, uv1, tint);
        if (clipped)
        {
            drawList.PopClipRect();
        }
    }
}
