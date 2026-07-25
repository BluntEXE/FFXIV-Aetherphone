using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Video;

// Draws the video directly onto a world-anchored quad via ImGui's background draw list - no game
// object, no Penumbra, no native hooks. Each frame: project the placement's four world corners to
// screen space via IGameGui.WorldToScreen, then draw as an image quad at those points.
//
// This is the confirmed-working version, reverted back to from a real depth-tested D3D11 render
// (see ScreenRenderer/ScreenDeviceHandler, kept in the tree but unused for now) - that version
// never got past "can't confirm it renders at all" given zero ability to test it locally, and a
// screen you can actually see beats one that's theoretically correctly occluded but invisible.
// The known tradeoff: ImGui's draw list has no depth buffer involvement, so this always draws on
// top of everything at its screen position regardless of what's actually nearer the camera.
internal sealed class ScreenPainter
{
    private readonly VideoPlayer video;
    private readonly ScreenPlacement placement;
    private IDalamudTextureWrap? texture;

    public ScreenPainter(VideoPlayer video, ScreenPlacement placement)
    {
        this.video = video;
        this.placement = placement;
    }

    public void Draw()
    {
        if (!placement.IsPlaced)
        {
            return;
        }

        var (topLeft, topRight, bottomRight, bottomLeft) = placement.ComputeCorners();
        if (!Plugin.GameGui.WorldToScreen(topLeft, out var screenTopLeft, out _) ||
            !Plugin.GameGui.WorldToScreen(topRight, out var screenTopRight, out _) ||
            !Plugin.GameGui.WorldToScreen(bottomRight, out var screenBottomRight, out _) ||
            !Plugin.GameGui.WorldToScreen(bottomLeft, out var screenBottomLeft, out _))
        {
            // Any corner behind the camera - skip this frame rather than draw a degenerate quad.
            // Being outside the viewport (as opposed to behind the camera) is fine; ImGui clips
            // that naturally.
            return;
        }

        var drawList = ImGui.GetBackgroundDrawList();

        // Always visible once placed, even with nothing playing.
        drawList.AddQuadFilled(screenTopLeft, screenTopRight, screenBottomRight, screenBottomLeft, 0xFF000000u);

        var frame = video.TryGetFrame(out var width, out var height);
        if (frame is null || width <= 0 || height <= 0)
        {
            return;
        }

        texture?.Dispose();
        texture = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Bgra32(width, height), frame,
            "Aetherphone.AetherStream.ScreenPainter");

        drawList.AddImageQuad(texture.Handle, screenTopLeft, screenTopRight, screenBottomRight, screenBottomLeft);
    }

    public void Dispose()
    {
        texture?.Dispose();
        texture = null;
    }
}
