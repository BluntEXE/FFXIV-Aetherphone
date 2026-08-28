using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct ScreenToastStyle(Vector4 Panel, Vector4 Stroke, Vector4 Ink)
{
    public static ScreenToastStyle From(PhoneTheme theme) => new(
        Palette.WithAlpha(Palette.Lighten(theme.AppBackground, 0.10f), 0.92f),
        Palette.WithAlpha(theme.TextStrong, 0.12f),
        theme.TextStrong);

    public static ScreenToastStyle From(AppSkin ui) => new(
        Palette.WithAlpha(Palette.Lighten(ui.Palette.BackdropTop, 0.10f), 0.92f),
        Palette.WithAlpha(ui.TitleInk, 0.12f),
        ui.TitleInk);
}

internal sealed class ScreenToast
{
    private const float LifetimeSeconds = 1.7f;
    private const float PopSeconds = 0.28f;
    private const float FadeSeconds = 0.22f;
    private const float BottomOffset = 96f;
    private const float RiseDistance = 14f;
    private const float MaxFrameSeconds = 0.1f;

    private static readonly TextStyle LabelStyle = new(0.9f, FontWeight.SemiBold);

    private string label = string.Empty;
    private float elapsed = LifetimeSeconds;

    public void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        label = text;
        elapsed = 0f;
    }

    public void Draw(Rect screen, in ScreenToastStyle style)
    {
        if (elapsed >= LifetimeSeconds)
        {
            return;
        }

        elapsed += MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        var pop = Easing.EaseOutQuint(Math.Clamp(elapsed / PopSeconds, 0f, 1f));
        var fadeStart = LifetimeSeconds - FadeSeconds;
        var fade = elapsed > fadeStart ? 1f - Math.Clamp((elapsed - fadeStart) / FadeSeconds, 0f, 1f) : 1f;
        var alpha = pop * fade;
        if (alpha <= 0.001f)
        {
            return;
        }

        var scale = UiScale.Current;
        var grow = 0.9f + 0.1f * pop;
        var padX = 14f * scale;
        var padY = 8f * scale;
        var textSize = Typography.Measure(label, LabelStyle);
        var maxWidth = MathF.Max(1f, screen.Width - 48f * scale);
        var fitted = Typography.FitText(label, maxWidth, LabelStyle);
        textSize = Typography.Measure(fitted, LabelStyle);
        var width = (textSize.X + padX * 2f) * grow;
        var height = (textSize.Y + padY * 2f) * grow;
        var rise = (1f - pop) * RiseDistance * scale;
        var center = new Vector2(screen.Center.X, screen.Max.Y - BottomOffset * scale + rise);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var rounding = height * 0.5f;
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, false);
        Elevation.Floating(drawList, min, max, rounding, scale, alpha);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(style.Panel, style.Panel.W * alpha)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(style.Stroke, style.Stroke.W * alpha)), Metrics.Stroke.Hairline);
        Typography.DrawCentered(drawList, center, fitted, Palette.WithAlpha(style.Ink, style.Ink.W * alpha),
            LabelStyle);
        drawList.PopClipRect();
    }
}
