using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Animation;

internal readonly struct HostWindowFootprint
{
    private readonly ImGuiWindowPtr window;
    private readonly Vector2 cursorMaxPosition;
    private readonly Vector2 idealMaxPosition;

    private HostWindowFootprint(ImGuiWindowPtr window, Vector2 cursorMaxPosition, Vector2 idealMaxPosition)
    {
        this.window = window;
        this.cursorMaxPosition = cursorMaxPosition;
        this.idealMaxPosition = idealMaxPosition;
    }

    public static HostWindowFootprint Capture()
    {
        var window = ImGuiP.GetCurrentWindowRead();
        return new HostWindowFootprint(window, window.DC.CursorMaxPos, window.DC.IdealMaxPos);
    }

    public void Restore()
    {
        window.DC.CursorMaxPos = cursorMaxPosition;
        window.DC.IdealMaxPos = idealMaxPosition;
    }

    public void Restore(Vector2 footprintMax)
    {
        window.DC.CursorMaxPos = Vector2.Max(cursorMaxPosition, footprintMax);
        window.DC.IdealMaxPos = Vector2.Max(idealMaxPosition, footprintMax);
    }
}
