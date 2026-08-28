using Aetherphone.Core;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float ConvertRowHeight = 66f;
    private const float EarnRowsShown = 4;

    private void DrawCashierTab(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);

        var wallet = coins.Wallet;
        if (wallet is not null)
        {
            CoinHero.Draw(wallet, ui.Palette);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }

        DrawStakeNotice(scale);

        ui.SectionHeading(Loc.T(L.Casino.ConvertHeading), 4f);
        DrawConvertRow(true, scale);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        DrawConvertRow(false, scale);

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        DrawRateEquation(scale);

        if (wallet is not null)
        {
            ui.SectionHeading(Loc.T(L.Coin.EarnHeader), 4f);
            DrawEarnPreview(wallet, scale);
            if (DrawNavRow(FontAwesomeIcon.Coins, L.Casino.OpenWalletRow, L.Casino.OpenWalletRowHint, scale))
            {
                navigation.Open("coin");
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        DrawRecordsRows(scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void DrawRateEquation(float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var chipsText = Core.Casino.CasinoChipLots.ChipPerCoin.ToString("N0", Loc.Culture);
        var coinsText = 1L.ToString("N0", Loc.Culture);
        var equalsText = " = ";
        var chipsSize = CurrencyGlyph.MeasureAmount(chipsText, TextStyles.Caption1);
        var equalsSize = Typography.Measure(equalsText, TextStyles.Caption1);
        var coinsSize = CurrencyGlyph.MeasureAmount(coinsText, TextStyles.Caption1);
        var x = origin.X + (width - chipsSize.X - equalsSize.X - coinsSize.X) * 0.5f;
        x += CurrencyGlyph.DrawAmount(drawList, new Vector2(x, origin.Y), chipsText, CurrencyKind.Chips,
            ui.MutedInk, TextStyles.Caption1).X;
        Typography.Draw(drawList, new Vector2(x, origin.Y), equalsText, ui.MutedInk, TextStyles.Caption1);
        x += equalsSize.X;
        CurrencyGlyph.DrawAmount(drawList, new Vector2(x, origin.Y), coinsText, CurrencyKind.Coins, ui.MutedInk,
            TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, chipsSize.Y + Metrics.Space.Md * scale));
    }

    private void DrawConvertRow(bool buying, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = ConvertRowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, row.Min, row.Max, rounding);

        var stack = casino.State?.Sitting?.Stack ?? 0;
        var seated = stack > 0;
        var inset = 14f * scale;
        var icon = buying ? FontAwesomeIcon.ArrowRight : FontAwesomeIcon.ArrowLeft;
        var iconCenter = new Vector2(row.Min.X + inset + 15f * scale, row.Center.Y);
        drawList.AddCircleFilled(iconCenter, 15f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 32);
        AppSkin.Icon(drawList, iconCenter, IconGlyph.Of(icon), ui.Accent, 0.8f);

        var pillHeight = 34f * scale;
        var pillMax = new Vector2(row.Max.X - inset, row.Center.Y + pillHeight * 0.5f);
        var pillMin = new Vector2(pillMax.X - 96f * scale, row.Center.Y - pillHeight * 0.5f);
        var textLeft = row.Min.X + inset + 40f * scale;
        var textWidth = pillMin.X - 10f * scale - textLeft;

        var title = buying ? Loc.T(L.Casino.ConvertToChips) : Loc.T(L.Casino.ConvertToCoins);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 14f * scale),
            Typography.FitText(title, textWidth, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);

        var hint = buying
            ? Loc.T(L.Casino.ConvertToChipsHint)
            : (seated
                ? Loc.T(L.Casino.ConvertToCoinsHint, stack.ToString("N0", Loc.Culture))
                : Loc.T(L.Casino.ConvertNoChips));
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 36f * scale),
            Typography.FitText(hint, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        var label = buying ? Loc.T(L.Casino.BuyIn) : Loc.T(L.Casino.CashOut);
        var enabled = !casino.MovingMoney && (buying || seated);
        if (AppSkin.PillButton(new Rect(pillMin, pillMax), label, buying, enabled, theme))
        {
            if (buying)
            {
                cashier.Open();
            }
            else
            {
                AskCashOut(casino.State!.Sitting!);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawEarnPreview(Core.Aethernet.Contracts.CoinWalletDto wallet, float scale)
    {
        var rules = wallet.Rules;
        if (rules is null || rules.Length == 0)
        {
            return;
        }

        var shown = 0;
        for (var index = 0; index < rules.Length && shown < EarnRowsShown; index++)
        {
            var rule = rules[index];
            if (CoinGoals.IsComplete(rule))
            {
                continue;
            }

            CoinEarnRow.Draw(rule, ui.Palette);
            shown++;
        }

        if (shown == 0)
        {
            for (var index = 0; index < rules.Length && shown < EarnRowsShown; index++)
            {
                CoinEarnRow.Draw(rules[index], ui.Palette);
                shown++;
            }
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }
}
