using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class BetComposer
{
    public const float FieldHeight = 40f;

    public const float QuickHeight = 32f;

    public const float ConfirmHeight = 46f;

    private const int BufferLength = 8;
    private const float RowGap = 8f;
    private const float StepperWidth = 40f;
    private const float FlashSeconds = 0.5f;

    private readonly string fieldId;

    private string buffer = string.Empty;
    private long amount;
    private float clampFlash;

    public BetComposer(string fieldId)
    {
        this.fieldId = fieldId;
    }

    public long Amount => amount;

    public static long Ceiling(long maximumBet, long stack)
    {
        var ceiling = maximumBet < stack ? maximumBet : stack;
        return ceiling < 0 ? 0 : ceiling;
    }

    public static long Clamp(long value, long minimumBet, long maximumBet, long stack)
    {
        var ceiling = Ceiling(maximumBet, stack);
        if (ceiling < minimumBet)
        {
            return 0;
        }

        if (value < minimumBet)
        {
            return minimumBet;
        }

        return value > ceiling ? ceiling : value;
    }

    public static long Half(long minimumBet, long maximumBet, long stack)
    {
        return Clamp(Ceiling(maximumBet, stack) / 2, minimumBet, maximumBet, stack);
    }

    public static float HeightFor(float scale)
    {
        return (FieldHeight + QuickHeight + ConfirmHeight + RowGap * 2f) * scale;
    }

    public void Reset(long value)
    {
        amount = value;
        buffer = value > 0 ? value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        clampFlash = 0f;
    }

    public void Prefill(long value)
    {
        if (buffer.Length > 0)
        {
            return;
        }

        Reset(value);
    }

    public bool Draw(AppSkin skin, in Rect bounds, long minimumBet, long maximumBet, long stack, long step,
        bool enabled, string confirmLabel, float deltaSeconds)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        if (clampFlash > 0f)
        {
            clampFlash -= deltaSeconds;
        }

        var typed = Parse();
        amount = Clamp(typed, minimumBet, maximumBet, stack);
        var ceiling = Ceiling(maximumBet, stack);
        var y = bounds.Min.Y;

        var stepperWidth = StepperWidth * scale;
        var fieldMin = new Vector2(bounds.Min.X + stepperWidth + RowGap * scale, y);
        var fieldMax = new Vector2(bounds.Max.X - stepperWidth - RowGap * scale, y + FieldHeight * scale);
        if (DrawStepper(skin, new Rect(new Vector2(bounds.Min.X, y), new Vector2(bounds.Min.X + stepperWidth,
                fieldMax.Y)), "-", enabled))
        {
            Reset(Clamp(amount - step, minimumBet, maximumBet, stack));
        }

        DrawField(drawList, skin, fieldMin, fieldMax, scale, enabled);

        if (DrawStepper(skin, new Rect(new Vector2(bounds.Max.X - stepperWidth, y), new Vector2(bounds.Max.X,
                fieldMax.Y)), "+", enabled))
        {
            Reset(Clamp(amount + step, minimumBet, maximumBet, stack));
        }

        y = fieldMax.Y + RowGap * scale;
        var quickHeight = QuickHeight * scale;
        var quickWidth = (bounds.Width - RowGap * scale * 2f) / 3f;
        if (DrawQuick(skin, new Rect(new Vector2(bounds.Min.X, y),
                new Vector2(bounds.Min.X + quickWidth, y + quickHeight)), Loc.T(L.Casino.BetMin), enabled))
        {
            Reset(Clamp(minimumBet, minimumBet, maximumBet, stack));
        }

        var middleLeft = bounds.Min.X + quickWidth + RowGap * scale;
        if (DrawQuick(skin, new Rect(new Vector2(middleLeft, y),
                new Vector2(middleLeft + quickWidth, y + quickHeight)), Loc.T(L.Casino.BetHalf), enabled))
        {
            Reset(Half(minimumBet, maximumBet, stack));
        }

        if (DrawQuick(skin, new Rect(new Vector2(bounds.Max.X - quickWidth, y),
                new Vector2(bounds.Max.X, y + quickHeight)), Loc.T(L.Casino.BetMax), enabled))
        {
            Reset(Clamp(ceiling, minimumBet, maximumBet, stack));
        }

        if (typed > 0 && typed != amount)
        {
            clampFlash = FlashSeconds;
        }

        y += quickHeight + RowGap * scale;
        var confirmRect = new Rect(new Vector2(bounds.Min.X, y),
            new Vector2(bounds.Max.X, y + ConfirmHeight * scale));
        var confirmEnabled = enabled && amount >= minimumBet && amount <= ceiling;
        return AppSkin.PillButton(confirmRect, confirmLabel, true, confirmEnabled, skin.Theme);
    }

    private void DrawField(ImDrawListPtr drawList, AppSkin skin, Vector2 min, Vector2 max, float scale, bool enabled)
    {
        var rounding = Metrics.Radius.Field * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(skin.FieldSurface));
        if (clampFlash > 0f)
        {
            var strength = clampFlash / FlashSeconds;
            Squircle.Stroke(drawList, min, max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(skin.Accent, strength)), Metrics.Stroke.Ring * scale);
        }

        if (!enabled)
        {
            Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f),
                buffer.Length > 0 ? buffer : "0", skin.MutedInk, TextStyles.Subheadline);
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X + 10f * scale,
            (min.Y + max.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(max.X - min.X - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, skin.TitleInk))
        {
            ImGui.InputText(fieldId, ref buffer, BufferLength,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }
    }

    private static bool DrawStepper(AppSkin skin, in Rect rect, string label, bool enabled)
    {
        var pressed = skin.GhostButton(rect, label);
        return enabled && pressed;
    }

    private static bool DrawQuick(AppSkin skin, in Rect rect, string label, bool enabled)
    {
        var pressed = skin.GhostButton(rect, label);
        return enabled && pressed;
    }

    private long Parse()
    {
        return long.TryParse(buffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
