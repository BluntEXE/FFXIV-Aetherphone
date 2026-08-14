using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackTable
{
    private const float PadX = 16f;
    private const float StatusRowHeight = 22f;
    private const float BannerHeight = 30f;
    private const float ActionBarHeight = 46f;
    private const float CardWidth = 30f;
    private const float CardOverlap = 0.42f;
    private const float DealerCardWidth = 34f;

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private static readonly int[] ActionBits =
    {
        BlackjackRules.ActionHit,
        BlackjackRules.ActionStand,
        BlackjackRules.ActionDouble,
        BlackjackRules.ActionSplit,
    };

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly CasinoTablesStore tables;
    private readonly CasinoTurnNotifier turns;
    private readonly BlackjackSeatFlow seatFlow;
    private readonly Action openCashier;
    private readonly Action leaveRoom;
    private readonly Action knock;
    private readonly BlackjackProjection projection = new();
    private readonly BlackjackDealPlayback playback = new();
    private readonly DealerBubble dealer = new();
    private readonly BetComposer composer = new("##blackjackBet");
    private readonly ParticleSystem particles = new(160);
    private readonly SeatView[] seatViews = new SeatView[BlackjackRules.SeatCount];
    private readonly Vector2[] seatCenters = new Vector2[BlackjackRules.SeatCount];

    private RollingValue winRoll;
    private int mySeat = -1;
    private string roomId = CasinoRoomIds.BlackjackPit;
    private string inlineReason = string.Empty;
    private string celebratedHandId = string.Empty;
    private string spokenHandId = string.Empty;
    private long settledDelta;
    private int spokenPhase = -1;
    private int spokenSeat = int.MinValue;
    private bool entered;

    public BlackjackTable(CasinoStore chips, CasinoRoomsStore rooms, CasinoTablesStore tables,
        CasinoTurnNotifier turns, Action openCashier, Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.tables = tables;
        this.turns = turns;
        this.openCashier = openCashier;
        this.leaveRoom = leaveRoom;
        knock = Knock;
        seatFlow = new BlackjackSeatFlow(tables);
    }

    public void Enter(string tableId)
    {
        entered = true;
        roomId = tableId.Length > 0 ? tableId : CasinoRoomIds.BlackjackPit;
        inlineReason = string.Empty;
        mySeat = -1;
        projection.Reset();
        playback.Reset();
        seatFlow.Reset();
        turns.Forget();
        composer.Reset(BlackjackRules.MinBet);
        rooms.Enter(roomId);
    }

    public void Reset()
    {
        if (entered)
        {
            rooms.Leave();
            seatFlow.Left();
        }

        entered = false;
        mySeat = -1;
        projection.Reset();
        playback.Reset();
        dealer.Clear();
        particles.Clear();
        seatFlow.Reset();
        turns.Forget();
        inlineReason = string.Empty;
        celebratedHandId = string.Empty;
        spokenHandId = string.Empty;
        spokenPhase = -1;
        spokenSeat = int.MinValue;
        settledDelta = 0;
        winRoll.Snap(0);
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        turns.StampAttention();
        ConsumeStakeResults();

        var room = rooms.Room;
        var closedReason = room.ClosedReason;
        var pad = PadX * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;
        var drawList = ImGui.GetWindowDrawList();

        if (closedReason.Length > 0)
        {
            DrawClosedNotice(drawList, ui, closedReason, left, body.Min.Y + Metrics.Space.Lg * scale, width, scale);
            return;
        }

        var held = room.State;
        var snapshot = held?.Snapshot;
        var state = chips.State;
        projection.Watch(rooms.AccountId);
        projection.Apply(held);
        projection.ApplyPersonal(room.Private);
        var board = projection.Board;
        mySeat = projection.MySeat;
        var nowTick = Environment.TickCount64;
        seatFlow.Observe(board, mySeat, board?.Phase ?? BlackjackPhases.Betting, nowTick);
        if (seatFlow.Reason.Length > 0)
        {
            inlineReason = seatFlow.Reason;
            seatFlow.ClearReason();
        }

        if (state is null || snapshot is null || board is null)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        playback.Update(board, board.Phase, delta);
        particles.Update(delta);
        dealer.Update(delta);

        var unreachable = room.Unreachable(nowTick);
        var veiled = unreachable && CasinoSeatMachine.Holds(seatFlow.Stage);
        if (veiled)
        {
            UiInteract.BlockThisFrame();
        }

        var localNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var deadlineRemaining = room.RemainingMilliseconds(board.DeadlineUnixMs, localNow);

        var y = body.Min.Y + Metrics.Space.Xs * scale;
        y = DrawStatusRow(drawList, ui, snapshot, board, !unreachable, left, y, width, scale);

        var footerHeight = FooterHeightFor(board.Phase, scale);
        var feltMin = new Vector2(left, y + Metrics.Space.Xs * scale);
        var feltMax = new Vector2(left + width, body.Max.Y - footerHeight - Metrics.Space.Md * scale);
        if (feltMax.Y <= feltMin.Y)
        {
            return;
        }

        var felt = new Rect(feltMin, feltMax);
        FeltPanel.Draw(drawList, felt, ui.Accent, scale);
        DrawDealer(drawList, ui, board, felt, scale);
        var tapped = DrawSeats(drawList, ui, board, felt, deadlineRemaining, scale);
        if (BlackjackRules.IsSeat(tapped) && seatViews[tapped].Phase == SeatPhase.Empty)
        {
            TapEmptySeat(tapped, state, board);
        }

        SpeakForPhase(board);
        dealer.Draw(drawList, new Vector2(felt.Center.X, felt.Min.Y + felt.Height * 0.30f), ui, scale);
        CelebrateSettledHand(board, scale);

        var footerTop = felt.Max.Y + Metrics.Space.Md * scale;
        DrawFooter(drawList, ui, state, board, snapshot, deadlineRemaining, delta, left, footerTop, width, scale,
            veiled);
        particles.Draw(drawList, scale);

        if (veiled)
        {
            ReconnectVeil.Draw(drawList, body, ui,
                room.RemainingMilliseconds(projection.SeatAt(mySeat)?.HeldUntilUnixMs ?? 0, localNow), scale);
        }
    }

    private void ConsumeStakeResults()
    {
        var result = rooms.TakeStakeResult();
        if (result is not null)
        {
            inlineReason = result.Granted
                ? string.Empty
                : result.Reason.Length > 0 ? result.Reason : CasinoReasons.Unreachable;
        }

        if (rooms.TakeStakeFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }
    }

    private void DrawClosedNotice(ImDrawListPtr drawList, AppSkin ui, string closedReason, float left, float y,
        float width, float scale)
    {
        var hint = Loc.T(CasinoReasons.TryMessage(closedReason, out var known) ? known : L.Casino.BlackjackClosedHint);
        if (AsksToJoin(closedReason))
        {
            DrawNotice(drawList, ui, Loc.T(L.Casino.BlackjackDoorTitle), hint, Loc.T(L.Casino.BlackjackAskToJoin),
                knock, left, y, width, scale);
            return;
        }

        DrawNotice(drawList, ui, Loc.T(L.Casino.BlackjackClosedTitle), hint, Loc.T(L.Casino.WheelBackToFloor),
            leaveRoom, left, y, width, scale);
    }

    internal static bool AsksToJoin(string reason)
    {
        return string.Equals(reason, CasinoReasons.InviteOnly, StringComparison.Ordinal)
            || string.Equals(reason, CasinoReasons.NotMember, StringComparison.Ordinal)
            || string.Equals(reason, CasinoReasons.Denied, StringComparison.Ordinal);
    }

    private void Knock()
    {
        tables.Knock(roomId);
    }

    private static float FooterHeightFor(int phase, float scale)
    {
        return phase == BlackjackPhases.Betting
            ? BannerHeight * scale + BetComposer.HeightFor(scale)
            : BannerHeight * scale + ActionBarHeight * scale;
    }

    private float DrawStatusRow(ImDrawListPtr drawList, AppSkin ui, CasinoRoomSnapshotDto snapshot,
        CasinoBlackjackRoomStateDto board, bool reachable, float left, float y, float width, float scale)
    {
        var height = StatusRowHeight * scale;
        var players = SeatedCount(board);
        var watching = snapshot.Occupancy > players ? snapshot.Occupancy - players : 0;
        var seated = watching > 0
            ? Loc.T(L.Casino.BlackjackAtTheTableWatching, GameNumber.Label(players),
                GameNumber.Label(watching))
            : Loc.T(L.Casino.BlackjackAtTheTable, GameNumber.Label(players));
        Typography.Draw(drawList, new Vector2(left, y + 4f * scale), seated, ui.MutedInk, TextStyles.Caption1);
        if (reachable && AwayAtMySeat())
        {
            AwayBadge.Draw(drawList, new Vector2(left + width, y), ui, Loc.T(L.Casino.AwayBadge), scale);
            return y + height;
        }

        if (reachable)
        {
            var seatedWidth = Typography.Measure(seated, TextStyles.Caption1).X;
            var room = width - seatedWidth - Metrics.Space.Md * scale;
            if (room > 0f)
            {
                var rules = Typography.FitText(Loc.T(L.Casino.BlackjackRules), room, TextStyles.Caption2);
                var rulesWidth = Typography.Measure(rules, TextStyles.Caption2).X;
                Typography.Draw(drawList, new Vector2(left + width - rulesWidth, y + 5f * scale), rules,
                    ui.MutedInk, TextStyles.Caption2);
            }

            return y + height;
        }

        var label = Loc.T(L.Casino.WheelReconnecting);
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        var chipMax = new Vector2(left + width, y + height - 2f * scale);
        var chipMin = new Vector2(chipMax.X - labelSize.X - 18f * scale, y);
        var chipRounding = (chipMax.Y - chipMin.Y) * 0.5f;
        Squircle.Fill(drawList, chipMin, chipMax, chipRounding, ImGui.GetColorU32(ui.FieldSurface));
        var dotCenter = new Vector2(chipMin.X + 8f * scale, (chipMin.Y + chipMax.Y) * 0.5f);
        drawList.AddCircleFilled(dotCenter, 2.6f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f + 0.45f * Pulse.Wave(Pulse.Breath))), 12);
        Typography.Draw(drawList, new Vector2(dotCenter.X + 6f * scale, chipMin.Y + 3f * scale), label, ui.MutedInk,
            TextStyles.Caption2);
        return y + height;
    }

    private void TapEmptySeat(int seatIndex, CasinoStateDto state, CasinoBlackjackRoomStateDto board)
    {
        var rack = CasinoWire.SittingFor(state, CasinoWire.BlackjackKind);
        if (rack is not null)
        {
            inlineReason = string.Empty;
            seatFlow.Sit(roomId, seatIndex, rack.Stack, board.Phase);
            return;
        }

        var buyIn = RackFor(state, board);
        if (buyIn <= 0)
        {
            openCashier();
            return;
        }

        inlineReason = string.Empty;
        seatFlow.Sit(roomId, seatIndex, buyIn, board.Phase);
    }

    private static long RackFor(CasinoStateDto state, CasinoBlackjackRoomStateDto board)
    {
        return BlackjackRules.RackFor(board.MaxBet, state.MinBuyIn, state.MaxBuyIn,
            state.Sitting?.Stack ?? 0);
    }

    private bool AwayAtMySeat()
    {
        var seat = projection.SeatAt(mySeat);
        return seat is not null && !seat.Connected;
    }

    private static int SeatedCount(CasinoBlackjackRoomStateDto board)
    {
        var seats = board.Seats;
        if (seats is null)
        {
            return 0;
        }

        var taken = 0;
        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].State != BlackjackSeatStates.Empty)
            {
                taken++;
            }
        }

        return taken;
    }

    private void DrawDealer(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, in Rect felt,
        float scale)
    {
        var anchor = SeatRing.DealerAnchor(felt);
        var cards = board.DealerCards;
        var count = cards?.Length ?? 0;
        if (count == 0)
        {
            return;
        }

        var cardWidth = DealerCardWidth * scale;
        var step = cardWidth * CardOverlap;
        var start = anchor.X - (count - 1) * step * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var travel = playback.TravelOf(index);
            if (travel <= 0f)
            {
                continue;
            }

            var target = new Vector2(start + index * step, anchor.Y);
            var center = BlackjackDealChoreography.Position(ShoeAnchor(felt), target, travel, scale);
            var rect = BlackjackDealChoreography.CardRect(center, cardWidth, travel);
            var card = cards![index];
            if (BlackjackDealChoreography.FaceUp(travel) && PlayingCards.IsCard(card))
            {
                PlayingCards.DrawFace(drawList, rect, card, PlayingCards.RoundingFor(cardWidth), scale, true);
            }
            else
            {
                PlayingCards.DrawBack(drawList, rect, PlayingCards.RoundingFor(cardWidth), scale, true);
            }
        }

        if (board.DealerTotal <= 0)
        {
            return;
        }

        Typography.DrawCentered(drawList,
            new Vector2(anchor.X, anchor.Y + PlayingCards.HeightFor(cardWidth) * 0.5f + 10f * scale),
            GameNumber.Label(board.DealerTotal), ui.TitleInk, TextStyles.Caption1);
    }

    private static Vector2 ShoeAnchor(in Rect felt)
    {
        return new Vector2(felt.Max.X - felt.Width * 0.12f, felt.Min.Y + felt.Height * 0.06f);
    }

    private int DrawSeats(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, in Rect felt,
        long turnRemaining, float scale)
    {
        BuildSeatViews(board);
        var style = new SeatRingStyle(ui.Accent, ui.TitleInk, ui.BodyInk, ui.MutedInk, ui.FieldSurface, Gold);
        var tapped = SeatRing.Draw(drawList, felt, seatViews, mySeat, scale, style, turnRemaining,
            board.WindowSeconds);
        SeatRing.Layout(felt, BlackjackRules.SeatCount, mySeat, seatCenters);
        DrawHands(drawList, ui, board, felt, scale);
        return tapped;
    }

    private void BuildSeatViews(CasinoBlackjackRoomStateDto board)
    {
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            var seat = projection.SeatAt(seatIndex);
            if (seat is null || seat.State == BlackjackSeatStates.Empty)
            {
                seatViews[seatIndex] = new SeatView(seatIndex, string.Empty, 0, 0, SeatPhase.Empty, false, true);
                continue;
            }

            var phase = board.ActiveSeat == seatIndex
                ? SeatPhase.Acting
                : BlackjackRules.PhaseOf(seat.State, seat.Connected, seat.JoinsNextHand, seat.Committed > 0);
            seatViews[seatIndex] = new SeatView(seatIndex, seat.DisplayName, seat.Chips, seat.Committed, phase,
                seatIndex == mySeat, seat.Connected);
        }
    }

    private void DrawHands(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, in Rect felt,
        float scale)
    {
        var seats = board.Seats;
        if (seats is null)
        {
            return;
        }

        var ellipseCenter = SeatRing.EllipseCenter(felt);
        var puckRadius = SeatRing.PuckRadius * scale;
        var cardWidth = CardWidth * scale;
        var ordinal = board.DealerCards?.Length ?? 0;
        for (var index = 0; index < seats.Length; index++)
        {
            var seatIndex = seats[index].SeatIndex;
            if (!BlackjackRules.IsSeat(seatIndex))
            {
                continue;
            }

            var hands = projection.HandsAt(seatIndex);
            var anchor = SeatRing.Pushed(seatCenters[seatIndex], ellipseCenter, puckRadius,
                SeatRing.HandPushFactor);
            for (var handIndex = 0; handIndex < hands.Length; handIndex++)
            {
                var handAnchor = new Vector2(anchor.X + (handIndex - (hands.Length - 1) * 0.5f) * cardWidth * 1.15f,
                    anchor.Y);
                ordinal = DrawHand(drawList, ui, board, seatIndex, handIndex, hands[handIndex], handAnchor, felt,
                    cardWidth, ordinal, scale);
            }
        }
    }

    private int DrawHand(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, int seatIndex,
        int handIndex, CasinoBlackjackHandDto hand, Vector2 anchor, in Rect felt, float cardWidth, int ordinal,
        float scale)
    {
        var cards = hand.Cards;
        var count = cards?.Length ?? 0;
        var step = cardWidth * CardOverlap;
        var start = anchor.X - (count - 1) * step * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var travel = playback.TravelOf(ordinal + index);
            if (travel <= 0f)
            {
                continue;
            }

            var target = new Vector2(start + index * step, anchor.Y);
            var center = BlackjackDealChoreography.Position(ShoeAnchor(felt), target, travel, scale);
            var rect = BlackjackDealChoreography.CardRect(center, cardWidth, travel);
            var card = projection.CardAt(seatIndex, handIndex, index, cards![index]);
            if (BlackjackDealChoreography.FaceUp(travel) && PlayingCards.IsCard(card))
            {
                PlayingCards.DrawFace(drawList, rect, card, PlayingCards.RoundingFor(cardWidth), scale, true);
            }
            else
            {
                PlayingCards.DrawBack(drawList, rect, PlayingCards.RoundingFor(cardWidth), scale, true);
            }
        }

        var totalY = anchor.Y + PlayingCards.HeightFor(cardWidth) * 0.5f + 9f * scale;
        if (hand.Total > 0)
        {
            var active = board.ActiveSeat == seatIndex && board.ActiveHand == handIndex;
            var ink = hand.Outcome == BlackjackOutcomes.Bust ? ui.MutedInk : active ? ui.Accent : ui.BodyInk;
            Typography.DrawCentered(drawList, new Vector2(anchor.X, totalY), GameNumber.Label(hand.Total), ink,
                TextStyles.Caption2);
        }

        DrawHandOutcome(drawList, ui, hand, new Vector2(anchor.X, totalY + 13f * scale), scale);
        return ordinal + count;
    }

    private void DrawHandOutcome(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackHandDto hand, Vector2 center,
        float scale)
    {
        if (hand.Outcome == BlackjackOutcomes.Pending)
        {
            return;
        }

        var text = OutcomeLabel(hand);
        if (text.Length == 0)
        {
            return;
        }

        var won = hand.Delta > 0;
        var ink = won ? Gold : ui.MutedInk;
        var size = Typography.Measure(text, TextStyles.Caption2);
        var pad = 6f * scale;
        var min = new Vector2(center.X - size.X * 0.5f - pad, center.Y - size.Y * 0.5f - 2f * scale);
        var max = new Vector2(center.X + size.X * 0.5f + pad, center.Y + size.Y * 0.5f + 2f * scale);
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(won ? Gold : ui.TitleInk, won ? 0.18f : 0.10f)));
        Typography.DrawCentered(drawList, center, text, ink, TextStyles.Caption2);
    }

    private static string OutcomeLabel(CasinoBlackjackHandDto hand)
    {
        if (hand.Outcome == BlackjackOutcomes.Blackjack)
        {
            return Loc.T(L.Casino.BlackjackSeatNatural);
        }

        if (hand.Outcome == BlackjackOutcomes.Push)
        {
            return Loc.T(L.Casino.BlackjackSeatPush);
        }

        return hand.Delta > 0
            ? string.Concat("+", hand.Delta.ToString("N0", Loc.Culture))
            : string.Empty;
    }

    private void SpeakForPhase(CasinoBlackjackRoomStateDto board)
    {
        if (board.Phase == spokenPhase && board.ActiveSeat == spokenSeat
            && string.Equals(spokenHandId, board.HandId, StringComparison.Ordinal))
        {
            return;
        }

        spokenPhase = board.Phase;
        spokenSeat = board.ActiveSeat;
        spokenHandId = board.HandId;
        if (board.Phase == BlackjackPhases.Betting)
        {
            dealer.Show(Loc.T(L.Casino.BlackjackWaitingForBets));
            return;
        }

        if (BlackjackPhases.Over(board.Phase) && board.DealerTotal > 0)
        {
            dealer.Show(Loc.T(L.Casino.BlackjackDealerHas, GameNumber.Label(board.DealerTotal)));
            return;
        }

        if (board.ActiveSeat >= 0 && board.ActiveSeat == mySeat)
        {
            dealer.Show(Loc.T(L.Casino.BlackjackYourTurn));
        }
    }

    private void CelebrateSettledHand(CasinoBlackjackRoomStateDto board, float scale)
    {
        if (string.Equals(celebratedHandId, board.HandId, StringComparison.Ordinal))
        {
            return;
        }

        settledDelta = 0;
        if (board.HandId.Length == 0 || !BlackjackRules.IsSeat(mySeat))
        {
            return;
        }

        var hands = projection.HandsAt(mySeat);
        if (hands.Length == 0 || !Settled(hands))
        {
            return;
        }

        celebratedHandId = board.HandId;
        settledDelta = 0;
        for (var index = 0; index < hands.Length; index++)
        {
            settledDelta += hands[index].Delta;
        }

        winRoll.Snap(0);
        if (settledDelta <= 0)
        {
            return;
        }

        var origin = seatCenters[mySeat];
        var stake = TotalBet(hands);
        if (stake > 0 && settledDelta >= stake * 10)
        {
            particles.Confetti(origin, 90, ConfettiPalette, 330f * scale, 5f, 1.6f);
            particles.Sparkle(origin, 24, Gold, 190f * scale, 4f, 1.0f);
            return;
        }

        particles.Confetti(origin, 40, ConfettiPalette, 250f * scale, 4f, 1.2f);
    }

    private static bool Settled(CasinoBlackjackHandDto[] hands)
    {
        for (var index = 0; index < hands.Length; index++)
        {
            if (hands[index].Outcome == BlackjackOutcomes.Pending)
            {
                return false;
            }
        }

        return true;
    }

    private static long TotalBet(CasinoBlackjackHandDto[] hands)
    {
        var total = 0L;
        for (var index = 0; index < hands.Length; index++)
        {
            total += hands[index].Bet;
        }

        return total;
    }

    private void DrawFooter(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state,
        CasinoBlackjackRoomStateDto board, CasinoRoomSnapshotDto snapshot, long deadlineRemaining, float delta,
        float left, float y, float width, float scale, bool veiled)
    {
        var draining = snapshot.State != CasinoRoomStates.Live;
        y = DrawBanner(drawList, ui, state, board, draining, deadlineRemaining, delta, left, y, width, scale);
        var sitting = CasinoWire.SittingFor(state, CasinoWire.BlackjackKind);
        var bought = sitting is not null;
        var onBoard = BlackjackRules.IsSeat(mySeat);
        if (!bought || (!onBoard && !CasinoSeatMachine.Holds(seatFlow.Stage)))
        {
            DrawSitAction(ui, state, board, bought, left, y, width, scale);
            return;
        }

        var stakeBlocked = draining || state.Draining || state.StakesPaused;
        var seatStack = SeatStackOf(sitting!);
        if (onBoard && CasinoJoinGate.CanPlaceBet(board.Phase, true, seatFlow.Waiting, draining,
                state.StakesPaused || state.Draining))
        {
            DrawComposer(ui, state, seatStack, board, left, y, width, scale, delta, veiled);
            return;
        }

        if (onBoard && CasinoJoinGate.CanAct(true, seatFlow.Waiting, projection.ActionsMask != 0))
        {
            DrawActionBar(ui, board, seatStack, left, y, width, scale);
            return;
        }

        var standLabel = seatFlow.StandQueued
            ? Loc.T(L.Casino.StandQueued)
            : stakeBlocked ? Loc.T(L.Casino.CashOut) : Loc.T(L.Casino.StandAction);
        if (DrawSingleAction(ui, standLabel, !seatFlow.Busy && !seatFlow.StandQueued, left, y, width, scale))
        {
            inlineReason = string.Empty;
            seatFlow.Stand(roomId);
        }
    }

    private long SeatStackOf(CasinoSittingDto sitting)
    {
        var seat = projection.SeatAt(mySeat);
        return seat is not null && seat.Chips > 0 ? seat.Chips : sitting.Stack;
    }

    private float DrawBanner(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state,
        CasinoBlackjackRoomStateDto board, bool draining, long deadlineRemaining, float delta,
        float left, float y, float width, float scale)
    {
        var center = new Vector2(left + width * 0.5f, y + BannerHeight * scale * 0.4f);
        if (BlackjackPhases.Over(board.Phase) && settledDelta > 0)
        {
            winRoll.Update((int)Math.Min(settledDelta, int.MaxValue), delta);
            var amount = "+" + ((long)winRoll.Display).ToString("N0", Loc.Culture);
            Typography.DrawCentered(drawList, center, Loc.T(L.Casino.BlackjackYouWon, amount), Gold,
                TextStyles.Title3.Scale * winRoll.PopScale, TextStyles.Title3.Weight);
            return y + BannerHeight * scale;
        }

        var message = inlineReason.Length > 0
            ? Loc.T(CasinoReasons.MessageFor(inlineReason))
            : SeatTextFor(state, draining) ?? BannerTextFor(board, deadlineRemaining);
        Typography.DrawCentered(drawList, center, Typography.FitText(message, width, TextStyles.Caption1),
            ui.MutedInk, TextStyles.Caption1);
        return y + BannerHeight * scale;
    }

    private string? SeatTextFor(CasinoStateDto state, bool draining)
    {
        if (seatFlow.StandQueued)
        {
            return Loc.T(L.Casino.StandAtHandEnd);
        }

        if (seatFlow.Waiting)
        {
            return Loc.T(L.Casino.DealtNextHand);
        }

        if (draining || state.Draining)
        {
            return Loc.T(L.Casino.TableDrainingLine);
        }

        return state.StakesPaused ? Loc.T(L.Casino.PausedTitle) : null;
    }

    private string BannerTextFor(CasinoBlackjackRoomStateDto board, long deadlineRemaining)
    {
        if (board.Phase == BlackjackPhases.Betting)
        {
            var seconds = (int)((deadlineRemaining + 999) / 1000);
            return board.HandId.Length == 0 || deadlineRemaining <= 0
                ? Loc.T(L.Casino.BlackjackWaitingForBets)
                : Loc.T(L.Casino.BlackjackBetsCloseIn, TimeText.Duration(seconds));
        }

        if (BlackjackPhases.Over(board.Phase))
        {
            return Loc.T(L.Casino.BlackjackHandOver);
        }

        if (board.ActiveSeat < 0)
        {
            return Loc.T(L.Casino.BlackjackDealing);
        }

        return board.ActiveSeat == mySeat
            ? Loc.T(L.Casino.BlackjackYourTurn)
            : Loc.T(L.Casino.BlackjackDealerPlays);
    }

    private void DrawComposer(AppSkin ui, CasinoStateDto state, long seatStack,
        CasinoBlackjackRoomStateDto board, float left, float y, float width, float scale, float delta, bool veiled)
    {
        var minimum = board.MinBet > 0 ? board.MinBet : BlackjackRules.MinBet;
        var maximum = board.MaxBet > 0 ? board.MaxBet : BlackjackRules.MaxBet;
        composer.Prefill(minimum);
        var blocked = veiled || state.StakesPaused || state.Draining || rooms.StakeInFlight;
        var bounds = new Rect(new Vector2(left, y),
            new Vector2(left + width, y + BetComposer.HeightFor(scale)));
        var label = Loc.T(L.Casino.BlackjackBetConfirm, composer.Amount.ToString("N0", Loc.Culture),
            BlackjackRules.BlackjackPayout(composer.Amount).ToString("N0", Loc.Culture));
        if (composer.Draw(ui, bounds, minimum, maximum, seatStack, BlackjackRules.BetStep, !blocked, label,
                delta))
        {
            inlineReason = string.Empty;
            rooms.PlaceBlackjackBet(composer.Amount);
        }
    }

    private void DrawActionBar(AppSkin ui, CasinoBlackjackRoomStateDto board, long seatStack, float left,
        float y, float width, float scale)
    {
        var mask = projection.ActionsMask;
        var offered = 0;
        for (var index = 0; index < ActionBits.Length; index++)
        {
            if (BlackjackRules.Allows(mask, ActionBits[index]))
            {
                offered++;
            }
        }

        if (offered == 0)
        {
            return;
        }

        var gap = Metrics.Space.Xs * scale;
        var buttonWidth = (width - gap * (offered - 1)) / offered;
        var height = ActionBarHeight * scale;
        var cost = ActiveBetOf(board);
        var affordable = seatStack >= cost;
        var drawn = 0;
        for (var index = 0; index < ActionBits.Length; index++)
        {
            var bit = ActionBits[index];
            if (!BlackjackRules.Allows(mask, bit))
            {
                continue;
            }

            var min = new Vector2(left + drawn * (buttonWidth + gap), y);
            var rect = new Rect(min, new Vector2(min.X + buttonWidth, min.Y + height));
            drawn++;
            var wagered = bit == BlackjackRules.ActionDouble || bit == BlackjackRules.ActionSplit;
            var legal = !rooms.StakeInFlight && (!wagered || affordable);
            var costLine = wagered ? cost.ToString("N0", Loc.Culture) : string.Empty;
            if (AppSkin.StackedPillButton(rect, LabelFor(bit), costLine, bit == BlackjackRules.ActionStand,
                    legal, ui.Theme) && legal)
            {
                inlineReason = string.Empty;
                rooms.SendBlackjackAction(bit);
            }
        }
    }

    private long ActiveBetOf(CasinoBlackjackRoomStateDto board)
    {
        var hands = projection.HandsAt(mySeat);
        var active = projection.ActiveHand;
        return active >= 0 && active < hands.Length ? hands[active].Bet : 0;
    }

    private static string LabelFor(int action)
    {
        return action switch
        {
            BlackjackRules.ActionHit => Loc.T(L.Casino.BlackjackActionHit),
            BlackjackRules.ActionStand => Loc.T(L.Casino.BlackjackActionStand),
            BlackjackRules.ActionDouble => Loc.T(L.Casino.BlackjackActionDouble),
            _ => Loc.T(L.Casino.BlackjackActionSplit),
        };
    }

    private void DrawSitAction(AppSkin ui, CasinoStateDto state, CasinoBlackjackRoomStateDto board, bool bought,
        float left, float y, float width, float scale)
    {
        var buyIn = bought
            ? CasinoWire.SittingFor(state, CasinoWire.BlackjackKind)?.Stack ?? 0
            : RackFor(state, board);
        if (!bought && buyIn <= 0)
        {
            if (DrawSingleAction(ui, Loc.T(L.Casino.BlackjackTakeSeat), true, left, y, width, scale))
            {
                openCashier();
            }

            return;
        }

        var seatIndex = FirstOpenSeat();
        var label = seatIndex < 0 ? Loc.T(L.Casino.TableFullBadge) : Loc.T(L.Casino.SitDownAction);
        if (DrawSingleAction(ui, label, seatIndex >= 0 && !seatFlow.Busy, left, y, width, scale))
        {
            inlineReason = string.Empty;
            seatFlow.Sit(roomId, seatIndex, buyIn, board.Phase);
        }
    }

    private int FirstOpenSeat()
    {
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            var seat = projection.SeatAt(seatIndex);
            if (seat is null || seat.State == BlackjackSeatStates.Empty)
            {
                return seatIndex;
            }
        }

        return -1;
    }

    private static bool DrawSingleAction(AppSkin ui, string label, bool enabled, float left, float y, float width,
        float scale)
    {
        var rect = new Rect(new Vector2(left + width * 0.2f, y),
            new Vector2(left + width * 0.8f, y + ActionBarHeight * scale));
        return AppSkin.PillButton(rect, label, true, enabled, ui.Theme) && enabled;
    }

    private static void DrawNotice(ImDrawListPtr drawList, AppSkin ui, string title, string hint, string action,
        Action onAction, float left, float y, float width, float scale)
    {
        var pad = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var block = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - pad * 2f);
        var height = titleSize.Y + block.Y + pad * 2f + 6f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);

        var pillY = max.Y + Metrics.Space.Md * scale;
        var pillRect = new Rect(new Vector2(left + width * 0.2f, pillY),
            new Vector2(left + width * 0.8f, pillY + 44f * scale));
        if (AppSkin.PillButton(pillRect, action, true, true, ui.Theme))
        {
            onAction();
        }
    }
}
