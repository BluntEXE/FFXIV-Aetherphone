using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class ActionReveal<TPanel> where TPanel : struct, Enum
{
    private const float OpenSeconds = 0.22f;
    private const float CloseSeconds = 0.14f;

    private string? targetId;
    private TPanel current;
    private bool closing;
    private float progress;
    private int openedFrame;

    public string? TargetId => targetId;
    public TPanel Current => current;
    public float Progress => progress;
    public bool Closing => closing;
    public int OpenedFrame => openedFrame;
    public bool IsOpen => !EqualityComparer<TPanel>.Default.Equals(current, default);

    public bool IsShowing(string id, TPanel panel) =>
        EqualityComparer<TPanel>.Default.Equals(current, panel) && targetId == id;

    public void Open(string id, TPanel panel)
    {
        if (targetId != id || !EqualityComparer<TPanel>.Default.Equals(current, panel))
        {
            progress = 0f;
        }

        targetId = id;
        current = panel;
        closing = false;
        openedFrame = ImGui.GetFrameCount();
    }

    public void Dismiss()
    {
        if (IsOpen)
        {
            closing = true;
        }
    }

    public void Reset()
    {
        targetId = null;
        current = default;
        closing = false;
        progress = 0f;
    }

    public void Tick(float deltaSeconds)
    {
        if (!IsOpen)
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

    public void DismissOnOutsideClick(Vector2 min, Vector2 max)
    {
        if (closing || ImGui.GetFrameCount() == openedFrame)
        {
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !UiInteract.HoverWindowOnly(min, max, false))
        {
            Dismiss();
        }
    }
}
