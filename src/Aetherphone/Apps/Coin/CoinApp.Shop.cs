using Aetherphone.Core;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float SkuCardHeight = 142f;
    private const float SkuCardGap = 10f;
    private const string FlairKind = "flair";

    private void DrawShop(Rect body)
    {
        var scale = UiScale.Current;
        catalog.EnsureFresh();
        using var surface = AppSurface.Begin(body);
        ConsumePurchaseResult();

        var skus = catalog.Skus;
        if (skus.Length == 0)
        {
            if (!catalog.LoadedOnce)
            {
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            }
            else
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Store, Loc.T(L.Coin.ShopEmpty),
                    Loc.T(L.Coin.ShopEmptyHint));
            }

            return;
        }

        var width = ScrollLayout.StableContentWidth();
        var gap = SkuCardGap * scale;
        var cardWidth = (width - gap) * 0.5f;
        var cardHeight = SkuCardHeight * scale;
        for (var index = 0; index < skus.Length; index += 2)
        {
            var origin = ImGui.GetCursorScreenPos();
            DrawSkuCard(skus[index], origin, cardWidth, cardHeight, scale);
            if (index + 1 < skus.Length)
            {
                DrawSkuCard(skus[index + 1], new Vector2(origin.X + cardWidth + gap, origin.Y),
                    cardWidth, cardHeight, scale);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cardHeight + gap));
        }

        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private void DrawSkuCard(CoinSkuStyle sku, Vector2 origin, float cardWidth, float cardHeight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(cardWidth, cardHeight);
        var rounding = 18f * scale;
        ui.Card(drawList, min, max, rounding, false);

        var inset = 12f * scale;
        var textLeft = min.X + inset;
        var textWidth = cardWidth - inset * 2f;
        var cursorY = min.Y + inset;
        if (DrawFlairPreview(drawList, sku, textLeft, cursorY, textWidth, scale))
        {
            cursorY += 26f * scale;
            var label = Typography.FitText(sku.Name, textWidth, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), label, ui.MutedInk, TextStyles.Caption1);
            cursorY += 18f * scale;
        }
        else
        {
            var name = Typography.FitText(sku.Name, textWidth, TextStyles.Headline);
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), name, ui.Palette.TitleInk, TextStyles.Headline);
            cursorY += 24f * scale;
        }

        var priceText = sku.Price.ToString("N0", Loc.Culture);
        Typography.Draw(drawList, new Vector2(textLeft, cursorY), priceText, ui.Palette.Accent, TextStyles.Title3);
        cursorY += 24f * scale;

        if (sku.AvailableUntilUnix is { } leavingUnix)
        {
            var leaving = Loc.T(L.Coin.LeavingSoon, TimeText.FutureDayLabel(leavingUnix));
            var fitted = Typography.FitText(leaving, textWidth, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), fitted, ui.MutedInk, TextStyles.Caption1);
        }

        var buttonRect = new Rect(
            new Vector2(min.X + inset, max.Y - 38f * scale),
            new Vector2(max.X - inset, max.Y - inset));
        if (sku.Owned)
        {
            AppSkin.Chip(buttonRect, Loc.T(L.Coin.Owned), true, theme);
            return;
        }

        if (ConfirmDialog.DrawPillButton(buttonRect, Loc.T(L.Coin.Buy), !store.Purchasing, theme, 1f, 1f,
                ConfirmButtonTone.Primary, "coin.buy." + sku.Id))
        {
            AskPurchase(sku);
        }
    }

    private bool DrawFlairPreview(ImDrawListPtr drawList, CoinSkuStyle sku, float left, float top, float width,
        float scale)
    {
        if (!string.Equals(sku.Kind, FlairKind, StringComparison.Ordinal))
        {
            return false;
        }

        var badge = badgeCatalog.Find(sku.Payload);
        if (badge is null)
        {
            return false;
        }

        var light = RoleInk.IsLight(theme);
        var glyphSize = 15f * scale;
        var center = new Vector2(left + glyphSize * 0.5f, top + glyphSize * 0.5f);
        BadgeStrip.DrawOne(drawList, center, badge, images, light, glyphSize);

        var nameLeft = left + glyphSize + 6f * scale;
        var fitted = Typography.FitText(PreviewName(), width - (glyphSize + 6f * scale), TextStyles.Headline);
        var ink = RoleInk.For(badge.Colors[0], light);
        Typography.Draw(drawList, new Vector2(nameLeft, top), fitted, ink, TextStyles.Headline,
            NameEffects.For(badge, light));
        return true;
    }

    private string PreviewName()
    {
        var user = session.CurrentUser;
        if (user is null)
        {
            return Loc.T(L.Coin.TabShop);
        }

        return user.DisplayName.Length > 0 ? user.DisplayName : user.Name;
    }

    private void AskPurchase(CoinSkuStyle sku)
    {
        var price = sku.Price;
        var skuId = sku.Id;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Coin.BuyConfirmTitle, sku.Name),
            Message = Loc.T(L.Coin.BuyConfirmBody, price.ToString("N0", Loc.Culture)),
            ConfirmLabel = Loc.T(L.Coin.Buy),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = () => store.Purchase(skuId, price),
        });
    }

    private void ConsumePurchaseResult()
    {
        var result = store.TakePurchaseResult();
        if (result is null)
        {
            return;
        }

        if (result.Purchased)
        {
            catalog.RefreshNow();
            return;
        }

        var message = result.Reason switch
        {
            "insufficient" => Loc.T(L.Coin.Insufficient),
            "price_changed" => Loc.T(L.Coin.PriceChanged),
            "frozen" => Loc.T(L.Coin.FrozenTitle),
            _ => Loc.T(L.Coin.Unavailable),
        };
        confirm.Alert(null, message, Loc.T(L.Common.Close));
        if (result.Reason == "price_changed")
        {
            catalog.RefreshNow();
        }
    }
}
