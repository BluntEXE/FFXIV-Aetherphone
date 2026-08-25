using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Apps;
using Aetherphone.Core.GameRooms;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Online;

// One room, three faces: the lobby with the code and the roster, the live Uno table, and the
// winner screen that leads back to another round. Everything rendered here is the server's word;
// a tap only ever sends an intent and the next event repaints the truth.
internal sealed class OnlineRoomView
{
    private const float HeaderHeight = 42f;
    private const float RosterRowHeight = 56f;
    private const float HandCardWidth = 46f;
    private const float HandCardHeight = 66f;
    private const float TableCardWidth = 56f;
    private const float TableCardHeight = 80f;
    private const long NoticeMilliseconds = 4_000;

    private readonly GameRoomsStore store;

    private string inlineReason = string.Empty;
    private long noticeAtTick;
    private long copiedAtTick;
    private int wildPendingCard = -1;

    public OnlineRoomView(GameRoomsStore store)
    {
        this.store = store;
    }

    public void Enter()
    {
        inlineReason = string.Empty;
        wildPendingCard = -1;
    }

    public void Draw(in PhoneContext context, Action back)
    {
        AppHeader.Draw(context, Loc.T(L.Games.OnlineUno), back);
        var scale = UiScale.Current;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + HeaderHeight * scale), content.Max);
        var theme = context.Theme;
        Consume(back);

        var session = store.Room;
        if (session.RoomId.Length == 0)
        {
            DrawClosed(body, theme, scale, session.ClosedReason, back);
            return;
        }

        var held = session.State;
        var board = held?.Uno;
        if (held is null || board is null)
        {
            DrawCenteredNotice(body, theme, Loc.T(L.Games.OnlineLoading));
            return;
        }

        if (held.Snapshot.Phase == GameRoomWire.PhasePlaying)
        {
            DrawTable(body, theme, scale, held.Snapshot, board);
            return;
        }

        DrawLobby(body, theme, scale, board, held.Snapshot.Phase);
    }

    private void Consume(Action back)
    {
        var act = store.TakeActOutcome();
        if (act is not null && !act.Granted && act.Reason.Length > 0)
        {
            inlineReason = act.Reason;
            noticeAtTick = Environment.TickCount64;
        }

        var answer = store.TakeRoomAnswer();
        if (answer is null)
        {
            return;
        }

        if (answer.Intent is GameRoomIntent.Left or GameRoomIntent.Closed && answer.Granted)
        {
            back();
            return;
        }

        if (!answer.Granted && answer.Reason.Length > 0)
        {
            inlineReason = answer.Reason;
            noticeAtTick = Environment.TickCount64;
        }
    }

    private void DrawClosed(Rect body, PhoneTheme theme, float scale, string reason, Action back)
    {
        var message = reason switch
        {
            GameRoomWire.ReasonKicked => Loc.T(L.Games.OnlineKicked),
            GameRoomWire.ReasonRestarting => Loc.T(L.Games.OnlineRestarting),
            _ => Loc.T(L.Games.OnlineRoomEnded),
        };
        DrawCenteredNotice(body, theme, message);
        var accent = Core.Apps.AppAccents.For("games");
        if (GameHud.Button(new Vector2(body.Center.X, body.Center.Y + 48f * scale),
                new Vector2(140f * scale, 36f * scale), Loc.T(L.Common.Cancel), accent, theme))
        {
            back();
        }
    }

    private static void DrawCenteredNotice(Rect body, PhoneTheme theme, string message)
    {
        Typography.DrawWrappedCentered(ImGui.GetWindowDrawList(), body.Center, message, theme.TextMuted,
            TextStyles.Subheadline, MathF.Min(body.Width - 48f, 280f * UiScale.Current));
    }

    // The lobby and the finished screen are the same room at rest: the roster, the code, and one
    // primary button whose label is the only thing the phase changes.
    private void DrawLobby(Rect body, PhoneTheme theme, float scale, UnoRoomStateDto board, int phase)
    {
        using var surface = AppSurface.Begin(body);
        var accent = Core.Apps.AppAccents.For("games");
        var players = board.Players ?? Array.Empty<UnoPlayerDto>();
        var isHost = string.Equals(board.HostUserId, store.AccountId, StringComparison.Ordinal);

        if (phase == GameRoomWire.PhaseFinished)
        {
            DrawWinnerBanner(theme, scale, board, players);
        }

        DrawCodeCard(theme, scale, accent);
        if (inlineReason.Length > 0 && Environment.TickCount64 - noticeAtTick < NoticeMilliseconds)
        {
            DrawInlineNotice(theme, scale);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var card = GroupCard.Begin(theme, players.Length == 0 ? 1 : players.Length, RosterRowHeight);
        for (var index = 0; index < players.Length; index++)
        {
            DrawRosterRow(card.NextRow(), theme, scale, board, players[index], isHost);
        }

        if (players.Length == 0)
        {
            card.NextRow();
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var buttonSize = new Vector2(width * 0.62f, 40f * scale);
        var primaryCenter = new Vector2(origin.X + width * 0.5f, origin.Y + buttonSize.Y * 0.5f);
        if (isHost)
        {
            var enough = players.Length >= 2;
            var label = phase == GameRoomWire.PhaseFinished
                ? Loc.T(L.Games.OnlineRematch)
                : Loc.T(L.Games.OnlineStart);
            if (GameHud.Button(primaryCenter, buttonSize, label, enough ? accent : theme.TextMuted, theme)
                && enough && !store.ActInFlight)
            {
                store.SendStart();
            }

            if (!enough)
            {
                Typography.DrawCentered(ImGui.GetWindowDrawList(),
                    new Vector2(primaryCenter.X, primaryCenter.Y + 32f * scale),
                    Loc.T(L.Games.OnlineNeedPlayers), theme.TextMuted, TextStyles.Footnote);
            }
        }
        else
        {
            Typography.DrawCentered(ImGui.GetWindowDrawList(), primaryCenter,
                Loc.T(L.Games.OnlineWaitingHost), theme.TextMuted, TextStyles.Subheadline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, buttonSize.Y + 44f * scale));

        var leaveOrigin = ImGui.GetCursorScreenPos();
        var leaveCenter = new Vector2(leaveOrigin.X + width * 0.5f, leaveOrigin.Y + 18f * scale);
        var leaveLabel = isHost ? Loc.T(L.Games.OnlineCloseRoom) : Loc.T(L.Games.OnlineLeave);
        if (GameHud.Button(leaveCenter, new Vector2(width * 0.5f, 34f * scale), leaveLabel,
                new Vector4(0.85f, 0.35f, 0.32f, 1f), theme) && !store.IntentInFlight)
        {
            var roomId = store.Room.RoomId;
            if (isHost)
            {
                store.CloseRoom(roomId);
            }
            else
            {
                store.LeaveRoom(roomId);
            }
        }

        ImGui.SetCursorScreenPos(leaveOrigin);
        ImGui.Dummy(new Vector2(width, 40f * scale + Metrics.Space.Lg * scale));
    }

    private void DrawWinnerBanner(PhoneTheme theme, float scale, UnoRoomStateDto board,
        UnoPlayerDto[] players)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = 54f * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var accent = Core.Apps.AppAccents.For("games");
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(Palette.WithAlpha(accent, 0.14f)));
        Squircle.Stroke(drawList, origin, max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(Palette.WithAlpha(accent, 0.4f)), 1f * scale);
        var message = board.WinnerSeat >= 0 && board.WinnerSeat < players.Length
            ? Loc.T(L.Games.OnlineWinner, players[board.WinnerSeat].DisplayName)
            : Loc.T(L.Games.OnlineRoundVoid);
        Typography.DrawCentered(drawList, new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f),
            Typography.FitText(message, width - 24f * scale, TextStyles.SubheadlineEmphasized),
            theme.TextStrong, TextStyles.SubheadlineEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawCodeCard(PhoneTheme theme, float scale, Vector4 accent)
    {
        var code = RoomCode();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = 64f * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(theme.GroupedCard));

        Typography.Draw(drawList, new Vector2(origin.X + 14f * scale, origin.Y + 10f * scale),
            Loc.T(L.Games.OnlineRoomCode), theme.TextMuted, TextStyles.Caption1);
        var spaced = code.Length == 0 ? "······" : string.Join(' ', code.ToCharArray());
        Typography.Draw(drawList, new Vector2(origin.X + 14f * scale, origin.Y + 28f * scale), spaced,
            theme.TextStrong, TextStyles.Title2);

        var copied = Environment.TickCount64 - copiedAtTick < 1500;
        var pillCenter = new Vector2(max.X - 52f * scale, origin.Y + height * 0.5f);
        if (GameHud.Button(pillCenter, new Vector2(80f * scale, 32f * scale),
                Loc.T(copied ? L.Games.OnlineCodeCopied : L.Games.OnlineCopyCode), accent, theme)
            && code.Length > 0)
        {
            ImGui.SetClipboardText(code);
            copiedAtTick = Environment.TickCount64;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private string RoomCode()
    {
        var rooms = store.Rooms;
        var roomId = store.Room.RoomId;
        for (var index = 0; index < rooms.Length; index++)
        {
            if (string.Equals(rooms[index].RoomId, roomId, StringComparison.Ordinal))
            {
                return rooms[index].JoinCode;
            }
        }

        return string.Empty;
    }

    private void DrawRosterRow(Rect row, PhoneTheme theme, float scale, UnoRoomStateDto board,
        UnoPlayerDto player, bool viewerIsHost)
    {
        var drawList = ImGui.GetWindowDrawList();
        var isRoomHost = string.Equals(player.UserId, board.HostUserId, StringComparison.Ordinal);
        var name = player.DisplayName;
        if (isRoomHost)
        {
            name = name + " · " + Loc.T(L.Games.OnlineHostBadge);
        }

        if (player.Away)
        {
            name = name + " · " + Loc.T(L.Games.OnlineAway);
        }

        var kickReserve = viewerIsHost && !isRoomHost ? 86f * scale : 0f;
        var textWidth = row.Width - kickReserve - 8f * scale;
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - 16f * scale),
            Typography.FitText(name, textWidth, TextStyles.SubheadlineEmphasized),
            player.Away ? theme.TextMuted : theme.TextStrong, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y + 4f * scale),
            Loc.T(L.Games.OnlineWins, player.Wins.ToString(Loc.Culture)), theme.TextMuted,
            TextStyles.Footnote);

        if (kickReserve > 0f && GameHud.Button(
                new Vector2(row.Max.X - 40f * scale, row.Center.Y),
                new Vector2(76f * scale, 28f * scale), Loc.T(L.Games.OnlineKick),
                new Vector4(0.85f, 0.35f, 0.32f, 1f), theme) && !store.IntentInFlight)
        {
            store.Kick(player.UserId);
        }
    }

    private void DrawInlineNotice(PhoneTheme theme, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var message = Loc.T(GamesOnlineText.ReasonMessage(inlineReason));
        Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + 4f * scale), message, theme.TextMuted,
            TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 22f * scale));
    }

    // The live table. Opponents up top with card counts and the turn ring, the pile and the
    // discard in the middle, the hand at the bottom. Taps send intents; the board only moves when
    // the server answers.
    private void DrawTable(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot,
        UnoRoomStateDto board)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, UnoCardArt.ColorFor(board.ActiveColor));
        var players = board.Players ?? Array.Empty<UnoPlayerDto>();
        var mySeat = SeatOf(players, store.AccountId);
        var mine = store.Room.Private?.Uno;
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var remaining = store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, nowMs);
        var myTurn = mySeat >= 0 && board.TurnSeat == mySeat;

        DrawOpponents(drawList, body, theme, scale, board, players, mySeat, remaining);
        DrawCenter(drawList, body, theme, scale, board, myTurn, remaining);
        DrawStatus(drawList, body, theme, scale, board, players, myTurn);
        DrawHand(drawList, body, theme, scale, board, mine, myTurn);
        if (wildPendingCard >= 0)
        {
            DrawColorPicker(drawList, body, theme, scale);
        }
    }

    private void DrawOpponents(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale,
        UnoRoomStateDto board, UnoPlayerDto[] players, int mySeat, long remaining)
    {
        var others = players.Length - (mySeat >= 0 ? 1 : 0);
        if (others <= 0)
        {
            return;
        }

        var bandTop = body.Min.Y + 10f * scale;
        var slotWidth = body.Width / others;
        var slot = 0;
        for (var offset = 1; offset <= players.Length; offset++)
        {
            var seat = mySeat >= 0 ? (mySeat + offset) % players.Length : offset - 1;
            if (seat == mySeat || slot >= others)
            {
                continue;
            }

            var player = players[seat];
            var centerX = body.Min.X + slotWidth * (slot + 0.5f);
            var cardRect = new Rect(
                new Vector2(centerX - 14f * scale, bandTop),
                new Vector2(centerX + 14f * scale, bandTop + 40f * scale));
            var dim = player.Away ? 0.35f : 1f;
            UnoCardArt.DrawBack(drawList, cardRect, scale, dim);
            Typography.DrawCentered(drawList, new Vector2(centerX + 20f * scale, bandTop + 12f * scale),
                player.CardCount.ToString(Loc.Culture), theme.TextStrong with { W = dim },
                TextStyles.SubheadlineEmphasized);
            var name = Typography.FitText(player.DisplayName, slotWidth - 8f * scale, TextStyles.Caption1);
            Typography.DrawCentered(drawList, new Vector2(centerX, bandTop + 52f * scale), name,
                (board.TurnSeat == seat ? theme.TextStrong : theme.TextMuted) with { W = dim },
                TextStyles.Caption1);
            if (board.TurnSeat == seat && board.WinnerSeat < 0)
            {
                TurnTimerRing.Draw(drawList, new Vector2(centerX, bandTop + 68f * scale), 8f * scale,
                    remaining, board.TurnSeconds, Core.Apps.AppAccents.For("games"), scale);
            }

            slot++;
        }
    }

    private void DrawCenter(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale,
        UnoRoomStateDto board, bool myTurn, long remaining)
    {
        var centerY = body.Min.Y + body.Height * 0.40f;
        var cardHalf = new Vector2(TableCardWidth * 0.5f * scale, TableCardHeight * 0.5f * scale);

        // The draw pile doubles as the draw button: on your turn with no pending card it glows.
        var deckCenter = new Vector2(body.Center.X - 52f * scale, centerY);
        var deckRect = new Rect(deckCenter - cardHalf, deckCenter + cardHalf);
        var canDraw = myTurn && !board.PendingDraw && !store.ActInFlight && board.WinnerSeat < 0;
        var deckHovered = canDraw && UiInteract.Hover(deckRect.Min, deckRect.Max);
        UnoCardArt.DrawBack(drawList, deckRect, scale, canDraw ? 1f : 0.75f);
        if (canDraw)
        {
            Squircle.Stroke(drawList, deckRect.Min, deckRect.Max, TableCardWidth * 0.18f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(Core.Apps.AppAccents.For("games"),
                    deckHovered ? 1f : 0.6f)), 2f * scale);
            if (deckHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(deckRect.Min, deckRect.Max, deckHovered))
            {
                store.SendDraw();
            }
        }

        Typography.DrawCentered(drawList,
            new Vector2(deckCenter.X, deckRect.Max.Y + 12f * scale),
            Loc.T(L.Games.OnlineDeck) + " " + board.DrawPileCount.ToString(Loc.Culture), theme.TextMuted,
            TextStyles.Caption1);

        var discardCenter = new Vector2(body.Center.X + 52f * scale, centerY);
        var discardRect = new Rect(discardCenter - cardHalf, discardCenter + cardHalf);
        if (board.DiscardTop >= 0)
        {
            UnoCardArt.DrawFace(drawList, discardRect, board.DiscardTop, scale, false);
        }

        // Wilds leave the top card colorless, so the chosen color rides a halo around the discard.
        drawList.AddCircleFilled(new Vector2(discardCenter.X, discardRect.Max.Y + 14f * scale), 6f * scale,
            ImGui.GetColorU32(UnoCardArt.ColorFor(board.ActiveColor)), 24);

        DrawDirection(drawList, new Vector2(body.Center.X, centerY), 86f * scale, board.Clockwise, theme,
            scale);
        if (myTurn && board.WinnerSeat < 0)
        {
            TurnTimerRing.Draw(drawList, new Vector2(body.Center.X, centerY), 96f * scale, remaining,
                board.TurnSeconds, Core.Apps.AppAccents.For("games"), scale);
        }
    }

    private static void DrawDirection(ImDrawListPtr drawList, Vector2 center, float radius, bool clockwise,
        PhoneTheme theme, float scale)
    {
        var color = ImGui.GetColorU32(theme.TextMuted with { W = 0.55f });
        for (var side = 0; side < 2; side++)
        {
            var angle = side == 0 ? -MathF.PI * 0.5f : MathF.PI * 0.5f;
            var tip = clockwise ? 0.22f : -0.22f;
            var at = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var forward = new Vector2(MathF.Cos(angle + (tip > 0 ? MathF.PI * 0.5f : -MathF.PI * 0.5f)),
                MathF.Sin(angle + (tip > 0 ? MathF.PI * 0.5f : -MathF.PI * 0.5f))) * 9f * scale;
            var wing = new Vector2(-forward.Y, forward.X) * 0.55f;
            drawList.AddTriangleFilled(at + forward, at - forward * 0.4f + wing, at - forward * 0.4f - wing,
                color);
        }
    }

    private void DrawStatus(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale,
        UnoRoomStateDto board, UnoPlayerDto[] players, bool myTurn)
    {
        var y = body.Min.Y + body.Height * 0.40f + TableCardHeight * 0.5f * scale + 34f * scale;
        string status;
        if (myTurn)
        {
            status = Loc.T(L.Games.OnlineYourTurn);
        }
        else if (board.TurnSeat >= 0 && board.TurnSeat < players.Length)
        {
            status = Loc.T(L.Games.OnlineTheirTurn, players[board.TurnSeat].DisplayName);
        }
        else
        {
            status = string.Empty;
        }

        if (!store.Room.Attached)
        {
            status = status.Length == 0
                ? Loc.T(L.Games.OnlineReconnecting)
                : status + " · " + Loc.T(L.Games.OnlineReconnecting);
        }

        if (inlineReason.Length > 0 && Environment.TickCount64 - noticeAtTick < NoticeMilliseconds)
        {
            status = Loc.T(GamesOnlineText.ReasonMessage(inlineReason));
        }

        if (status.Length > 0)
        {
            Typography.DrawCentered(drawList, new Vector2(body.Center.X, y),
                Typography.FitText(status, body.Width - 24f * scale, TextStyles.Subheadline),
                myTurn ? theme.TextStrong : theme.TextMuted, TextStyles.Subheadline);
        }
    }

    private void DrawHand(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale,
        UnoRoomStateDto board, UnoYouDto? mine, bool myTurn)
    {
        var hand = mine?.Hand ?? Array.Empty<int>();
        var pending = myTurn && board.PendingDraw && mine is not null && mine.PendingDrawnPlayable;
        var bandBottom = body.Max.Y - 10f * scale;
        var bandTop = bandBottom - HandCardHeight * scale;

        if (pending)
        {
            var passCenter = new Vector2(body.Center.X, bandTop - 26f * scale);
            if (GameHud.Button(passCenter, new Vector2(110f * scale, 32f * scale), Loc.T(L.Games.OnlinePass),
                    Core.Apps.AppAccents.For("games"), theme) && !store.ActInFlight)
            {
                store.SendPass();
            }
        }

        if (hand.Length == 0)
        {
            return;
        }

        var cardWidth = HandCardWidth * scale;
        var available = body.Width - 24f * scale;
        var step = hand.Length <= 1
            ? 0f
            : MathF.Min(cardWidth + 6f * scale, (available - cardWidth) / (hand.Length - 1));
        var spanWidth = cardWidth + step * (hand.Length - 1);
        var left = body.Center.X - spanWidth * 0.5f;

        var hoveredIndex = -1;
        var mouse = ImGui.GetMousePos();
        for (var index = hand.Length - 1; index >= 0; index--)
        {
            var cardLeft = left + step * index;
            var rightEdge = index == hand.Length - 1 ? cardLeft + cardWidth : cardLeft + step;
            if (mouse.X >= cardLeft && mouse.X < rightEdge && mouse.Y >= bandTop - 16f * scale
                && mouse.Y <= bandBottom)
            {
                hoveredIndex = index;
                break;
            }
        }

        for (var index = 0; index < hand.Length; index++)
        {
            var card = hand[index];
            var playable = myTurn && board.WinnerSeat < 0 && !store.ActInFlight
                && GameRoomWire.IsPlayable(card, board.ActiveColor, board.DiscardTop)
                && (!pending || card == mine!.PendingDrawnCard);
            var lifted = index == hoveredIndex && playable;
            var top = lifted ? bandTop - 14f * scale : bandTop;
            var rect = new Rect(new Vector2(left + step * index, top),
                new Vector2(left + step * index + cardWidth, top + HandCardHeight * scale));
            UnoCardArt.DrawFace(drawList, rect, card, scale, lifted, playable ? 1f : 0.55f);
            if (lifted)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (lifted && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (GameRoomWire.IsWild(card))
                {
                    wildPendingCard = card;
                }
                else
                {
                    store.SendPlay(card, -1);
                }
            }
        }
    }

    private void DrawColorPicker(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale)
    {
        drawList.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.62f)));
        var panelHalf = new Vector2(120f * scale, 74f * scale);
        var panel = new Rect(body.Center - panelHalf, body.Center + panelHalf);
        Squircle.Fill(drawList, panel.Min, panel.Max, 18f * scale, ImGui.GetColorU32(theme.AppBackground));
        Squircle.Stroke(drawList, panel.Min, panel.Max, 18f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f)), 1f * scale);
        Typography.DrawCentered(drawList, new Vector2(panel.Center.X, panel.Min.Y + 18f * scale),
            Loc.T(L.Games.OnlinePickColor), theme.TextStrong, TextStyles.SubheadlineEmphasized);

        var swatch = 38f * scale;
        var gap = 12f * scale;
        var rowWidth = swatch * 4f + gap * 3f;
        var startX = panel.Center.X - rowWidth * 0.5f;
        var top = panel.Min.Y + 36f * scale;
        for (var color = 0; color < 4; color++)
        {
            var min = new Vector2(startX + color * (swatch + gap), top);
            var max = min + new Vector2(swatch, swatch);
            var hovered = UiInteract.Hover(min, max);
            Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(UnoCardArt.ColorFor(color)));
            if (hovered)
            {
                Squircle.Stroke(drawList, min, max, 10f * scale,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), 2f * scale);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                store.SendPlay(wildPendingCard, color);
                wildPendingCard = -1;
                return;
            }
        }

        // A tap anywhere outside the panel puts the wild back in the hand.
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !UiInteract.Hover(panel.Min, panel.Max))
        {
            wildPendingCard = -1;
        }
    }

    private static int SeatOf(UnoPlayerDto[] players, string userId)
    {
        for (var index = 0; index < players.Length; index++)
        {
            if (string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return players[index].Seat;
            }
        }

        return -1;
    }
}
