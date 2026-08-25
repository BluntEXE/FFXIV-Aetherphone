using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class PhoneWindow : Window
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
                                               ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                                               ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground;

    private const int RecenterFrameCount = 3;
    private const int ScaledStyleVarCount = 6;
    private const float RotateSeconds = 0.26f;
    private readonly PhoneShell shell;
    private readonly Configuration configuration;
    private int recenterFrames;
    private int pendingFrames;
    private float landscapeBlend;
    private int rotatePinFrames;
    private bool dockGrowLeft;
    private bool dockGrowUp;
    private Vector2? pendingPosition;
    private Vector2? maximizedPosition;
    private Vector2? minimizedPosition;

    public PhoneWindow(PhoneShell shell, Configuration configuration)
        : base(AepConstants.Name, BaseFlags)
    {
        this.shell = shell;
        this.configuration = configuration;
        Size = PhoneSizeCatalog.SizeFor(configuration.PhoneWidth);
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
        maximizedPosition = configuration.MaximizedPosition;
        minimizedPosition = configuration.MinimizedPosition;
    }

    public bool IsMinimized => shell.MinimizedResting;

    public Vector2 LastPosition { get; private set; }

    public Vector2 LastSize { get; private set; }

    public bool ShowsChrome => IsOpen && shell.MinimizePhase == MinimizePhase.None && LastSize.Y > 0f;

    public void Maximize()
    {
        RequestPosition(maximizedPosition);
        shell.ForceMaximize();
    }

    public void StartMinimized()
    {
        RequestPosition(minimizedPosition);
        shell.ForceMinimized();
    }

    public void PersistPositions()
    {
        if (configuration.MaximizedPosition == maximizedPosition && configuration.MinimizedPosition == minimizedPosition)
        {
            return;
        }

        configuration.MaximizedPosition = maximizedPosition;
        configuration.MinimizedPosition = minimizedPosition;
        configuration.SaveNow();
    }

    public void Recenter()
    {
        shell.ForceMaximize();
        recenterFrames = RecenterFrameCount;
        pendingFrames = 0;
        minimizedPosition = null;
        IsOpen = true;
    }

    public void ToggleShell()
    {
        if (IsOpen)
        {
            IsOpen = false;
            return;
        }

        Maximize();
        IsOpen = true;
    }

    public void OpenSettings()
    {
        Maximize();
        IsOpen = true;
        shell.OpenApp("settings");
    }

    private void RequestPosition(Vector2? target)
    {
        if (target is not { } position)
        {
            return;
        }

        pendingPosition = position;
        pendingFrames = RecenterFrameCount;
    }

    public override void OnOpen()
    {
        shell.OnOpened();
    }

    public override void OnClose()
    {
        PersistPositions();
        shell.OnClosed();
    }

    public override void PreDraw()
    {
        var portraitWidth = Components.PhoneBounds.ClampWidth(configuration.PhoneWidth);
        var landscapeWidth = Components.PhoneBounds.LandscapeWidth(configuration);
        var rotation = AdvanceRotation();
        shell.PrepareFrame(MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        var phase = shell.MinimizePhase;
        var minimized = phase == MinimizePhase.Minimized;
        var zoom = minimized ? 1f : PhoneSizeCatalog.ZoomFor(float.Lerp(portraitWidth, landscapeWidth, rotation));
        UiScale.SetPhone(zoom);
        Plugin.Fonts.SetPhoneZoom(zoom);
        var dockSize = shell.MinimizedSize;
        var size = minimized
            ? dockSize / UiScale.Global
            : OrientedSize(portraitWidth, landscapeWidth, rotation);
        Size = size;
        SizeCondition = ImGuiCond.Always;
        var locked = !minimized && configuration.LockPosition;
        var holdStill = !minimized && (shell.HomeEditing || Components.UiInteract.PointerOverGestureSurface);
        Flags = minimized || locked || holdStill
            ? BaseFlags | ImGuiWindowFlags.NoMove
            : BaseFlags;
        Components.DragScrollHost.Enabled = locked;

        if (recenterFrames > 0)
        {
            var viewport = ImGui.GetMainViewport();
            var scaledSize = size * UiScale.Global;
            Position = viewport.Pos + (viewport.Size - scaledSize) * 0.5f;
            PositionCondition = ImGuiCond.Always;
            recenterFrames--;
        }
        else if (pendingFrames > 0 && pendingPosition is { } pendingTarget)
        {
            Position = pendingTarget;
            PositionCondition = ImGuiCond.Always;
            pendingFrames--;
        }
        else if (minimized)
        {
            Position = DockedPosition(dockSize);
            PositionCondition = ImGuiCond.Always;
        }
        else if (phase is MinimizePhase.Collapsing or MinimizePhase.Expanding &&
                 maximizedPosition is { } homePosition)
        {
            Position = Vector2.Lerp(homePosition, DockedPosition(dockSize), shell.MinimizeEased);
            PositionCondition = ImGuiCond.Always;
        }
        else if (!minimized && rotatePinFrames > 0 && LastSize.Y > 0f)
        {
            rotatePinFrames--;
            Position = CenterPinnedPosition(size);
            PositionCondition = ImGuiCond.Always;
        }
        else
        {
            Position = null;
            pendingFrames = 0;
        }

        PushScaledStyle(zoom);
    }

    public override void PostDraw() => ImGui.PopStyleVar(ScaledStyleVarCount);

    private static void PushScaledStyle(float zoom)
    {
        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, style.FramePadding * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, style.ItemInnerSpacing * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, style.ScrollbarSize * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, style.GrabMinSize * zoom);
    }

    private float AdvanceRotation()
    {
        var target = shell.LandscapeActive ? 1f : 0f;
        if (landscapeBlend == target)
        {
            return Easing.SmootherStep(landscapeBlend);
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var step = delta / RotateSeconds;
        landscapeBlend = target > landscapeBlend
            ? MathF.Min(target, landscapeBlend + step)
            : MathF.Max(target, landscapeBlend - step);
        rotatePinFrames = RecenterFrameCount;
        return Easing.SmootherStep(landscapeBlend);
    }

    private static Vector2 OrientedSize(float portraitWidth, float landscapeWidth, float rotation)
    {
        var portrait = PhoneSizeCatalog.SizeFor(portraitWidth);
        if (rotation <= 0f)
        {
            return portrait;
        }

        var landscape = PhoneSizeCatalog.LandscapeSizeFor(landscapeWidth);
        return Vector2.Lerp(portrait, landscape, rotation);
    }

    private Vector2 DockedPosition(Vector2 dockSize)
    {
        var viewport = ImGui.GetMainViewport();
        var drag = shell.ConsumeMinimizedDrag();
        var idle = shell.MinimizedIdleSize;
        var extra = Vector2.Max(dockSize - idle, Vector2.Zero);
        if (minimizedPosition is not { } anchor)
        {
            anchor = maximizedPosition ?? LastPosition;
            dockGrowLeft = PastCenterX(anchor.X + idle.X * 0.5f, viewport);
            dockGrowUp = PastCenterY(anchor.Y + idle.Y * 0.5f, viewport);
        }

        anchor += drag.Delta;
        if (drag.Released)
        {
            var visual = LastPosition;
            dockGrowLeft = PastCenterX(visual.X + dockSize.X * 0.5f, viewport);
            dockGrowUp = PastCenterY(visual.Y + dockSize.Y * 0.5f, viewport);
            anchor = new Vector2(visual.X + (dockGrowLeft ? extra.X : 0f), visual.Y + (dockGrowUp ? extra.Y : 0f));
        }

        anchor = ClampToViewport(anchor, idle, viewport);
        minimizedPosition = anchor;
        var position = new Vector2(dockGrowLeft ? anchor.X - extra.X : anchor.X,
            dockGrowUp ? anchor.Y - extra.Y : anchor.Y);
        return ClampToViewport(position, dockSize, viewport);
    }

    private static bool PastCenterX(float x, ImGuiViewportPtr viewport) =>
        x > viewport.Pos.X + viewport.Size.X * 0.5f;

    private static bool PastCenterY(float y, ImGuiViewportPtr viewport) =>
        y > viewport.Pos.Y + viewport.Size.Y * 0.5f;

    private Vector2 CenterPinnedPosition(Vector2 size)
    {
        var scaledSize = size * UiScale.Global;
        var center = LastPosition + LastSize * 0.5f;
        return ClampToViewport(center - scaledSize * 0.5f, scaledSize, ImGui.GetMainViewport());
    }

    private static Vector2 ClampToViewport(Vector2 position, Vector2 size, ImGuiViewportPtr viewport)
    {
        var maxPosition = viewport.Pos + viewport.Size - size;
        return new Vector2(Math.Clamp(position.X, viewport.Pos.X, MathF.Max(viewport.Pos.X, maxPosition.X)),
            Math.Clamp(position.Y, viewport.Pos.Y, MathF.Max(viewport.Pos.Y, maxPosition.Y)));
    }

    public override void Draw()
    {
        LastPosition = ImGui.GetWindowPos();
        LastSize = ImGui.GetWindowSize();
        Components.UiInteract.SetWindowHovered(ImGui.IsWindowHovered(
            ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem));
        Components.UiInteract.SetWindowFocused(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows));
        Plugin.Updates.Poll();
        using (Plugin.Fonts.Push(1f))
        {
            var origin = ImGui.GetCursorScreenPos();
            var available = ImGui.GetContentRegionAvail();
            ImGui.Dummy(available);
            var device = new Rect(origin, origin + available);
            shell.Draw(device);
        }

        if (shell.MinimizePhase == MinimizePhase.None)
        {
            maximizedPosition = ImGui.GetWindowPos();
        }

        if (shell.ConsumeCloseRequest())
        {
            IsOpen = false;
        }
    }
}
