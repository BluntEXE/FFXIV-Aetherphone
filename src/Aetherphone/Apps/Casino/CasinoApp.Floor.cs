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
    private const float NavRowHeight = 60f;
    private const float ResumePillHeight = 36f;
    private const long SessionPillAfterSeconds = 45 * 60;

    private readonly struct FloorTileDefinition
    {
        public readonly string GameId;
        public readonly LocString Name;
        public readonly bool Playable;
        public readonly string RoomId;

        public FloorTileDefinition(string gameId, LocString name, bool playable, string roomId = "")
        {
            GameId = gameId;
            Name = name;
            Playable = playable;
            RoomId = roomId;
        }
    }

    private static readonly FloorTileDefinition[] FloorTiles =
    {
        new(CasinoGames.Blackjack, L.Casino.GameBlackjack, false),
        new(CasinoGames.Slots, L.Casino.GameSlots, true),
        new(CasinoGames.Scratch, L.Casino.GameScratch, true),
        new(CasinoGames.Barkeep, L.Casino.GameBarkeep, true),
        new(CasinoGames.Bingo, L.Casino.GameBingo, false),
        new(CasinoGames.Wheel, L.Casino.GameWheel, true, Core.Casino.CasinoRoomIds.WheelFloor),
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
            DrawSittingResumeCard(state.Sitting, scale);
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
        ui.SectionHeading(Loc.T(L.Casino.RecordsHeading), 4f);
        if (DrawNavRow(FontAwesomeIcon.Receipt, L.Casino.HistoryRow, L.Casino.HistoryRowHint, scale))
        {
            historyLoadFailed = false;
            history.Invalidate();
            router.Push(new CasinoRoute(CasinoScreen.History));
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (DrawNavRow(FontAwesomeIcon.ShieldAlt, L.Casino.FairnessRow, L.Casino.FairnessRowHint, scale))
        {
            router.Push(new CasinoRoute(CasinoScreen.Fairness));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        ui.SectionHeading(Loc.T(L.Casino.CareHeading), 4f);
        if (DrawNavRow(FontAwesomeIcon.HandHoldingHeart, L.Casino.LimitsRow, L.Casino.LimitsRowHint, scale))
        {
            router.Push(new CasinoRoute(CasinoScreen.Limits));
        }

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
            DrawCornerChip(drawList, tile, Loc.T(L.Casino.Soon), ui.MutedInk, scale);
            return;
        }

        if (definition.RoomId.Length > 0)
        {
            var occupancy = casinoRooms.OccupancyOf(definition.RoomId);
            if (occupancy > 0)
            {
                DrawCornerChip(drawList, tile,
                    Loc.T(L.Casino.WheelAtTheRail, Apps.Games.Framework.GameNumber.Label(occupancy)), ui.Accent,
                    scale);
            }
        }

        if (UiInteract.Click(tile.Min, tile.Max, hovered))
        {
            OpenGame(definition.GameId);
        }
    }

    private void DrawCornerChip(ImDrawListPtr drawList, Rect tile, string label, Vector4 ink, float scale)
    {
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var horizontalPad = 7f * scale;
        var chipHeight = labelSize.Y + 5f * scale;
        var chipMax = new Vector2(tile.Max.X - 8f * scale, tile.Min.Y + 8f * scale + chipHeight);
        var chipMin = new Vector2(chipMax.X - labelSize.X - horizontalPad * 2f, tile.Min.Y + 8f * scale);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f * scale);
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, ink, TextStyles.Caption1);
    }

    private void DrawSittingResumeCard(Core.Aethernet.Contracts.CasinoSittingDto sitting, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var inset = 14f * scale;
        var label = Loc.T(L.Casino.ChipsAt, sitting.Stack.ToString("N0", Loc.Culture),
            Loc.T(GameName(ClientGameId(sitting.GameKind))));
        var labelSize = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var sessionLine = SessionElapsedLine();
        var sessionSize = sessionLine.Length > 0
            ? Typography.Measure(sessionLine, TextStyles.Caption1)
            : Vector2.Zero;
        var pillHeight = ResumePillHeight * scale;
        var height = 12f * scale + labelSize.Y + 10f * scale + pillHeight + 12f * scale;
        if (sessionLine.Length > 0)
        {
            height += sessionSize.Y + 6f * scale;
        }

        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, min, max, rounding);
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);

        var fitted = Typography.FitText(label, width - inset * 2f, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 12f * scale), fitted, ui.Accent,
            TextStyles.SubheadlineEmphasized);

        var pillTop = min.Y + 12f * scale + labelSize.Y + 10f * scale;
        var pillGap = 10f * scale;
        var pillWidth = (width - inset * 2f - pillGap) * 0.5f;
        var resumeRect = new Rect(new Vector2(min.X + inset, pillTop),
            new Vector2(min.X + inset + pillWidth, pillTop + pillHeight));
        if (AppSkin.PillButton(resumeRect, Loc.T(L.Casino.ResumeAction), true, !casino.MovingMoney, theme))
        {
            OpenGame(ClientGameId(sitting.GameKind));
        }

        var cashOutRect = new Rect(new Vector2(resumeRect.Max.X + pillGap, pillTop),
            new Vector2(min.X + inset + pillWidth * 2f + pillGap, pillTop + pillHeight));
        if (AppSkin.PillButton(cashOutRect, Loc.T(L.Casino.CashOut), false, !casino.MovingMoney, theme))
        {
            AskCashOut(sitting);
        }

        if (sessionLine.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(min.X + inset, pillTop + pillHeight + 6f * scale),
                sessionLine, ui.MutedInk, TextStyles.Caption1);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private string SessionElapsedLine()
    {
        var seenAtUnix = casino.SittingSeenAtUnix;
        if (seenAtUnix <= 0)
        {
            return string.Empty;
        }

        var elapsedSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seenAtUnix;
        if (elapsedSeconds < SessionPillAfterSeconds)
        {
            return string.Empty;
        }

        return Loc.T(L.Casino.SessionPill, TimeText.Duration((int)Math.Min(elapsedSeconds, int.MaxValue)));
    }

    private void AskCashOut(Core.Aethernet.Contracts.CasinoSittingDto sitting)
    {
        var stackText = sitting.Stack.ToString("N0", Loc.Culture);
        confirm.Ask(new Core.Confirm.ConfirmRequest
        {
            Title = Loc.T(L.Casino.CashOutConfirmTitle, stackText),
            Message = Loc.T(L.Casino.CashOutConfirmBody),
            ConfirmLabel = Loc.T(L.Casino.CashOut),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = casino.CloseSitting,
        });
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

    private bool DrawNavRow(FontAwesomeIcon icon, LocString title, LocString hint, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = NavRowHeight * scale;
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
        AppSkin.Icon(drawList, iconCenter, icon.ToIconString(), ui.Accent, 0.85f);

        var textLeft = row.Min.X + 48f * scale;
        var chevronCenter = new Vector2(row.Max.X - 18f * scale, row.Center.Y);
        var textWidth = chevronCenter.X - 14f * scale - textLeft;
        var titleText = Typography.FitText(Loc.T(title), textWidth, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 12f * scale), titleText, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var hintText = Typography.FitText(Loc.T(hint), textWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 32f * scale), hintText, ui.MutedInk,
            TextStyles.Footnote);
        AppSkin.Icon(drawList, chevronCenter, FontAwesomeIcon.ChevronRight.ToIconString(), ui.MutedInk, 0.8f);

        var clicked = UiInteract.Click(row.Min, row.Max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return clicked;
    }
}
