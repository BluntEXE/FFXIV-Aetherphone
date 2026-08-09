using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float TileHeight = 108f;
    private const float TileGap = 12f;
    private const float LimitsRowHeight = 60f;

    private readonly struct FloorTileDefinition
    {
        public readonly string GameId;
        public readonly LocString Name;
        public readonly bool Playable;

        public FloorTileDefinition(string gameId, LocString name, bool playable)
        {
            GameId = gameId;
            Name = name;
            Playable = playable;
        }
    }

    private static readonly FloorTileDefinition[] FloorTiles =
    {
        new(CasinoGames.Blackjack, L.Casino.GameBlackjack, false),
        new(CasinoGames.Slots, L.Casino.GameSlots, true),
        new(CasinoGames.Scratch, L.Casino.GameScratch, true),
        new(CasinoGames.Barkeep, L.Casino.GameBarkeep, true),
        new(CasinoGames.Bingo, L.Casino.GameBingo, false),
        new(CasinoGames.Wheel, L.Casino.GameWheel, false),
    };

    private void DrawFloor(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);
        var wallet = coins.Wallet;
        if (wallet is not null)
        {
            var heroOrigin = ImGui.GetCursorScreenPos();
            var heroWidth = ScrollLayout.StableContentWidth();
            CoinHero.Draw(wallet, ui.Palette);
            var heroMax = new Vector2(heroOrigin.X + heroWidth, ImGui.GetCursorScreenPos().Y);
            if (UiInteract.Click(heroOrigin, heroMax, UiInteract.Hover(heroOrigin, heroMax)))
            {
                cashier.Open();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }

        var state = casino.State;
        if (state?.Sitting is not null)
        {
            DrawChipsInPlayRow(state.Sitting, scale);
        }

        if (state is not null && (state.StakesPaused || state.Draining))
        {
            var title = state.StakesPaused ? Loc.T(L.Casino.PausedTitle) : Loc.T(L.Casino.DrainingTitle);
            var hint = state.StakesPaused ? Loc.T(L.Casino.PausedHint) : Loc.T(L.Casino.DrainingHint);
            DrawFloorNotice(title, hint, scale);
        }

        ui.SectionHeading(Loc.T(L.Casino.GamesHeading), 4f);
        DrawGameGrid(scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        ui.SectionHeading(Loc.T(L.Casino.CareHeading), 4f);
        DrawLimitsRow(scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void DrawGameGrid(float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var gap = TileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var tileHeight = TileHeight * scale;
        var rowCount = (FloorTiles.Length + 1) / 2;
        for (var index = 0; index < FloorTiles.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var tile = new Rect(min, new Vector2(min.X + tileWidth, min.Y + tileHeight));
            DrawGameTile(drawList, tile, FloorTiles[index], scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowCount * tileHeight + (rowCount - 1) * gap));
    }

    private void DrawGameTile(ImDrawListPtr drawList, Rect tile, in FloorTileDefinition definition, float scale)
    {
        var rounding = Metrics.Radius.Card * scale;
        var hovered = definition.Playable && UiInteract.Hover(tile.Min, tile.Max);
        ui.Card(drawList, tile.Min, tile.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, tile.Min, tile.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var contentAlpha = definition.Playable ? 1f : 0.45f;
        var glyphCenter = new Vector2(tile.Center.X, tile.Min.Y + 40f * scale);
        var glyphRadius = 20f * scale;
        drawList.AddCircleFilled(glyphCenter, glyphRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.FieldSurface, contentAlpha)), 40);
        drawList.AddCircle(glyphCenter, glyphRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f * contentAlpha)), 40,
            Metrics.Stroke.Thin * scale);
        CasinoGlyphs.Draw(drawList, definition.GameId, glyphCenter, 11f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, contentAlpha)),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.FieldSurface, contentAlpha)));

        var name = Typography.FitText(Loc.T(definition.Name), tile.Width - 20f * scale,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawCentered(drawList, new Vector2(tile.Center.X, tile.Min.Y + 78f * scale), name,
            Palette.WithAlpha(ui.TitleInk, definition.Playable ? 1f : 0.6f), TextStyles.SubheadlineEmphasized);

        if (!definition.Playable)
        {
            DrawSoonChip(drawList, tile, scale);
            return;
        }

        if (UiInteract.Click(tile.Min, tile.Max, hovered))
        {
            router.Push(new CasinoRoute(CasinoScreen.Cabinet, definition.GameId));
        }
    }

    private void DrawSoonChip(ImDrawListPtr drawList, Rect tile, float scale)
    {
        var label = Loc.T(L.Casino.Soon);
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var horizontalPad = 7f * scale;
        var chipHeight = labelSize.Y + 5f * scale;
        var chipMax = new Vector2(tile.Max.X - 8f * scale, tile.Min.Y + 8f * scale + chipHeight);
        var chipMin = new Vector2(chipMax.X - labelSize.X - horizontalPad * 2f, tile.Min.Y + 8f * scale);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f * scale);
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, ui.MutedInk, TextStyles.Caption1);
    }

    private void DrawChipsInPlayRow(Core.Aethernet.Contracts.CasinoSittingDto sitting, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = 48f * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        ui.Card(drawList, row.Min, row.Max, rounding);
        Squircle.Stroke(drawList, row.Min, row.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var label = Loc.T(L.Casino.ChipsAt, sitting.Stack.ToString("N0", Loc.Culture),
            Loc.T(GameName(ClientGameId(sitting.GameKind))));
        var chevronCenter = new Vector2(row.Max.X - 18f * scale, row.Center.Y);
        var fitted = Typography.FitText(label, chevronCenter.X - 14f * scale - row.Min.X - 14f * scale,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(row.Min.X + 14f * scale, row.Center.Y - 9f * scale), fitted,
            ui.Accent, TextStyles.SubheadlineEmphasized);
        AppSkin.Icon(drawList, chevronCenter, FontAwesomeIcon.ChevronRight.ToIconString(), ui.MutedInk, 0.8f);

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            cashier.Open();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private void DrawFloorNotice(string title, string hint, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + hintBlock.Y + 26f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 10f * scale), title,
            ui.Accent, TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + titleSize.Y + 16f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private static string ClientGameId(string wireKind)
    {
        const string prefix = "casino.";
        return wireKind.StartsWith(prefix, StringComparison.Ordinal) ? wireKind[prefix.Length..] : wireKind;
    }

    private void DrawLimitsRow(float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = LimitsRowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        ui.Card(drawList, row.Min, row.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconCenter = new Vector2(row.Min.X + 26f * scale, row.Center.Y);
        drawList.AddCircleFilled(iconCenter, 15f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 32);
        AppSkin.Icon(drawList, iconCenter, FontAwesomeIcon.HandHoldingHeart.ToIconString(), ui.Accent, 0.85f);

        var textLeft = row.Min.X + 48f * scale;
        var chevronCenter = new Vector2(row.Max.X - 18f * scale, row.Center.Y);
        var textWidth = chevronCenter.X - 14f * scale - textLeft;
        var title = Typography.FitText(Loc.T(L.Casino.LimitsRow), textWidth, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 12f * scale), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var hint = Typography.FitText(Loc.T(L.Casino.LimitsRowHint), textWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 32f * scale), hint, ui.MutedInk,
            TextStyles.Footnote);
        AppSkin.Icon(drawList, chevronCenter, FontAwesomeIcon.ChevronRight.ToIconString(), ui.MutedInk, 0.8f);

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            router.Push(new CasinoRoute(CasinoScreen.Limits));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }
}
