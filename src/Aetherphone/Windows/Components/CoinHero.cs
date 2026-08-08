using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CoinHero
{
    private const float HeroHeight = 132f;
    private const float HeroRounding = 24f;
    private const float CapBarHeight = 58f;
    private const float CapBarRounding = 18f;

    private static readonly Vector4 CappedTint = new(0.98f, 0.80f, 0.36f, 1f);

    public static void Draw(CoinWalletDto wallet, in AppPalette palette)
    {
        var scale = UiScale.Current;
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
        Material.TopGlow(drawList, min, max, rounding, palette.Accent, 0.92f, 0.22f);
        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var centerX = min.X + width * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 27f * scale), Loc.T(L.Coin.Balance),
            palette.HeaderInk, TextStyles.FootnoteEmphasized);

        var amountText = wallet.Balance.ToString("N0", Loc.Culture);
        var available = width - 44f * scale;
        var amountScale = Typography.FitScale(amountText, available, 2.35f, 1.2f, FontWeight.Bold);
        var amountSize = Typography.Measure(amountText, amountScale, FontWeight.Bold);
        var rowCenterY = min.Y + height * 0.56f;
        Typography.Draw(drawList, new Vector2(centerX - amountSize.X * 0.5f, rowCenterY - amountSize.Y * 0.5f),
            amountText, palette.TitleInk, amountScale, FontWeight.Bold);

        var lifetimeText = Loc.T(L.Coin.EarnedLifetime) + " " + wallet.LifetimeEarned.ToString("N0", Loc.Culture);
        Typography.DrawCentered(drawList, new Vector2(centerX, max.Y - 18f * scale), lifetimeText,
            palette.MutedInk, TextStyles.Footnote);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
    }

    public static void DrawCapBar(CoinWalletDto wallet, in AppPalette palette)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = CapBarHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = CapBarRounding * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var inset = 14f * scale;
        var capped = wallet.DailyCap > 0 && wallet.EarnedToday >= wallet.DailyCap;
        var accent = capped ? CappedTint : palette.Accent;
        var textY = min.Y + 12f * scale;
        var progressText = capped
            ? Loc.T(L.Coin.CapReached)
            : Loc.T(L.Coin.CapProgress,
                wallet.EarnedToday.ToString("N0", Loc.Culture),
                wallet.DailyCap.ToString("N0", Loc.Culture));
        Typography.Draw(drawList, new Vector2(min.X + inset, textY), progressText, palette.TitleInk,
            TextStyles.FootnoteEmphasized);

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
            var fraction = Math.Clamp((float)((double)wallet.EarnedToday / wallet.DailyCap), 0f, 1f);
            if (fraction > 0.001f)
            {
                var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * fraction, barMax.Y);
                Squircle.Fill(drawList, barMin, fillMax, barRounding, ImGui.GetColorU32(accent));
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
    }
}
