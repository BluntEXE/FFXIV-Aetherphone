using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed class ChirperActionReveal
{
    internal enum Panel
    {
        None,
        Picker,
        Repost,
    }

    private const float OpenSeconds = 0.22f;
    private const float CloseSeconds = 0.14f;
    private string? postId;
    private Panel current;
    private bool closing;
    private float progress;
    private int openedFrame;
    public string? PostId => postId;
    public Panel Current => current;
    public float Progress => progress;
    public bool Closing => closing;
    public int OpenedFrame => openedFrame;
    public bool IsShowing(string id, Panel panel) => current == panel && postId == id;

    public void Open(string id, Panel panel)
    {
        if (postId != id || current != panel)
        {
            progress = 0f;
        }

        postId = id;
        current = panel;
        closing = false;
        openedFrame = ImGui.GetFrameCount();
    }

    public void Dismiss()
    {
        if (current != Panel.None)
        {
            closing = true;
        }
    }

    public void Reset()
    {
        postId = null;
        current = Panel.None;
        closing = false;
        progress = 0f;
    }

    public void Tick(float deltaSeconds)
    {
        if (current == Panel.None)
        {
            return;
        }

        if (closing)
        {
            progress -= deltaSeconds / CloseSeconds;
            if (progress <= 0f)
            {
                Reset();
            }

            return;
        }

        if (progress < 1f)
        {
            progress = MathF.Min(1f, progress + deltaSeconds / OpenSeconds);
        }
    }
}
