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
    private const float FlairCardHeight = 200f;
    private const float PlainCardHeight = 112f;
    private const float SkuCardGap = 12f;
    private const float StageHeight = 92f;
    private const float StagePadding = 20f;
    private const float GlyphFraction = 0.72f;
    private const float GlyphGapFraction = 0.34f;
    private const float PreviewMaxScale = 1.90f;
    private const float PreviewMinScale = 1.00f;
    private const float BloomAlpha = 0.14f;
    private const float ButtonHeight = 44f;
    private const string FlairKind = "flair";
    private const string FrameKind = "frame";
    private const float FrameCardHeight = 232f;
    private const float FrameStageHeight = 124f;

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
        for (var index = 0; index < skus.Length; index++)
        {
            var sku = skus[index];
            var badge = BadgeFor(sku);
            var frame = FrameFor(sku);
            var cardHeight = (frame is not null ? FrameCardHeight
                : badge is null ? PlainCardHeight : FlairCardHeight) * scale;
            var origin = ImGui.GetCursorScreenPos();
            DrawSkuCard(sku, badge, frame, origin, width, cardHeight, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cardHeight + gap));
        }

        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private Core.Social.BadgeStyle? BadgeFor(CoinSkuStyle sku)
    {
        return string.Equals(sku.Kind, FlairKind, StringComparison.Ordinal)
            ? badgeCatalog.Find(sku.Payload)
            : null;
    }

    private Core.Social.FrameStyle? FrameFor(CoinSkuStyle sku)
    {
        return string.Equals(sku.Kind, FrameKind, StringComparison.Ordinal)
            ? frameCatalog.Find(sku.Payload)
            : null;
    }

    private void DrawFrameStage(ImDrawListPtr drawList, Rect stage, Core.Social.FrameStyle frame, float scale)
    {
        var rounding = 16f * scale;
        Squircle.Fill(drawList, stage.Min, stage.Max, rounding, ImGui.GetColorU32(ui.Palette.FieldSurface));
        Material.EdgeSquircle(drawList, stage.Min, stage.Max, rounding, scale);

        var outerRadius = MathF.Min(stage.Height * 0.42f, stage.Width * 0.30f);
        var avatarRadius = outerRadius / frame.Scale;
        var user = session.CurrentUser;
        AvatarView.DrawRemote(drawList, stage.Center, avatarRadius, theme, user?.Name ?? string.Empty,
            user?.World ?? string.Empty, user?.AvatarUrl, images, lodestone, 1.2f, 48, 1f, frame);
    }

    private void DrawSkuCard(CoinSkuStyle sku, Core.Social.BadgeStyle? badge, Core.Social.FrameStyle? frame,
        Vector2 origin, float cardWidth, float cardHeight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(cardWidth, cardHeight);
        var rounding = 20f * scale;
        ui.Card(drawList, min, max, rounding, false);

        var inset = 14f * scale;
        var textLeft = min.X + inset;
        var textRight = max.X - inset;
        var cursorY = min.Y + inset;
        if (frame is not null)
        {
            var stage = new Rect(new Vector2(textLeft, cursorY),
                new Vector2(textRight, cursorY + FrameStageHeight * scale));
            DrawFrameStage(drawList, stage, frame, scale);
            cursorY = stage.Max.Y + 12f * scale;
        }
        else if (badge is not null)
        {
            var stage = new Rect(new Vector2(textLeft, cursorY),
                new Vector2(textRight, cursorY + StageHeight * scale));
            DrawFlairStage(drawList, stage, badge, scale);
            cursorY = stage.Max.Y + 12f * scale;
        }

        var priceText = Loc.Plural(L.Coin.Price, (int)sku.Price);
        var priceSize = Typography.Measure(priceText, TextStyles.Title3);
        var coinGlyph = priceSize.Y * GlyphFraction;
        var coinGap = priceSize.Y * GlyphGapFraction;
        var priceLeft = textRight - priceSize.X - coinGlyph - coinGap;

        var name = Typography.FitText(sku.Name, priceLeft - textLeft - 8f * scale, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(textLeft, cursorY), name, ui.Palette.TitleInk, TextStyles.Title3);

        ProgressRing.CenterIcon(drawList,
            new Vector2(priceLeft + coinGlyph * 0.5f, cursorY + priceSize.Y * 0.5f),
            FontAwesomeIcon.Coins, ui.Palette.Accent, coinGlyph);
        Typography.Draw(drawList, new Vector2(priceLeft + coinGlyph + coinGap, cursorY), priceText,
            ui.Palette.Accent, TextStyles.Title3);
        cursorY += 26f * scale;

        if (sku.AvailableUntilUnix is { } leavingUnix)
        {
            var leaving = Loc.T(L.Coin.LeavingSoon, TimeText.FutureDayLabel(leavingUnix));
            var fitted = Typography.FitText(leaving, textRight - textLeft, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(textLeft, cursorY), fitted, ui.MutedInk, TextStyles.Caption1);
        }

        var buttonRect = new Rect(
            new Vector2(textLeft, max.Y - inset - ButtonHeight * scale),
            new Vector2(textRight, max.Y - inset));
        if (sku.Owned)
        {
            AppSkin.Chip(buttonRect, Loc.T(L.Coin.Owned), true, theme);
            return;
        }

        if (store.Purchasing)
        {
            AppSkin.Chip(buttonRect, Loc.T(L.Coin.Buy), false, theme);
            return;
        }

        if (ui.PillButton(buttonRect, Loc.T(L.Coin.Buy), true, "coin.buy." + sku.Id))
        {
            AskPurchase(sku);
        }
    }

    private void DrawFlairStage(ImDrawListPtr drawList, Rect stage, Core.Social.BadgeStyle badge, float scale)
    {
        var light = RoleInk.IsLight(theme);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, stage.Min, stage.Max, rounding, ImGui.GetColorU32(ui.Palette.FieldSurface));
        Material.EdgeSquircle(drawList, stage.Min, stage.Max, rounding, scale);

        var padding = StagePadding * scale;
        var maxWidth = stage.Width - padding * 2f;
        var tallest = Typography.Measure("M", new TextStyle(PreviewMaxScale, FontWeight.Bold)).Y;
        var reserve = tallest * (GlyphFraction + GlyphGapFraction);
        var available = MathF.Max(1f, maxWidth - reserve);

        var source = PreviewName();
        var nameScale = Typography.FitScale(source, available, PreviewMaxScale, PreviewMinScale, FontWeight.Bold);
        var nameStyle = new TextStyle(nameScale, FontWeight.Bold);
        var name = Typography.FitText(source, available, nameStyle);
        var nameSize = Typography.Measure(name, nameStyle);

        var glyphSize = nameSize.Y * GlyphFraction;
        var gap = nameSize.Y * GlyphGapFraction;
        var blockWidth = glyphSize + gap + nameSize.X;
        var blockLeft = stage.Center.X - blockWidth * 0.5f;
        var rowCenterY = stage.Center.Y;

        DrawBloom(drawList, stage, new Vector2(stage.Center.X, rowCenterY),
            blockWidth * 0.72f, nameSize.Y * 1.35f, RoleInk.Highlight(badge.Colors[0], light));

        BadgeStrip.DrawOne(drawList, new Vector2(blockLeft + glyphSize * 0.5f, rowCenterY), badge, images, light,
            glyphSize);

        var ink = RoleInk.For(badge.Colors[0], light);
        var namePos = new Vector2(blockLeft + glyphSize + gap, rowCenterY - nameSize.Y * 0.5f);
        Typography.Draw(drawList, namePos, name, ink, nameStyle, NameEffects.For(badge, light));
    }

    private static void DrawBloom(ImDrawListPtr drawList, Rect stage, Vector2 center, float radiusX, float radiusY,
        Vector4 color)
    {
        var spanX = MathF.Min(radiusX, MathF.Min(center.X - stage.Min.X, stage.Max.X - center.X));
        var spanY = MathF.Min(radiusY, MathF.Min(center.Y - stage.Min.Y, stage.Max.Y - center.Y));
        if (spanX <= 1f || spanY <= 1f)
        {
            return;
        }

        var core = ImGui.GetColorU32(color with { W = BloomAlpha });
        var edge = ImGui.GetColorU32(color with { W = 0f });
        var left = center.X - spanX;
        var right = center.X + spanX;
        var top = center.Y - spanY;
        var bottom = center.Y + spanY;

        drawList.AddRectFilledMultiColor(new Vector2(left, top), center, edge, edge, core, edge);
        drawList.AddRectFilledMultiColor(new Vector2(center.X, top), new Vector2(right, center.Y),
            edge, edge, edge, core);
        drawList.AddRectFilledMultiColor(new Vector2(left, center.Y), new Vector2(center.X, bottom),
            edge, core, edge, edge);
        drawList.AddRectFilledMultiColor(center, new Vector2(right, bottom), core, edge, edge, edge);
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
