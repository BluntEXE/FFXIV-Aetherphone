using Aetherphone.Core.Animation;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed class MinimizeMorphView
{
    private const float FaceFadeStart = 0.55f;
    private const float FaceFadeEnd = 0.95f;

    private readonly ThemeProvider themes;
    private readonly MinimizeTransition minimize;
    private readonly MinimizedCapsule capsule;
    private readonly ShellScreenPainter painter;

    public MinimizeMorphView(ThemeProvider themes, MinimizeTransition minimize, MinimizedCapsule capsule,
        ShellScreenPainter painter)
    {
        this.themes = themes;
        this.minimize = minimize;
        this.capsule = capsule;
        this.painter = painter;
    }

    public bool Draw(Rect device, float delta)
    {
        if (minimize.MorphActive)
        {
            DrawMorph(device, delta);
            return false;
        }

        return DrawResting(device, delta);
    }

    private void DrawMorph(Rect device, float delta)
    {
        var scale = UiScale.Current;
        var theme = themes.Chrome;
        var capsuleScale = UiScale.Global;
        var startBody = DeviceChrome.BodyRect(device, theme);
        var endBody = MinimizedRect(device, capsuleScale);
        var eased = minimize.EasedProgress;
        var body = new Rect(Vector2.Lerp(startBody.Min, endBody.Min, eased),
            Vector2.Lerp(startBody.Max, endBody.Max, eased));
        var geometry = ChassisGeometry.Morph(body, theme, scale, capsuleScale, eased);

        var shell = ImGui.GetWindowDrawList();
        Elevation.Squircle(shell, geometry.Body.Min, geometry.Body.Max, geometry.BodyRadius, scale, eased);
        DeviceChrome.DrawShell(shell, geometry, scale, theme, 1f);
        RevealMorphContent(DeviceChrome.Chassis(device, theme), theme, geometry, eased);

        var faceAlpha = Easing.SmoothStep(Easing.Segment(eased, FaceFadeStart, FaceFadeEnd));
        capsule.DrawFace(ImGui.GetForegroundDrawList(), geometry, theme, delta, false, faceAlpha);
    }

    private void RevealMorphContent(in ChassisGeometry device, PhoneTheme theme, in ChassisGeometry geometry,
        float eased)
    {
        var screen = geometry.Screen;
        if (screen.Height <= 0.5f)
        {
            return;
        }

        var fullScreen = device.Screen;
        var fullRadius = device.ScreenRadius;
        var rounding = geometry.ScreenRadius;
        var veil = ImGui.GetColorU32(Palette.WithAlpha(theme.ScreenBase, eased));
        var shrink = ShrinkMotion(fullScreen, screen);
        SceneCompositor.DrawClipped(screen, fullScreen, 0f, target =>
        {
            painter.PaintCurrent(target, fullRadius, theme, shrink);
            Squircle.Fill(ImGui.GetWindowDrawList(), screen.Min, screen.Max, rounding, veil);
        });
        DeviceChrome.MaskScreenCorners(ImGui.GetWindowDrawList(), geometry, theme, UiScale.Current);
    }

    private static HomeMotion ShrinkMotion(Rect fullScreen, Rect target)
    {
        var zoom = fullScreen.Width > 0f ? target.Width / fullScreen.Width : 1f;
        if (zoom >= 0.999f)
        {
            return new HomeMotion(1f, default, 0f, false);
        }

        var pivot = (target.Min - fullScreen.Min * zoom) / (1f - zoom);
        return new HomeMotion(zoom, pivot, 0f, false);
    }

    private bool DrawResting(Rect device, float delta)
    {
        switch (capsule.Draw(device, themes.Chrome, delta))
        {
            case MinimizedAction.Expand:
                minimize.BeginExpand();
                break;
            case MinimizedAction.Close:
                return true;
        }

        return false;
    }

    private Rect MinimizedRect(Rect device, float scale) => new(device.Min, device.Min + capsule.Measure(scale));
}
