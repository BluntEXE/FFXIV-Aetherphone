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
    private const int ShopFilterAll = 0;
    private const int ShopFilterFrames = 1;
    private const int ShopFilterBadges = 2;
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
    private const float FrameCellHeight = 260f;
    private const float FrameCellStageHeight = 150f;
    private const float FrameCellButtonHeight = 34f;
    private const float ShopSectionGap = 20f;
    private const float ShopHeaderHeight = 30f;

    private readonly ChipRail shopRail = new();
    private readonly string[] shopChipLabels = new string[3];
    private readonly bool[] shopChipActive = new bool[3];

    private int shopFilter;

    private void DrawShop(Rect body)
    {
        var scale = UiScale.Current;
        catalog.EnsureFresh();
        using var surface = AppSurface.Begin(body);
        shopRefresh.Draw(body, surface.Pull, surface.Dragging, catalog.Fetching, ui.MutedInk, RefreshShop);
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
        CountShopSections(skus, out var frameTotal, out var frameOwned, out var badgeTotal, out var badgeOwned);
        DrawShopFilterRail(frameTotal, badgeTotal, scale);

        if (shopFilter != ShopFilterBadges && frameTotal > 0)
        {
            DrawShopSectionHeader(Loc.T(L.Loadout.FramesTitle), frameOwned, frameTotal, width, scale);
            DrawFrameGrid(skus, width, scale);
            if (shopFilter == ShopFilterAll && badgeTotal > 0)
            {
                ImGui.Dummy(new Vector2(0f, ShopSectionGap * scale));
            }
        }

        if (shopFilter != ShopFilterFrames && badgeTotal > 0)
        {
            DrawShopSectionHeader(Loc.T(L.Loadout.BadgesTitle), badgeOwned, badgeTotal, width, scale);
            DrawBadgeCards(skus, width, scale);
        }

        if (shopFilter == ShopFilterAll)
        {
            DrawPlainCards(skus, width, scale);
        }

        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private static void CountShopSections(CoinSkuStyle[] skus, out int frameTotal, out int frameOwned,
        out int badgeTotal, out int badgeOwned)
    {
        frameTotal = 0;
        frameOwned = 0;
        badgeTotal = 0;
        badgeOwned = 0;
        for (var index = 0; index < skus.Length; index++)
        {
            var sku = skus[index];
            if (string.Equals(sku.Kind, FrameKind, StringComparison.Ordinal))
            {
                frameTotal++;
                if (sku.Owned)
                {
                    frameOwned++;
                }

                continue;
            }

            if (string.Equals(sku.Kind, FlairKind, StringComparison.Ordinal))
            {
                badgeTotal++;
                if (sku.Owned)
                {
                    badgeOwned++;
                }
            }
        }
    }

    private void DrawShopFilterRail(int frameTotal, int badgeTotal, float scale)
    {
        if (frameTotal == 0 || badgeTotal == 0)
        {
            shopFilter = ShopFilterAll;
            return;
        }

        shopChipLabels[ShopFilterAll] = Loc.T(L.Coin.FilterAll);
        shopChipLabels[ShopFilterFrames] = Loc.T(L.Loadout.FramesTitle);
        shopChipLabels[ShopFilterBadges] = Loc.T(L.Loadout.BadgesTitle);
        shopChipActive[ShopFilterAll] = shopFilter == ShopFilterAll;
        shopChipActive[ShopFilterFrames] = shopFilter == ShopFilterFrames;
        shopChipActive[ShopFilterBadges] = shopFilter == ShopFilterBadges;
        var tapped = shopRail.Draw(ui, shopChipLabels, shopChipActive, "coin.shop.categories");
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        if (tapped >= 0)
        {
            shopFilter = tapped;
        }
    }

    private void DrawShopSectionHeader(string title, int owned, int total, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var titleSize = Typography.Measure(title, TextStyles.Title3);
        var barWidth = 4f * scale;
        var bar = new Rect(
            new Vector2(origin.X, origin.Y + 3f * scale),
            new Vector2(origin.X + barWidth, origin.Y + titleSize.Y - 3f * scale));
        Squircle.Fill(drawList, bar.Min, bar.Max, barWidth * 0.5f, ImGui.GetColorU32(ui.Palette.Accent));
        Typography.Draw(drawList, new Vector2(bar.Max.X + 8f * scale, origin.Y), title, ui.Palette.TitleInk,
            TextStyles.Title3);

        var counter = Loc.T(L.Coin.SectionOwned, owned, total);
        var counterSize = Typography.Measure(counter, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(origin.X + width - counterSize.X, origin.Y + 4f * scale), counter,
            ui.MutedInk, TextStyles.Caption1);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ShopHeaderHeight * scale));
    }

    private void DrawFrameGrid(CoinSkuStyle[] skus, float width, float scale)
    {
        var gap = SkuCardGap * scale;
        var cellWidth = (width - gap) * 0.5f;
        var cellHeight = FrameCellHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var cell = 0;
        for (var index = 0; index < skus.Length; index++)
        {
            var sku = skus[index];
            if (!string.Equals(sku.Kind, FrameKind, StringComparison.Ordinal))
            {
                continue;
            }

            var column = cell % 2;
            var row = cell / 2;
            var cellMin = new Vector2(origin.X + column * (cellWidth + gap), origin.Y + row * (cellHeight + gap));
            DrawFrameCard(sku, frameCatalog.Find(sku.Payload), cellMin, cellWidth, cellHeight, scale);
            cell++;
        }

        var rows = (cell + 1) / 2;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * (cellHeight + gap)));
    }

    private void DrawFrameCard(CoinSkuStyle sku, Core.Social.FrameStyle? frame, Vector2 origin, float cellWidth,
        float cellHeight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(cellWidth, cellHeight);
        ui.Card(drawList, min, max, 18f * scale, false);

        var inset = 10f * scale;
        var stage = new Rect(
            new Vector2(min.X + inset, min.Y + inset),
            new Vector2(max.X - inset, min.Y + inset + FrameCellStageHeight * scale));
        var stageRounding = 14f * scale;
        Squircle.Fill(drawList, stage.Min, stage.Max, stageRounding, ImGui.GetColorU32(ui.Palette.FieldSurface));
        Material.EdgeSquircle(drawList, stage.Min, stage.Max, stageRounding, scale);
        DrawBloom(drawList, stage, stage.Center, stage.Width * 0.42f, stage.Height * 0.60f, ui.Palette.Accent);

        var outerRadius = MathF.Min(stage.Height * 0.48f, stage.Width * 0.45f);
        var avatarRadius = outerRadius / (frame?.Scale ?? 1f);
        var user = session.CurrentUser;
        AvatarView.DrawRemote(drawList, stage.Center, avatarRadius, theme, user?.Name ?? string.Empty,
            user?.World ?? string.Empty, user?.AvatarUrl, images, lodestone, 1.2f, 48, 1f, frame);
        DrawLeavingTag(drawList, stage, sku, scale);

        var name = Typography.FitText(sku.Name, stage.Width, TextStyles.BodyEmphasized);
        var nameSize = Typography.Measure(name, TextStyles.BodyEmphasized);
        var nameY = stage.Max.Y + 8f * scale;
        Typography.Draw(drawList, new Vector2(min.X + (cellWidth - nameSize.X) * 0.5f, nameY), name,
            ui.Palette.TitleInk, TextStyles.BodyEmphasized);

        var priceText = Loc.Plural(L.Coin.Price, (int)sku.Price);
        var lineHeight = Typography.Measure(priceText, TextStyles.SubheadlineEmphasized).Y;
        var coinGlyph = lineHeight * GlyphFraction;
        var coinGap = lineHeight * GlyphGapFraction;
        var priceFit = Typography.FitText(priceText, stage.Width - coinGlyph - coinGap,
            TextStyles.SubheadlineEmphasized);
        var priceSize = Typography.Measure(priceFit, TextStyles.SubheadlineEmphasized);
        var blockLeft = min.X + (cellWidth - coinGlyph - coinGap - priceSize.X) * 0.5f;
        var priceY = nameY + nameSize.Y + 4f * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(blockLeft + coinGlyph * 0.5f, priceY + priceSize.Y * 0.5f),
            FontAwesomeIcon.Coins, ui.Palette.Accent, coinGlyph);
        Typography.Draw(drawList, new Vector2(blockLeft + coinGlyph + coinGap, priceY), priceFit,
            ui.Palette.Accent, TextStyles.SubheadlineEmphasized);

        var buttonRect = new Rect(
            new Vector2(stage.Min.X, max.Y - inset - FrameCellButtonHeight * scale),
            new Vector2(stage.Max.X, max.Y - inset));
        DrawBuyControl(sku, buttonRect);
    }

    private void DrawLeavingTag(ImDrawListPtr drawList, Rect stage, CoinSkuStyle sku, float scale)
    {
        if (sku.AvailableUntilUnix is not { } leavingUnix)
        {
            return;
        }

        var label = Loc.T(L.Coin.LeavingSoon, TimeText.FutureDayLabel(leavingUnix));
        var paddingX = 8f * scale;
        var paddingY = 3f * scale;
        var margin = 6f * scale;
        var fitted = Typography.FitText(label, stage.Width - margin * 2f - paddingX * 2f, TextStyles.Caption2);
        var size = Typography.Measure(fitted, TextStyles.Caption2);
        var min = new Vector2(stage.Min.X + margin, stage.Min.Y + margin);
        var max = min + new Vector2(size.X + paddingX * 2f, size.Y + paddingY * 2f);
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.Accent, 0.22f)));
        Typography.Draw(drawList, min + new Vector2(paddingX, paddingY), fitted, ui.Palette.TitleInk,
            TextStyles.Caption2);
    }

    private void DrawBadgeCards(CoinSkuStyle[] skus, float width, float scale)
    {
        var gap = SkuCardGap * scale;
        for (var index = 0; index < skus.Length; index++)
        {
            var sku = skus[index];
            if (!string.Equals(sku.Kind, FlairKind, StringComparison.Ordinal))
            {
                continue;
            }

            var badge = badgeCatalog.Find(sku.Payload);
            var cardHeight = (badge is null ? PlainCardHeight : FlairCardHeight) * scale;
            var origin = ImGui.GetCursorScreenPos();
            DrawSkuCard(sku, badge, origin, width, cardHeight, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cardHeight + gap));
        }
    }

    private void DrawPlainCards(CoinSkuStyle[] skus, float width, float scale)
    {
        var gap = SkuCardGap * scale;
        for (var index = 0; index < skus.Length; index++)
        {
            var sku = skus[index];
            if (string.Equals(sku.Kind, FlairKind, StringComparison.Ordinal)
                || string.Equals(sku.Kind, FrameKind, StringComparison.Ordinal))
            {
                continue;
            }

            var cardHeight = PlainCardHeight * scale;
            var origin = ImGui.GetCursorScreenPos();
            DrawSkuCard(sku, null, origin, width, cardHeight, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cardHeight + gap));
        }
    }

    private void DrawSkuCard(CoinSkuStyle sku, Core.Social.BadgeStyle? badge, Vector2 origin, float cardWidth,
        float cardHeight, float scale)
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
        if (badge is not null)
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
        DrawBuyControl(sku, buttonRect);
    }

    private void DrawBuyControl(CoinSkuStyle sku, Rect buttonRect)
    {
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
            RefreshShop();
            RefreshInventory();
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
