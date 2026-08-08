using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class CoinHero
{
    private const float HeroHeight = 152f;
    private const float HeroRounding = 24f;
    private const float MedallionRadius = 15f;
    private const float CapBarHeight = 58f;
    private const float CapBarRounding = 18f;
    private const float CapFillSmoothSeconds = 0.3f;

    private static readonly Vector4 CappedTint = new(0.98f, 0.80f, 0.36f, 1f);
    private static readonly Vector4 MedallionInk = new(0.26f, 0.18f, 0.05f, 1f);

    private static RollingValue balanceRoll;
    private static Spring capFill = new(0f);

    public static void Draw(CoinWalletDto wallet, in AppPalette palette)
    {
        var scale = UiScale.Current;
        var delta = ImGui.GetIO().DeltaTime;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = HeroHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = HeroRounding * scale;
        var surface = Palette.Lighten(palette.BackdropTop, 0.12f) with { W = 1f };
        Elevation.Card(drawList, min, max, rounding, scale, 0.9f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(surface));
        Material.TopGlow(drawList, min, max, rounding, palette.Accent, 0.92f, 0.26f);
        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var centerX = min.X + width * 0.5f;
        drawList.PushClipRect(min, max, true);
        Sparkle(drawList, min, width, height, 0.14f, 0.26f, 2.1f, 0.20f, palette.Accent, scale);
        Sparkle(drawList, min, width, height, 0.86f, 0.20f, 1.6f, 0.16f, palette.Accent, scale);
        Sparkle(drawList, min, width, height, 0.22f, 0.74f, 1.5f, 0.13f, palette.Accent, scale);
        Sparkle(drawList, min, width, height, 0.80f, 0.66f, 2.4f, 0.18f, palette.Accent, scale);
        Sparkle(drawList, min, width, height, 0.68f, 0.36f, 1.3f, 0.11f, palette.Accent, scale);

        var medallionCenter = new Vector2(centerX, min.Y + 30f * scale);
        var medallionRadius = MedallionRadius * scale;
        ProgressRing.Glow(medallionCenter, medallionRadius * 2.2f, palette.Accent, 0.4f);
        drawList.AddCircleFilled(medallionCenter, medallionRadius, ImGui.GetColorU32(palette.Accent), 48);
        drawList.AddCircle(medallionCenter, medallionRadius - 3f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(MedallionInk, 0.35f)), 48, 1.2f * scale);
        ProgressRing.CenterIcon(drawList, medallionCenter, FontAwesomeIcon.Coins, MedallionInk,
            medallionRadius * 1.05f);

        Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 57f * scale), Loc.T(L.Coin.Balance),
            palette.HeaderInk, TextStyles.Title3);

        balanceRoll.Update((int)wallet.Balance, delta);
        var amountText = balanceRoll.Display.ToString("N0", Loc.Culture);
        var available = width - 44f * scale;
        var amountScale = Typography.FitScale(amountText, available, 2.35f, 1.2f, FontWeight.Bold)
            * balanceRoll.PopScale;
        var amountSize = Typography.Measure(amountText, amountScale, FontWeight.Bold);
        var rowCenterY = min.Y + height * 0.60f;
        ProgressRing.Glow(new Vector2(centerX, rowCenterY), amountSize.Y * 1.35f, palette.Accent, 0.22f);
        Typography.Draw(drawList, new Vector2(centerX - amountSize.X * 0.5f, rowCenterY - amountSize.Y * 0.5f),
            amountText, palette.TitleInk, amountScale, FontWeight.Bold);

        var statsText = Loc.T(L.Coin.EarnedLifetime) + " " + wallet.LifetimeEarned.ToString("N0", Loc.Culture);
        if (wallet.LifetimeSpent > 0)
        {
            statsText += "  ·  " + Loc.T(L.Coin.SpentLifetime) + " "
                + wallet.LifetimeSpent.ToString("N0", Loc.Culture);
        }

        Typography.DrawCentered(drawList, new Vector2(centerX, max.Y - 18f * scale), statsText,
            palette.MutedInk, TextStyles.Footnote);
        drawList.PopClipRect();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
    }

    public static void DrawCapBar(CoinWalletDto wallet, in AppPalette palette)
    {
        var scale = UiScale.Current;
        var delta = ImGui.GetIO().DeltaTime;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = CapBarHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = CapBarRounding * scale;
        var capped = wallet.DailyCap > 0 && wallet.EarnedToday >= wallet.DailyCap;
        var accent = capped ? CappedTint : palette.Accent;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(palette.CardFill));
        if (capped)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.07f)));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.4f)),
                1f * scale);
        }

        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var inset = 14f * scale;
        var textY = min.Y + 12f * scale;
        var progressText = capped
            ? Loc.T(L.Coin.CapReached)
            : Loc.T(L.Coin.CapProgress,
                wallet.EarnedToday.ToString("N0", Loc.Culture),
                wallet.DailyCap.ToString("N0", Loc.Culture));
        var progressLeft = min.X + inset;
        if (capped)
        {
            var checkSize = 11f * scale;
            var progressSize = Typography.Measure(progressText, TextStyles.FootnoteEmphasized);
            ProgressRing.CenterIcon(drawList,
                new Vector2(progressLeft + checkSize * 0.5f, textY + progressSize.Y * 0.5f),
                FontAwesomeIcon.CheckCircle, accent, checkSize);
            progressLeft += checkSize + 6f * scale;
        }

        Typography.Draw(drawList, new Vector2(progressLeft, textY), progressText,
            capped ? accent : palette.TitleInk, TextStyles.FootnoteEmphasized);

        var resetText = Loc.T(L.Coin.CapResets, TimeText.FutureMoment(wallet.ResetsAtUnix));
        var resetSize = Typography.Measure(resetText, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(max.X - inset - resetSize.X, textY + 1f * scale), resetText,
            palette.MutedInk, TextStyles.Footnote);

        var barTop = max.Y - 18f * scale;
        var barMin = new Vector2(min.X + inset, barTop);
        var barMax = new Vector2(max.X - inset, barTop + 6f * scale);
        var barRounding = (barMax.Y - barMin.Y) * 0.5f;
        Squircle.Fill(drawList, barMin, barMax, barRounding,
            ImGui.GetColorU32(Palette.WithAlpha(palette.TitleInk, 0.10f)));
        if (wallet.DailyCap > 0)
        {
            var target = Math.Clamp((float)((double)wallet.EarnedToday / wallet.DailyCap), 0f, 1f);
            var shown = Math.Clamp(capFill.Step(target, CapFillSmoothSeconds, delta), 0f, 1f);
            if (shown > 0.001f)
            {
                var fillEnd = barMin.X + (barMax.X - barMin.X) * shown;
                Squircle.Fill(drawList, barMin, new Vector2(fillEnd, barMax.Y), barRounding,
                    ImGui.GetColorU32(accent));
                ProgressRing.Glow(new Vector2(fillEnd, (barMin.Y + barMax.Y) * 0.5f), 7f * scale, accent,
                    0.4f);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
    }

    private static void Sparkle(ImDrawListPtr drawList, Vector2 min, float width, float height,
        float fractionX, float fractionY, float radius, float alpha, Vector4 accent, float scale)
    {
        drawList.AddCircleFilled(new Vector2(min.X + width * fractionX, min.Y + height * fractionY),
            radius * scale, ImGui.GetColorU32(Palette.WithAlpha(accent, alpha)), 12);
    }
}
