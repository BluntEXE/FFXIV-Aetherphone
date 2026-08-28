using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed class DealerBubble
{
    private const float HoldSeconds = 2.5f;
    private const float FadeSeconds = 0.4f;
    private const float EntranceSmoothing = 0.09f;
    private const float PadX = 12f;
    private const float PadY = 7f;

    private string message = string.Empty;
    private float remaining;
    private Spring entrance = new(0f);

    public bool Visible => message.Length > 0;

    public void Show(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (string.Equals(message, text, StringComparison.Ordinal) && remaining > 0f)
        {
            return;
        }

        message = text;
        remaining = HoldSeconds + FadeSeconds;
        entrance.SnapTo(0f);
    }

    public void Clear()
    {
        message = string.Empty;
        remaining = 0f;
        entrance.SnapTo(0f);
    }

    public void Update(float deltaSeconds)
    {
        if (message.Length == 0)
        {
            return;
        }

        remaining -= deltaSeconds;
        if (remaining <= 0f)
        {
            Clear();
            return;
        }

        entrance.Step(1f, EntranceSmoothing, deltaSeconds);
    }

    public void Draw(ImDrawListPtr drawList, Vector2 anchor, AppSkin skin, float scale)
    {
        if (message.Length == 0)
        {
            return;
        }

        var fade = remaining < FadeSeconds ? remaining / FadeSeconds : 1f;
        var alpha = fade * Math.Clamp(entrance.Value, 0f, 1f);
        if (alpha <= 0.01f)
        {
            return;
        }

        var size = Typography.Measure(message, TextStyles.Subheadline);
        var half = new Vector2(size.X * 0.5f + PadX * scale, size.Y * 0.5f + PadY * scale);
        var min = anchor - half;
        var max = anchor + half;
        var rounding = half.Y;
        Material.Frosted(drawList, min, max, rounding, scale, alpha * 0.9f);
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(skin.Accent, alpha * 0.35f)), Metrics.Stroke.Hairline * scale);
        Typography.DrawCentered(drawList, anchor, message, Palette.WithAlpha(skin.TitleInk, alpha),
            TextStyles.Subheadline);
    }
}
