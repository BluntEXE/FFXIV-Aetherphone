using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal readonly struct GameResult
{
    public readonly string Title;
    public readonly Vector4 TitleColor;
    public readonly string PrimaryLabel;
    public readonly string PrimaryValue;
    public readonly string? SecondaryLine;
    public readonly bool NewBest;
    public readonly string? ButtonLabel;

    public GameResult(string title, Vector4 titleColor, string primaryLabel, string primaryValue, string? secondaryLine,
        bool newBest, string? buttonLabel = null)
    {
        Title = title;
        TitleColor = titleColor;
        PrimaryLabel = primaryLabel;
        PrimaryValue = primaryValue;
        SecondaryLine = secondaryLine;
        NewBest = newBest;
        ButtonLabel = buttonLabel;
    }
}

internal static class GameOverlay
{
    private const float CountUpSeconds = 0.7f;
    private const float CardRadius = 26f;
    private const float MinCardWidth = 260f;
    private const float MaxCardWidth = 300f;
    private const float CardWidthFraction = 0.86f;
    private const float BadgeHeight = 24f;
    private const float ButtonHeight = 42f;
    private const float MinButtonWidth = 150f;
    private const float ButtonSidePadding = 52f;
    private const float MinFitFactor = 0.62f;

    private static readonly ParticleSystem Celebration = new(224);
    private static readonly Vector4[] ConfettiPalette =
    {
        new(0.98f, 0.62f, 0.28f, 1f), new(0.42f, 0.78f, 0.98f, 1f), new(0.62f, 0.90f, 0.46f, 1f),
        new(0.95f, 0.45f, 0.62f, 1f), new(0.80f, 0.62f, 0.98f, 1f), new(0.99f, 0.86f, 0.40f, 1f),
    };

    private static float lastProgress;
    private static double lastDrawTime;
    private static bool celebrated;
    private static float countShown;

    public static bool Draw(Rect area, PhoneTheme theme, Vector4 accent, float progress, in GameResult result)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var now = ImGui.GetTime();
        var clamped = Math.Clamp(progress, 0f, 1f);
        if (now - lastDrawTime > 0.25 || clamped < lastProgress - 0.01f)
        {
            celebrated = false;
            countShown = 0f;
            Celebration.Clear();
        }

        lastDrawTime = now;
        lastProgress = clamped;
        var alpha = MathF.Min(1f, clamped * 1.5f);
        var grow = Easing.EaseOutBack(clamped);
        Material.Veil(drawList, area.Min, area.Max, 0.58f * alpha);

        var padding = Metrics.Space.Xl * scale;
        var label = Loc.Upper(result.PrimaryLabel);
        var buttonLabel = result.ButtonLabel ?? Loc.T(L.Games.PlayAgain);
        var secondary = result.SecondaryLine;
        var hasSecondary = !string.IsNullOrEmpty(secondary);
        var buttonWidth = MathF.Max(MinButtonWidth * scale,
            Typography.Measure(buttonLabel, TextStyles.Headline).X + ButtonSidePadding * scale);
        var widest = MathF.Max(buttonWidth, Typography.Measure(result.Title, TextStyles.Title1).X);
        widest = MathF.Max(widest, Typography.Measure(result.PrimaryValue, TextStyles.LargeTitle).X);
        widest = MathF.Max(widest, Typography.Measure(label, TextStyles.Caption1).X);
        if (hasSecondary)
        {
            widest = MathF.Max(widest, Typography.Measure(secondary!, TextStyles.Footnote).X);
        }

        var widthLimit = MathF.Max(MinCardWidth * scale,
            MathF.Min(area.Width * CardWidthFraction, MaxCardWidth * scale));
        var cardWidth = Math.Clamp(widest + padding * 2f, MinCardWidth * scale, widthLimit);
        var contentWidth = cardWidth - padding * 2f;
        var titleScale = Typography.FitScale(result.Title, contentWidth, TextStyles.Title1.Scale,
            TextStyles.Title1.Scale * MinFitFactor, TextStyles.Title1.Weight);
        var valueScale = Typography.FitScale(result.PrimaryValue, contentWidth, TextStyles.LargeTitle.Scale,
            TextStyles.LargeTitle.Scale * MinFitFactor, TextStyles.LargeTitle.Weight);
        var titleHeight = Typography.Measure(result.Title, titleScale, TextStyles.Title1.Weight).Y;
        var labelHeight = Typography.Measure(label, TextStyles.Caption1).Y;
        var valueHeight = Typography.Measure(result.PrimaryValue, valueScale, TextStyles.LargeTitle.Weight).Y;
        var secondaryHeight = hasSecondary ? Typography.Measure(secondary!, TextStyles.Footnote).Y : 0f;
        var buttonHeight = ButtonHeight * scale;
        var cardHeight = padding * 2f + titleHeight + Metrics.Space.Lg * scale + labelHeight +
            Metrics.Space.Xxs * scale + valueHeight + Metrics.Space.Xl * scale + buttonHeight;
        if (result.NewBest)
        {
            cardHeight += BadgeHeight * scale + Metrics.Space.Md * scale;
        }

        if (hasSecondary)
        {
            cardHeight += Metrics.Space.Sm * scale + secondaryHeight;
        }

        var cardScale = 0.86f + 0.14f * grow;
        var center = area.Center;
        var half = new Vector2(cardWidth, cardHeight) * 0.5f * cardScale;
        var min = center - half;
        var max = center + half;
        var radius = CardRadius * scale;
        if (result.NewBest && !celebrated && clamped >= 0.4f)
        {
            celebrated = true;
            Celebration.Confetti(new Vector2(center.X, min.Y + 8f * scale), 110, ConfettiPalette, 300f * scale, 4.2f,
                1.5f);
            Celebration.Sparkle(center, 18, GamePalette.Lighten(accent, 0.4f), 130f * scale, 2.6f, 0.9f);
            UiFeedback.Play(UiSound.GameWin);
        }

        Celebration.Update(deltaSeconds);
        Elevation.Floating(drawList, min, max, radius, scale, alpha);
        Material.Frosted(drawList, min, max, radius, scale, alpha);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.20f * alpha }), 1f * scale);

        var offset = padding - cardHeight * 0.5f;
        var titlePhase = Phase(clamped, 0.05f, 0.5f);
        DrawStaggered(drawList, Place(center, offset + titleHeight * 0.5f, cardScale), result.Title,
            result.TitleColor with { W = result.TitleColor.W * titlePhase }, titleScale, TextStyles.Title1.Weight,
            titlePhase, scale);
        offset += titleHeight + Metrics.Space.Lg * scale;
        if (result.NewBest)
        {
            DrawBestBadge(drawList, Place(center, offset + BadgeHeight * 0.5f * scale, cardScale), accent,
                Phase(clamped, 0.2f, 0.65f), scale);
            offset += BadgeHeight * scale + Metrics.Space.Md * scale;
        }

        var labelPhase = Phase(clamped, 0.2f, 0.62f);
        DrawStaggered(drawList, Place(center, offset + labelHeight * 0.5f, cardScale), label,
            theme.TextMuted with { W = labelPhase }, TextStyles.Caption1.Scale, TextStyles.Caption1.Weight, labelPhase,
            scale);
        offset += labelHeight + Metrics.Space.Xxs * scale;
        var valuePhase = Phase(clamped, 0.25f, 0.7f);
        DrawStaggered(drawList, Place(center, offset + valueHeight * 0.5f, cardScale),
            CountingValue(result.PrimaryValue, valuePhase > 0f ? deltaSeconds : 0f),
            theme.TextStrong with { W = valuePhase }, valueScale, TextStyles.LargeTitle.Weight, valuePhase, scale);
        offset += valueHeight;
        if (hasSecondary)
        {
            offset += Metrics.Space.Sm * scale;
            var secondaryPhase = Phase(clamped, 0.32f, 0.78f);
            DrawStaggered(drawList, Place(center, offset + secondaryHeight * 0.5f, cardScale), secondary!,
                theme.TextMuted with { W = secondaryPhase }, TextStyles.Footnote.Scale, TextStyles.Footnote.Weight,
                secondaryPhase, scale);
            offset += secondaryHeight;
        }

        offset += Metrics.Space.Xl * scale;
        Celebration.Draw(drawList, scale);
        var buttonPhase = Phase(clamped, 0.45f, 0.95f);
        if (buttonPhase <= 0.2f)
        {
            return false;
        }

        var pop = 0.85f + 0.15f * Easing.EaseOutBack(buttonPhase);
        var buttonSize = new Vector2(MathF.Min(buttonWidth, contentWidth), buttonHeight) * pop;
        var buttonLift = (1f - buttonPhase) * 8f * scale;
        return GameHud.Button(Place(center, offset + buttonHeight * 0.5f, cardScale) + new Vector2(0f, buttonLift),
            buttonSize, buttonLabel, accent, theme);
    }

    private static Vector2 Place(Vector2 center, float offsetY, float cardScale) =>
        new(center.X, center.Y + offsetY * cardScale);

    private static float Phase(float progress, float start, float end)
    {
        if (progress <= start)
        {
            return 0f;
        }

        if (progress >= end)
        {
            return 1f;
        }

        return Easing.EaseOutCubic((progress - start) / (end - start));
    }

    private static void DrawStaggered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        float textScale, FontWeight weight, float phase, float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var lift = (1f - phase) * 10f * scale;
        Typography.DrawCentered(drawList, center + new Vector2(0f, lift), text, color, textScale, weight);
    }

    private static void DrawBestBadge(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float phase, float scale)
    {
        if (phase <= 0f)
        {
            return;
        }

        var badge = Loc.T(L.Games.NewBest);
        var badgeSize = Typography.Measure(badge, TextStyles.FootnoteEmphasized);
        var badgeHalf = new Vector2(badgeSize.X * 0.5f + 12f * scale, BadgeHeight * 0.5f * scale) *
            Easing.EaseOutBack(phase);
        var min = center - badgeHalf;
        var max = center + badgeHalf;
        Squircle.Fill(drawList, min, max, badgeHalf.Y, ImGui.GetColorU32(accent with { W = 0.24f * phase }));
        Squircle.Stroke(drawList, min, max, badgeHalf.Y, ImGui.GetColorU32(accent with { W = 0.45f * phase }),
            1f * scale);
        var sweep = Pulse.Phase(2400.0);
        var sweepX = min.X + (max.X - min.X + 24f * scale) * sweep - 12f * scale;
        drawList.PushClipRect(min, max, true);
        drawList.AddQuadFilled(new Vector2(sweepX - 5f * scale, max.Y), new Vector2(sweepX + 1f * scale, min.Y),
            new Vector2(sweepX + 7f * scale, min.Y), new Vector2(sweepX + 1f * scale, max.Y),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.20f * phase)));
        drawList.PopClipRect();
        Typography.DrawCentered(drawList, center, badge, accent with { W = phase }, TextStyles.FootnoteEmphasized);
    }

    private static string CountingValue(string primaryValue, float deltaSeconds)
    {
        if (!int.TryParse(primaryValue, NumberStyles.None, CultureInfo.InvariantCulture, out var target) || target <= 0)
        {
            return primaryValue;
        }

        if (deltaSeconds <= 0f && countShown <= 0f)
        {
            return GameNumber.Label(0);
        }

        if (countShown >= target)
        {
            return primaryValue;
        }

        countShown = MathF.Min(target, countShown + target * deltaSeconds / CountUpSeconds);
        var display = (int)countShown;
        return display >= target ? primaryValue : GameNumber.Label(display);
    }
}
