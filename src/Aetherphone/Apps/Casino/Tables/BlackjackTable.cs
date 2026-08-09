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

// The felt from the player's side of it. Every number on this screen is the server's: seats, stacks,
// bets, totals, whose turn it is and which of the four verbs that turn allows. Nothing here plays
// blackjack, it only paints a hand that has already been dealt somewhere else, which is what lets a
// phone that missed three frames catch up by being told rather than by working it out.
//
// The one law the whole screen obeys: a win is gold, counts up and throws confetti at the seat that
// won it, a loss is quiet, and the house never celebrates.
internal sealed class BlackjackTable
{
    private const float PadX = 16f;
    private const float StatusRowHeight = 22f;
    private const float BannerHeight = 30f;
    private const float ActionBarHeight = 46f;
    private const float CardWidth = 30f;
    private const float CardOverlap = 0.42f;
    private const float DealerCardWidth = 34f;
    private const int ActionCount = 4;

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
    private readonly Action openCashier;
    private readonly Action leaveRoom;
    private readonly BlackjackProjection projection = new();
    private readonly BlackjackDealPlayback playback = new();
    private readonly DealerBubble dealer = new();
    private readonly BetComposer composer = new("##blackjackBet");
    private readonly ParticleSystem particles = new(160);
    private readonly SeatView[] seatViews = new SeatView[BlackjackRules.SeatCount];
    private readonly Vector2[] seatCenters = new Vector2[BlackjackRules.SeatCount];

    private RollingValue winRoll;
    private string inlineReason = string.Empty;
    private string celebratedHandId = string.Empty;
    private string spokenHandId = string.Empty;
    private long settledDelta;
    private int spokenPhase = -1;
    private int spokenSeat = int.MinValue;
    private bool entered;

    public BlackjackTable(CasinoStore chips, CasinoRoomsStore rooms, Action openCashier, Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.openCashier = openCashier;
        this.leaveRoom = leaveRoom;
    }

    public void Enter()
    {
        entered = true;
        inlineReason = string.Empty;
        projection.Reset();
        playback.Reset();
        composer.Reset(BlackjackRules.MinBet);
        rooms.Enter(CasinoRoomIds.BlackjackTable);
    }

    public void Reset()
    {
        if (entered)
        {
            rooms.Leave();
        }

        entered = false;
        projection.Reset();
        playback.Reset();
        dealer.Clear();
        particles.Clear();
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
        ConsumeStakeResults();

        var room = rooms.Room;
        var closedReason = room.ClosedReason;
        var pad = PadX * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;
        var drawList = ImGui.GetWindowDrawList();

        if (closedReason.Length > 0)
        {
            DrawNotice(drawList, ui, Loc.T(L.Casino.BlackjackClosedTitle),
                Loc.T(CasinoReasons.TryMessage(closedReason, out var known) ? known : L.Casino.BlackjackClosedHint),
                Loc.T(L.Casino.WheelBackToFloor), leaveRoom, left, body.Min.Y + Metrics.Space.Lg * scale, width,
                scale);
            return;
        }

        var held = room.State;
        var snapshot = held?.Snapshot;
        var state = chips.State;
        projection.Apply(held);
        projection.ApplyPersonal(room.Private);
        var board = projection.Board;
        if (state is null || snapshot is null || board is null)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        playback.Update(board, snapshot.Phase, delta);
        particles.Update(delta);
        dealer.Update(delta);

        var localNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var phaseRemaining = room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, localNow);
        var turnRemaining = room.RemainingMilliseconds(board.DeadlineUnixMs, localNow);

        var y = body.Min.Y + Metrics.Space.Xs * scale;
        y = DrawStatusRow(drawList, ui, snapshot, room.Attached, left, y, width, scale);

        var footerHeight = FooterHeightFor(snapshot.Phase, scale);
        var feltMin = new Vector2(left, y + Metrics.Space.Xs * scale);
        var feltMax = new Vector2(left + width, body.Max.Y - footerHeight - Metrics.Space.Md * scale);
        if (feltMax.Y <= feltMin.Y)
        {
            return;
        }

        var felt = new Rect(feltMin, feltMax);
        FeltPanel.Draw(drawList, felt, ui.Accent, scale);
        DrawDealer(drawList, ui, board, felt, scale);
        var tapped = DrawSeats(drawList, ui, board, felt, turnRemaining, scale);
        if (BlackjackRules.IsSeat(tapped) && seatViews[tapped].Phase == SeatPhase.Empty)
        {
            openCashier();
        }

        SpeakForPhase(snapshot.Phase, board);
        dealer.Draw(drawList, new Vector2(felt.Center.X, felt.Min.Y + felt.Height * 0.30f), ui, scale);
        CelebrateSettledHand(board, scale);

        var footerTop = felt.Max.Y + Metrics.Space.Md * scale;
        DrawFooter(drawList, ui, state, board, snapshot, phaseRemaining, delta, left, footerTop, width, scale);
        particles.Draw(drawList, scale);
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

    // The stage never resizes under a hand: the footer reserves the tallest thing the current phase
    // can put there, so a bet window opening or an action bar arriving cannot shove the felt.
    private static float FooterHeightFor(int phase, float scale)
    {
        return phase == CasinoRoomPhases.Open
            ? BannerHeight * scale + BetComposer.HeightFor(scale)
            : BannerHeight * scale + ActionBarHeight * scale;
    }

    private float DrawStatusRow(ImDrawListPtr drawList, AppSkin ui, CasinoRoomSnapshotDto snapshot, bool attached,
        float left, float y, float width, float scale)
    {
        var height = StatusRowHeight * scale;
        var seated = Loc.T(L.Casino.BlackjackAtTheTable, GameNumber.Label(snapshot.Occupancy));
        Typography.Draw(drawList, new Vector2(left, y + 4f * scale), seated, ui.MutedInk, TextStyles.Caption1);
        if (attached)
        {
            // The house rules are printed on the felt of a real table, so they live here rather than
            // behind a sheet: the two numbers that decide every hand should never need looking up.
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
        var tapped = SeatRing.Draw(drawList, felt, seatViews, board.MySeat, scale, style, turnRemaining,
            board.WindowSeconds);
        SeatRing.Layout(felt, BlackjackRules.SeatCount, board.MySeat, seatCenters);
        DrawHands(drawList, ui, board, felt, scale);
        return tapped;
    }

    private void BuildSeatViews(CasinoBlackjackRoomStateDto board)
    {
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            var seat = projection.SeatAt(seatIndex);
            if (seat is null)
            {
                seatViews[seatIndex] = new SeatView(seatIndex, string.Empty, 0, 0, SeatPhase.Empty, false, true);
                continue;
            }

            var acting = board.ActiveSeat == seatIndex;
            var phase = acting ? SeatPhase.Acting : BlackjackRules.PhaseOf(seat.State);
            seatViews[seatIndex] = new SeatView(seatIndex, seat.DisplayName, seat.Stack, seat.Bet, phase,
                seat.Mine, seat.Connected);
        }
    }

    // Cards are laid in from the seat toward the felt, one fan per split hand, and the ordinal a card
    // carries in the stagger is its place in the whole deal rather than its place in its own hand:
    // that is what keeps the pass order reading as a pass rather than five seats dealing at once.
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
            var seat = seats[index];
            if (!BlackjackRules.IsSeat(seat.SeatIndex))
            {
                continue;
            }

            var hands = seat.Hands;
            if (hands is null)
            {
                continue;
            }

            var anchor = SeatRing.Pushed(seatCenters[seat.SeatIndex], ellipseCenter, puckRadius,
                SeatRing.HandPushFactor);
            for (var handIndex = 0; handIndex < hands.Length; handIndex++)
            {
                var hand = hands[handIndex];
                var handAnchor = new Vector2(anchor.X + (handIndex - (hands.Length - 1) * 0.5f) * cardWidth * 1.15f,
                    anchor.Y);
                ordinal = DrawHand(drawList, ui, board, seat, hand, handAnchor, felt, cardWidth, ordinal, scale);
            }
        }
    }

    private int DrawHand(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board,
        in CasinoBlackjackSeatDto seat, CasinoBlackjackHandDto hand, Vector2 anchor, in Rect felt, float cardWidth,
        int ordinal, float scale)
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
            var card = projection.CardAt(seat.SeatIndex, hand.SplitIndex, index, cards![index]);
            if (BlackjackDealChoreography.FaceUp(travel) && PlayingCards.IsCard(card))
            {
                PlayingCards.DrawFace(drawList, rect, card, PlayingCards.RoundingFor(cardWidth), scale, true);
            }
            else
            {
                PlayingCards.DrawBack(drawList, rect, PlayingCards.RoundingFor(cardWidth), scale, true);
            }
        }

        if (hand.Total > 0)
        {
            var active = board.ActiveSeat == seat.SeatIndex && board.ActiveSplit == hand.SplitIndex;
            var ink = hand.Outcome == BlackjackOutcomes.Bust ? ui.MutedInk : active ? ui.Accent : ui.BodyInk;
            Typography.DrawCentered(drawList,
                new Vector2(anchor.X, anchor.Y + PlayingCards.HeightFor(cardWidth) * 0.5f + 9f * scale),
                GameNumber.Label(hand.Total), ink, TextStyles.Caption2);
        }

        return ordinal + count;
    }

    // The dealer speaks when something actually changed, never on a timer: a bubble that reappears
    // every few seconds because its own hold expired is a tic, not a table.
    private void SpeakForPhase(int phase, CasinoBlackjackRoomStateDto board)
    {
        if (phase == spokenPhase && board.ActiveSeat == spokenSeat
            && string.Equals(spokenHandId, board.HandId, StringComparison.Ordinal))
        {
            return;
        }

        spokenPhase = phase;
        spokenSeat = board.ActiveSeat;
        spokenHandId = board.HandId;
        if (phase == CasinoRoomPhases.Open)
        {
            dealer.Show(Loc.T(L.Casino.BlackjackWaitingForBets));
            return;
        }

        if (phase == CasinoRoomPhases.Result && board.DealerTotal > 0)
        {
            dealer.Show(Loc.T(L.Casino.BlackjackDealerHas, GameNumber.Label(board.DealerTotal)));
            return;
        }

        if (board.ActiveSeat >= 0 && board.ActiveSeat == board.MySeat)
        {
            dealer.Show(Loc.T(L.Casino.BlackjackYourTurn));
        }
    }

    // The count up and the confetti fire once per hand and only for money that actually came back to
    // my seat, read from the settled deltas the server wrote. A hand that lost says nothing here at
    // all: silence is what a loss sounds like.
    private void CelebrateSettledHand(CasinoBlackjackRoomStateDto board, float scale)
    {
        if (string.Equals(celebratedHandId, board.HandId, StringComparison.Ordinal))
        {
            return;
        }

        // A new hand owes nobody the last one's number, and a hand I sat out never settles onto my
        // seat at all, so the figure is cleared the moment the table moves on rather than when the
        // next win happens to overwrite it.
        settledDelta = 0;
        if (board.HandId.Length == 0 || !BlackjackRules.IsSeat(board.MySeat))
        {
            return;
        }

        var seat = projection.SeatAt(board.MySeat);
        var hands = seat?.Hands;
        if (hands is null || hands.Length == 0 || !Settled(hands))
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

        var origin = seatCenters[board.MySeat];
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
        CasinoBlackjackRoomStateDto board, CasinoRoomSnapshotDto snapshot, long phaseRemaining, float delta,
        float left, float y, float width, float scale)
    {
        y = DrawBanner(drawList, ui, board, snapshot, phaseRemaining, delta, left, y, width, scale);
        var sitting = state.Sitting;
        var seated = sitting is not null
            && string.Equals(sitting.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal);
        if (!seated || board.MySeat < 0)
        {
            DrawTakeSeat(ui, left, y, width, scale);
            return;
        }

        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            DrawComposer(ui, state, sitting!, board, left, y, width, scale, delta);
            return;
        }

        if (board.ActiveSeat == board.MySeat)
        {
            DrawActionBar(ui, board, sitting!, left, y, width, scale);
        }
    }

    private float DrawBanner(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board,
        CasinoRoomSnapshotDto snapshot, long phaseRemaining, float delta, float left, float y, float width,
        float scale)
    {
        var center = new Vector2(left + width * 0.5f, y + BannerHeight * scale * 0.4f);
        if (snapshot.Phase == CasinoRoomPhases.Result && settledDelta > 0)
        {
            winRoll.Update((int)Math.Min(settledDelta, int.MaxValue), delta);
            var amount = "+" + ((long)winRoll.Display).ToString("N0", Loc.Culture);
            Typography.DrawCentered(drawList, center, Loc.T(L.Casino.BlackjackYouWon, amount), Gold,
                TextStyles.Title3.Scale * winRoll.PopScale, TextStyles.Title3.Weight);
            return y + BannerHeight * scale;
        }

        // A refusal owns the banner while it stands, because the composer below it is about to be
        // pressed again and a reason printed anywhere else would be read after the retry.
        var message = inlineReason.Length > 0
            ? Loc.T(CasinoReasons.MessageFor(inlineReason))
            : BannerTextFor(board, snapshot, phaseRemaining);
        Typography.DrawCentered(drawList, center, Typography.FitText(message, width, TextStyles.Caption1),
            ui.MutedInk, TextStyles.Caption1);
        return y + BannerHeight * scale;
    }

    private static string BannerTextFor(CasinoBlackjackRoomStateDto board, CasinoRoomSnapshotDto snapshot,
        long phaseRemaining)
    {
        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            var seconds = (int)((phaseRemaining + 999) / 1000);
            return Loc.T(L.Casino.BlackjackBetsCloseIn, TimeText.Duration(seconds));
        }

        if (snapshot.Phase == CasinoRoomPhases.Result)
        {
            return Loc.T(L.Casino.BlackjackHandOver);
        }

        if (board.ActiveSeat < 0)
        {
            return Loc.T(L.Casino.BlackjackDealing);
        }

        return board.ActiveSeat == board.MySeat
            ? Loc.T(L.Casino.BlackjackYourTurn)
            : Loc.T(L.Casino.BlackjackDealerPlays);
    }

    private void DrawComposer(AppSkin ui, CasinoStateDto state, CasinoSittingDto sitting,
        CasinoBlackjackRoomStateDto board, float left, float y, float width, float scale, float delta)
    {
        var minimum = board.MinBet > 0 ? board.MinBet : BlackjackRules.MinBet;
        var maximum = board.MaxBet > 0 ? board.MaxBet : BlackjackRules.MaxBet;
        composer.Prefill(minimum);
        var blocked = state.StakesPaused || state.Draining || rooms.StakeInFlight;
        var bounds = new Rect(new Vector2(left, y),
            new Vector2(left + width, y + BetComposer.HeightFor(scale)));
        var label = Loc.T(L.Casino.BlackjackBetConfirm, composer.Amount.ToString("N0", Loc.Culture));
        if (composer.Draw(ui, bounds, minimum, maximum, sitting.Stack, BlackjackRules.BetStep, !blocked, label,
                delta))
        {
            inlineReason = string.Empty;
            rooms.PlaceBlackjackBet(composer.Amount);
        }
    }

    private void DrawActionBar(AppSkin ui, CasinoBlackjackRoomStateDto board, CasinoSittingDto sitting, float left,
        float y, float width, float scale)
    {
        var gap = Metrics.Space.Xs * scale;
        var buttonWidth = (width - gap * (ActionCount - 1)) / ActionCount;
        var height = ActionBarHeight * scale;
        var cost = ActiveBetOf(board);
        var affordable = sitting.Stack >= cost;
        for (var index = 0; index < ActionCount; index++)
        {
            var bit = ActionBits[index];
            var min = new Vector2(left + index * (buttonWidth + gap), y);
            var rect = new Rect(min, new Vector2(min.X + buttonWidth, min.Y + height));
            var legal = BlackjackRules.Allows(board.ActionsMask, bit) && !rooms.StakeInFlight;
            if (bit == BlackjackRules.ActionDouble || bit == BlackjackRules.ActionSplit)
            {
                legal = legal && affordable;
            }

            if (AppSkin.PillButton(rect, LabelFor(bit, cost), bit == BlackjackRules.ActionStand, legal, ui.Theme)
                && legal)
            {
                inlineReason = string.Empty;
                rooms.SendBlackjackAction(bit);
            }
        }
    }

    private long ActiveBetOf(CasinoBlackjackRoomStateDto board)
    {
        var seat = projection.SeatAt(board.MySeat);
        var hands = seat?.Hands;
        if (hands is null)
        {
            return 0;
        }

        for (var index = 0; index < hands.Length; index++)
        {
            if (hands[index].SplitIndex == board.ActiveSplit)
            {
                return hands[index].Bet;
            }
        }

        return 0;
    }

    private static string LabelFor(int action, long cost)
    {
        return action switch
        {
            BlackjackRules.ActionHit => Loc.T(L.Casino.BlackjackActionHit),
            BlackjackRules.ActionStand => Loc.T(L.Casino.BlackjackActionStand),
            BlackjackRules.ActionDouble => Loc.T(L.Casino.BlackjackActionDouble, cost.ToString("N0", Loc.Culture)),
            _ => Loc.T(L.Casino.BlackjackActionSplit, cost.ToString("N0", Loc.Culture)),
        };
    }

    private void DrawTakeSeat(AppSkin ui, float left, float y, float width, float scale)
    {
        var rect = new Rect(new Vector2(left + width * 0.2f, y),
            new Vector2(left + width * 0.8f, y + ActionBarHeight * scale));
        if (AppSkin.PillButton(rect, Loc.T(L.Casino.BlackjackTakeSeat), true, true, ui.Theme))
        {
            openCashier();
        }
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
