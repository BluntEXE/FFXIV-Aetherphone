using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class JackpotRail
{
    public const float Height = 116f;

    private const float Rounding = 24f;
    private const float Inset = 18f;
    private const float AmountMaxScale = 2.30f;
    private const float AmountMinScale = 1.15f;
    private const int BulbsPerSide = 7;
    private const float BulbRadius = 1.9f;
    private const float BulbInset = 9f;
    private const double ChaseMilliseconds = 2200.0;
    private const float MeterHeight = 4f;
    private const float MeterSmoothSeconds = 0.5f;

    private static readonly Vector4 Ink = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Bed = new(0.180f, 0.075f, 0.020f, 1f);
    private static readonly Vector4 Crown = new(0.420f, 0.180f, 0.045f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);
    private static readonly Vector4 Ember = new(1f, 0.62f, 0.24f, 1f);

    private RollingValue potRoll;
    private Spring meterFill;

    public void Snap(long coins)
    {
        potRoll.Snap((int)coins);
        meterFill.SnapTo(0f);
    }

    public bool Draw(AppSkin ui, long chips)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = Height * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = Rounding * scale;
        var hovered = UiInteract.Hover(min, max);

        Elevation.Card(drawList, min, max, rounding, scale, 0.95f);
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(Crown), ImGui.GetColorU32(Bed));
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Gold, 0.45f)), Metrics.Stroke.Thin * scale);
        DrawBulbs(drawList, min, max, scale);

        var inset = Inset * scale;
        var left = min.X + inset;
        var eyebrow = Loc.T(L.Casino.JackpotEyebrow);
        Typography.Draw(drawList, new Vector2(left, min.Y + 14f * scale), eyebrow,
            Palette.WithAlpha(Gold, 0.90f), TextStyles.Caption1);

        var coins = CasinoChipLots.CoinsFor(chips);
        potRoll.Update((int)coins, delta);
        var amountText = NumberText.Group(potRoll.Display);
        var available = width - inset * 2f;
        var tallestHeight = Typography.Measure(amountText, AmountMaxScale, FontWeight.Bold).Y;
        var restScale = Typography.FitScale(amountText, available - CurrencyGlyph.Reserve(tallestHeight),
            AmountMaxScale, AmountMinScale, FontWeight.Bold);
        var restHeight = Typography.Measure(amountText, restScale, FontWeight.Bold).Y;
        var glyphSize = restHeight * CurrencyGlyph.GlyphFraction;
        var reserve = CurrencyGlyph.Reserve(restHeight);
        var poppedScale = restScale * potRoll.PopScale;
        var poppedSize = Typography.Measure(amountText, poppedScale, FontWeight.Bold);
        var amountBottom = min.Y + 34f * scale + restHeight;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins,
            new Vector2(left + glyphSize * 0.5f, amountBottom - restHeight * 0.5f), glyphSize);
        Typography.Draw(drawList, new Vector2(left + reserve, amountBottom - poppedSize.Y), amountText, Gold,
            poppedScale, FontWeight.Bold);

        var unit = Loc.T(L.Casino.JackpotUnit);
        var unitSize = Typography.Measure(unit, TextStyles.Subheadline);
        Typography.Draw(drawList,
            new Vector2(left + reserve + poppedSize.X + 8f * scale, amountBottom - unitSize.Y), unit,
            Palette.WithAlpha(Gold, 0.75f), TextStyles.Subheadline);

        var hint = Typography.FitText(Loc.T(L.Casino.JackpotHint), available, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left, amountBottom + 6f * scale), hint,
            Palette.WithAlpha(Ink, 0.72f), TextStyles.Footnote);

        DrawMeter(drawList, min, max, coins, delta, scale);
        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var clicked = UiInteract.Click(min, max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        return clicked;
    }

    private static void DrawBulbs(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale)
    {
        var phase = Pulse.Phase(ChaseMilliseconds);
        var radius = BulbRadius * scale;
        var inset = BulbInset * scale;
        var span = max.X - min.X - inset * 2f;
        for (var index = 0; index < BulbsPerSide; index++)
        {
            var slide = (index + 0.5f) / BulbsPerSide;
            var lit = Lit(slide, phase);
            var color = ImGui.GetColorU32(Palette.WithAlpha(Vector4.Lerp(Ember, Gold, lit), 0.25f + 0.60f * lit));
            var x = min.X + inset + span * slide;
            drawList.AddCircleFilled(new Vector2(x, min.Y + inset), radius, color, 10);
            drawList.AddCircleFilled(new Vector2(x, max.Y - inset), radius, color, 10);
        }
    }

    private static float Lit(float slide, float phase)
    {
        var distance = MathF.Abs(slide - phase);
        if (distance > 0.5f)
        {
            distance = 1f - distance;
        }

        var reach = 1f - distance * 4f;
        return reach < 0f ? 0f : reach;
    }

    private void DrawMeter(ImDrawListPtr drawList, Vector2 min, Vector2 max, long coins, float delta, float scale)
    {
        var span = CasinoChipLots.JackpotCapCoins - CasinoChipLots.JackpotSeedCoins;
        if (span <= 0)
        {
            return;
        }

        var filled = coins - CasinoChipLots.JackpotSeedCoins;
        var target = Math.Clamp((float)filled / span, 0f, 1f);
        var shown = Math.Clamp(meterFill.Step(target, MeterSmoothSeconds, delta), 0f, 1f);
        var thickness = MeterHeight * scale;
        var inset = Inset * scale;
        var trackMin = new Vector2(min.X + inset, max.Y - inset - thickness);
        var trackMax = new Vector2(max.X - inset, max.Y - inset);
        Squircle.Fill(drawList, trackMin, trackMax, thickness * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(Ink, 0.14f)));
        if (shown <= 0f)
        {
            return;
        }

        var fillMax = new Vector2(trackMin.X + (trackMax.X - trackMin.X) * shown, trackMax.Y);
        Squircle.Fill(drawList, trackMin, fillMax, thickness * 0.5f, ImGui.GetColorU32(Gold));
    }
}
