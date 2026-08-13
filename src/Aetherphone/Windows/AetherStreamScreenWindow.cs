using Aetherphone.Core.Video;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class AetherStreamScreenWindow : Window
{
    private const float SourceAspect = (float)VideoEngine.ScreenWidth / VideoEngine.ScreenHeight;

    private readonly ScreenController screen;
    private readonly VideoPlayer video;

    internal AetherStreamScreenWindow(ScreenController screen, VideoPlayer video)
        : base("AetherStream Screen###AetherStreamScreenWindow")
    {
        this.screen = screen;
        this.video = video;
        Size = new Vector2(640f, 360f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(160f, 90f), MaximumSize = new Vector2(4096f, 4096f),
        };
        IsOpen = false;
    }

    public override void Draw()
    {
        var handle = screen.Engine.ScreenViewHandle;
        if (handle == nint.Zero || !video.HasMedia)
        {
            ImGui.TextDisabled("Nothing playing.");
            return;
        }

        var available = ImGui.GetContentRegionAvail();
        var drawWidth = available.X;
        var drawHeight = drawWidth / SourceAspect;
        if (drawHeight > available.Y && available.Y > 0f)
        {
            drawHeight = available.Y;
            drawWidth = drawHeight * SourceAspect;
        }

        var offsetX = (available.X - drawWidth) * 0.5f;
        if (offsetX > 0f)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
        }

        ImGui.Image(new ImTextureID(handle), new Vector2(drawWidth, drawHeight));
    }
}
