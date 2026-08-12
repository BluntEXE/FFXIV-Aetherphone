using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float ChipBarHeight = 76f;
    private const float LiveStripHeight = 92f;
    private const float LiveStripGap = 10f;

    private void DrawLobbyTab(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);

        DrawChipBar(scale);
        var jackpot = casino.Jackpot;
        if (jackpot > 0 && jackpotRail.Draw(ui, jackpot))
        {
            OpenGame(CasinoGames.Slots);
        }

        DrawStakeNotice(scale);

        var sitting = casino.State?.Sitting;
        if (sitting is not null)
        {
            DrawSittingResumeCard(sitting, scale);
        }

        DrawDailySpinCard(scale);

        if (AnyRoomLive())
        {
            ui.SectionHeading(Loc.T(L.Casino.LiveHeading), 4f);
            DrawLiveStrip(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        ui.SectionHeading(Loc.T(L.Casino.GamesHeading), 4f);
        DrawGameGrid(scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        DrawRecordsRows(scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void DrawChipBar(float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = ChipBarHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, min, max, rounding);

        var inset = 16f * scale;
        var stack = casino.State?.Sitting?.Stack ?? 0;
        var coinBalance = coins.Wallet?.Balance ?? 0;

        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 13f * scale),
            Loc.T(L.Casino.ChipsRow), ui.MutedInk, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 31f * scale),
            stack.ToString("N0", Loc.Culture), stack > 0 ? ui.Accent : ui.MutedInk, TextStyles.Title3);

        var mid = min.X + width * 0.44f;
        drawList.AddLine(new Vector2(mid, min.Y + 16f * scale), new Vector2(mid, max.Y - 16f * scale),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.10f)), 1f * scale);

        Typography.Draw(drawList, new Vector2(mid + inset, min.Y + 13f * scale),
            Loc.T(L.Casino.WalletRow), ui.MutedInk, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(mid + inset, min.Y + 31f * scale),
            coinBalance.ToString("N0", Loc.Culture), ui.TitleInk, TextStyles.Title3);

        var pillHeight = 28f * scale;
        var pillMax = new Vector2(max.X - inset, max.Y - 12f * scale);
        var pillMin = new Vector2(pillMax.X - 96f * scale, pillMax.Y - pillHeight);
        var label = stack > 0 ? Loc.T(L.Casino.TopUp) : Loc.T(L.Casino.BuyIn);
        if (AppSkin.PillButton(new Rect(pillMin, pillMax), label, true, !casino.MovingMoney, theme))
        {
            cashier.Open();
        }

        Typography.Draw(drawList, new Vector2(min.X + inset, max.Y - 22f * scale),
            Loc.T(L.Casino.ChipRate), ui.MutedInk, TextStyles.Caption2);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private bool AnyRoomLive()
    {
        return casinoRooms.TryRoomClock(Core.Casino.CasinoRoomIds.WheelFloor, out _, out var wheelEnds)
            && wheelEnds > 0
            || casinoRooms.TryRoomClock(Core.Casino.CasinoRoomIds.BingoHall, out _, out var bingoEnds)
            && bingoEnds > 0;
    }

    private void DrawLiveStrip(float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var gap = LiveStripGap * scale;
        var cardWidth = (width - gap) * 0.5f;
        var height = LiveStripHeight * scale;

        var wheelRect = new Rect(origin, new Vector2(origin.X + cardWidth, origin.Y + height));
        if (DrawLiveCard(wheelRect, CasinoGames.Wheel, Core.Casino.CasinoRoomIds.WheelFloor,
                L.Casino.GameWheel, scale))
        {
            OpenGame(CasinoGames.Wheel);
        }

        var bingoMin = new Vector2(origin.X + cardWidth + gap, origin.Y);
        var bingoRect = new Rect(bingoMin, new Vector2(bingoMin.X + cardWidth, bingoMin.Y + height));
        if (DrawLiveCard(bingoRect, CasinoGames.Bingo, Core.Casino.CasinoRoomIds.BingoHall,
                L.Casino.GameBingo, scale))
        {
            OpenGame(CasinoGames.Bingo);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private bool DrawLiveCard(Rect card, string gameId, string roomId, LocString name, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = 12f * scale;
        var occupancy = casinoRooms.OccupancyOf(roomId);
        var live = occupancy > 0;
        var dotCenter = new Vector2(card.Min.X + inset + 3f * scale, card.Min.Y + inset + 4f * scale);
        drawList.AddCircleFilled(dotCenter, 3.5f * scale,
            ImGui.GetColorU32(live ? ui.Accent : Palette.WithAlpha(ui.MutedInk, 0.6f)), 12);

        var headcount = Apps.Games.Framework.GameNumber.Label(occupancy);
        Typography.Draw(drawList, new Vector2(dotCenter.X + 9f * scale, card.Min.Y + inset - 2f * scale),
            Loc.T(L.Casino.LivePlayers, headcount), live ? ui.Accent : ui.MutedInk, TextStyles.Caption1);

        var title = Typography.FitText(Loc.T(name), card.Width - inset * 2f, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(card.Min.X + inset, card.Min.Y + 28f * scale), title, ui.TitleInk,
            TextStyles.Headline);

        var phaseLine = RoomPhaseLine(gameId, roomId);
        if (phaseLine.Length > 0)
        {
            var fitted = Typography.FitText(phaseLine, card.Width - inset * 2f, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(card.Min.X + inset, card.Min.Y + 52f * scale), fitted,
                ui.MutedInk, TextStyles.Footnote);
        }

        var stake = Typography.FitText(MinimumStakeLine(gameId), card.Width - inset * 2f, TextStyles.Caption2);
        Typography.Draw(drawList, new Vector2(card.Min.X + inset, card.Max.Y - 20f * scale), stake,
            Palette.WithAlpha(ui.MutedInk, 0.85f), TextStyles.Caption2);

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private string RoomPhaseLine(string gameId, string roomId)
    {
        if (!casinoRooms.TryRoomClock(roomId, out var phase, out var endsAtUnixMs) || endsAtUnixMs <= 0)
        {
            return string.Empty;
        }

        var remaining = casinoRooms.Room.RemainingMilliseconds(endsAtUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (remaining <= 0)
        {
            return string.Empty;
        }

        var seconds = (int)((remaining + 999) / 1000);
        var clock = TimeText.Duration(seconds);
        var bingo = string.Equals(gameId, CasinoGames.Bingo, StringComparison.Ordinal);
        if (phase == Core.Casino.CasinoRoomPhases.Open)
        {
            return bingo ? Loc.T(L.Casino.BingoCardsClose, clock) : Loc.T(L.Casino.WheelBetsCloseIn, clock);
        }

        return Loc.T(L.Casino.RoomNextIn, clock);
    }

    private static string MinimumStakeLine(string gameId)
    {
        var minimum = MinimumStakeOf(gameId);
        return minimum > 0
            ? Loc.T(L.Casino.MinimumStake, minimum.ToString("N0", Loc.Culture))
            : string.Empty;
    }

    private static long MinimumStakeOf(string gameId) => gameId switch
    {
        CasinoGames.Slots => Core.Casino.SlotsRules.MinStake,
        CasinoGames.Wheel => Core.Casino.WheelRules.MinStakePerSpot,
        CasinoGames.Bingo => Core.Casino.BingoRules.CardPrice,
        CasinoGames.Scratch => Core.Casino.ScratchRules.Prices[0],
        CasinoGames.Barkeep => Core.Casino.BarkeepRules.EntryChips,
        CasinoGames.Blackjack => Core.Casino.BlackjackRules.MinBet,
        _ => 0,
    };
}
