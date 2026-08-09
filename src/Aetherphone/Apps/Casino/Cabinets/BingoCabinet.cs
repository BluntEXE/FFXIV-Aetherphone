using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class BingoCabinet
{
    private const float StatusRowHeight = 22f;
    private const float CallerHeight = 148f;
    private const float BallRailHeight = 32f;
    private const float ProgressHeight = 62f;
    private const float LadderRowHeight = 30f;
    private const float SegmentHeight = 40f;
    private const float PillHeight = 46f;
    private const float CardGap = 10f;
    private const float CardLabelHeight = 22f;
    private const int RailCalls = 8;

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private static readonly LocString[] StageNames =
    {
        L.Casino.BingoStageLine,
        L.Casino.BingoStageTwoLines,
        L.Casino.BingoStageFullHouse,
    };

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly Action openCashier;
    private readonly Action leaveRoom;
    private readonly BingoRoundPlayback playback = new();
    private readonly ParticleSystem particles = new(160);
    private readonly long[] ladder = new long[BingoRules.StageCount];

    private RollingValue winRoll;
    private string inlineReason = string.Empty;
    private string celebratedRoundId = string.Empty;
    private long settledPayout;
    private int requestedCards = 1;
    private bool entered;
    private Vector2 celebrationAnchor;

    public BingoCabinet(CasinoStore chips, CasinoRoomsStore rooms, Action openCashier, Action leaveRoom)
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
        requestedCards = 1;
        rooms.Enter(CasinoRoomIds.BingoHall);
    }

    public void Reset()
    {
        if (entered)
        {
            rooms.Leave();
        }

        entered = false;
        playback.Reset();
        particles.Clear();
        inlineReason = string.Empty;
        celebratedRoundId = string.Empty;
        settledPayout = 0;
        requestedCards = 1;
        winRoll.Snap(0);
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        ConsumeStakeResults();

        var room = rooms.Room;
        var held = room.State;
        var snapshot = held?.Snapshot;
        var board = held?.Bingo;
        var mine = snapshot is null ? null : rooms.BingoCardsFor(snapshot.RoomId, snapshot.RoundIndex);
        playback.Update(snapshot, board, mine, delta);
        particles.Update(delta);

        var closedReason = room.ClosedReason;
        if (closedReason.Length > 0)
        {
            DrawClosed(body, ui, closedReason, scale);
            return;
        }

        var state = chips.State;
        if (state is null || snapshot is null)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var remaining = room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        CelebrateSettledRoom(snapshot, board, mine, scale);

        using var surface = AppSurface.Begin(body);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
        var width = ScrollLayout.StableContentWidth();
        var calledOff = CalledOff(board, mine);
        DrawStatusRow(ui, snapshot, room.Attached, width, scale);
        DrawCaller(ui, snapshot, board, remaining, width, scale);
        if (snapshot.Phase != CasinoRoomPhases.Open)
        {
            DrawProgress(ui, mine, width, scale);
        }

        DrawLadder(ui, board, width, scale);
        if (snapshot.Phase == CasinoRoomPhases.Result)
        {
            DrawRoomSummary(ui, mine, calledOff, remaining, width, scale, delta);
        }

        var sitting = state.Sitting;
        var seated = sitting is not null
            && string.Equals(sitting.GameKind, CasinoWire.BingoKind, StringComparison.Ordinal);
        if (calledOff)
        {
            if (snapshot.Phase != CasinoRoomPhases.Result)
            {
                DrawNote(ui, Loc.T(L.Casino.BingoCalledOff), width, scale);
            }
        }
        else if (!seated)
        {
            DrawSeatMissing(ui, width, scale);
        }
        else if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            DrawComposer(ui, state, sitting!, mine, width, scale);
        }

        DrawCards(ui, mine, calledOff, width, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        particles.Draw(ImGui.GetWindowDrawList(), scale);
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

    internal static int HeldCards(CasinoBingoCardsDto? mine)
    {
        var held = mine?.Cards?.Length ?? 0;
        return held > BingoRules.MaxCards ? BingoRules.MaxCards : held;
    }

    internal static bool Settled(CasinoBingoCardsDto? mine)
    {
        return mine is not null && mine.RoundState == CasinoRoundStates.Settled;
    }

    internal static bool CalledOff(CasinoBingoRoomStateDto? board, CasinoBingoCardsDto? mine)
    {
        return (board is not null && board.Cancelled)
            || (mine is not null && mine.RoundState == CasinoRoundStates.Voided);
    }

    internal static long PrizeAt(CasinoBingoRoomStateDto? board, int stage)
    {
        if (!BingoRules.IsStage(stage))
        {
            return 0;
        }

        var published = board?.Prizes;
        if (published is not null && published.Length >= BingoRules.StageCount)
        {
            return published[stage];
        }

        return BingoRules.PrizeFor(stage, CardsInPlay(board));
    }

    internal static void LadderFor(CasinoBingoRoomStateDto? board, Span<long> ladder)
    {
        for (var stage = 0; stage < BingoRules.StageCount && stage < ladder.Length; stage++)
        {
            ladder[stage] = PrizeAt(board, stage);
        }
    }

    internal static int PrizeCardCapOf(CasinoBingoRoomStateDto? board)
    {
        var cap = board?.PrizeCardCap ?? 0;
        return cap > 0 ? cap : BingoRules.PrizeCardCap;
    }

    private void CelebrateSettledRoom(CasinoRoomSnapshotDto snapshot, CasinoBingoRoomStateDto? board,
        CasinoBingoCardsDto? mine, float scale)
    {
        var roundKey = BingoRoundPlayback.RoundKeyOf(snapshot);
        settledPayout = Settled(mine) ? mine!.Payout : 0;
        if (snapshot.Phase != CasinoRoomPhases.Result || !Settled(mine)
            || string.Equals(celebratedRoundId, roundKey, StringComparison.Ordinal))
        {
            return;
        }

        celebratedRoundId = roundKey;
        winRoll.Snap(0);
        if (settledPayout <= 0)
        {
            return;
        }

        if (settledPayout >= PrizeAt(board, BingoRules.StageFullHouse))
        {
            particles.Confetti(celebrationAnchor, 90, ConfettiPalette, 330f * scale, 5f, 1.6f);
            particles.Sparkle(celebrationAnchor, 24, Gold, 190f * scale, 4f, 1.0f);
            return;
        }

        particles.Confetti(celebrationAnchor, 40, ConfettiPalette, 250f * scale, 4f, 1.2f);
    }

    internal static int CardsInPlay(CasinoBingoRoomStateDto? board)
    {
        return board?.Cards ?? 0;
    }

    private void DrawStatusRow(AppSkin ui, CasinoRoomSnapshotDto snapshot, bool attached, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = StatusRowHeight * scale;
        var hall = Loc.T(L.Casino.BingoInTheHall, GameNumber.Label(snapshot.Occupancy));
        Typography.Draw(drawList, new Vector2(origin.X, origin.Y + 4f * scale), hall, ui.MutedInk,
            TextStyles.Caption1);

        if (!attached)
        {
            var label = Loc.T(L.Casino.WheelReconnecting);
            var labelSize = Typography.Measure(label, TextStyles.Caption2);
            var chipMax = new Vector2(origin.X + width, origin.Y + height - 2f * scale);
            var chipMin = new Vector2(chipMax.X - labelSize.X - 18f * scale, origin.Y);
            Squircle.Fill(drawList, chipMin, chipMax, (chipMax.Y - chipMin.Y) * 0.5f,
                ImGui.GetColorU32(ui.FieldSurface));
            var dotCenter = new Vector2(chipMin.X + 8f * scale, (chipMin.Y + chipMax.Y) * 0.5f);
            drawList.AddCircleFilled(dotCenter, 2.6f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f + 0.45f * Pulse.Wave(Pulse.Breath))), 12);
            Typography.Draw(drawList, new Vector2(dotCenter.X + 6f * scale, chipMin.Y + 3f * scale), label,
                ui.MutedInk, TextStyles.Caption2);
        }

        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawCaller(AppSkin ui, CasinoRoomSnapshotDto snapshot, CasinoBingoRoomStateDto? board,
        long remainingMs, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = CallerHeight * scale;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        var callerRounding = Metrics.Radius.Card * scale;
        Elevation.Card(drawList, min, max, callerRounding, scale);
        ui.Card(drawList, min, max, callerRounding);
        Material.TopGlow(drawList, min, max, callerRounding, ui.Palette.Accent, 0.55f, 0.30f);
        celebrationAnchor = new Vector2((min.X + max.X) * 0.5f, min.Y + 48f * scale);

        var centerX = (min.X + max.X) * 0.5f;
        var seconds = (int)((remainingMs + 999) / 1000);
        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 34f * scale),
                Loc.T(L.Casino.BingoCardsClose, TimeText.Duration(seconds)), ui.TitleInk, TextStyles.Title3);
            Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 64f * scale),
                Loc.T(L.Casino.BingoFirstBall, TimeText.Duration(seconds)), ui.MutedInk, TextStyles.Caption1);
        }
        else if (playback.LatestBall > 0)
        {
            var entry = MathF.Min(1f, playback.SinceBall / BingoRoundPlayback.BallEntrySeconds);
            var radius = 38f * scale * (0.62f + 0.38f * Easing.EaseOutBack(entry));
            var ballCenter = new Vector2(centerX, min.Y + 46f * scale);
            drawList.AddCircleFilled(ballCenter, radius * 1.42f,
                ImGui.GetColorU32(new Vector4(ui.Palette.Accent.X, ui.Palette.Accent.Y, ui.Palette.Accent.Z,
                    0.16f * (1f - entry * 0.4f))), 40);
            BingoCardArt.DrawBallChip(drawList, ballCenter, radius, playback.LatestBall, 1f,
                ui.Palette.HeaderInk);
            Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 96f * scale),
                Loc.T(L.Casino.BingoCalledCount, GameNumber.Label(playback.BallCount),
                    GameNumber.Label(BingoRules.Balls)), ui.MutedInk, TextStyles.Subheadline);
        }
        else
        {
            Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 48f * scale),
                Loc.T(L.Casino.BingoWaitingRoom), ui.MutedInk, TextStyles.Subheadline);
        }

        DrawBallRail(drawList, ui, board, min, max, scale);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private readonly record struct CardProgress(int CardIndex, int Gap, int GoalStage);

    private CardProgress BestProgress(CasinoBingoCardsDto? mine)
    {
        var holding = HeldCards(mine);
        var best = new CardProgress(-1, int.MaxValue, BingoRules.StageLine);
        for (var cardIndex = 0; cardIndex < holding; cardIndex++)
        {
            var mask = playback.AutoMaskOf(cardIndex);
            var gap = BingoRules.NextGoalGap(mask, out var goalStage);
            if (gap < best.Gap)
            {
                best = new CardProgress(cardIndex, gap, goalStage);
            }
        }

        return best;
    }

    private void DrawProgress(AppSkin ui, CasinoBingoCardsDto? mine, float width, float scale)
    {
        if (HeldCards(mine) == 0)
        {
            return;
        }

        var best = BestProgress(mine);
        if (best.CardIndex < 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = ProgressHeight * scale;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        var hot = best.Gap <= 1;
        var rounding = Metrics.Radius.Card * scale;
        Elevation.Card(drawList, min, max, rounding, scale);
        ui.Card(drawList, min, max, rounding);
        if (hot)
        {
            Material.TopGlow(drawList, min, max, rounding, Gold, 0.62f, 0.34f);
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Gold), 1.4f * scale);
        }

        var headline = HeadlineFor(best);
        var ink = hot ? Gold : ui.TitleInk;
        var centerY = (min.Y + max.Y) * 0.5f;
        var pad = Metrics.Space.Lg * scale;
        Typography.Draw(drawList, new Vector2(min.X + pad, centerY - 15f * scale), headline, ink,
            TextStyles.Title3);

        var onCard = Loc.T(L.Casino.BingoProgressOn,
            Loc.T(L.Casino.BingoCardLabel, GameNumber.Label(best.CardIndex + 1)));
        Typography.Draw(drawList, new Vector2(min.X + pad, centerY + 6f * scale), onCard, ui.MutedInk,
            TextStyles.Subheadline);

        if (hot)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(playback.SinceBall * 4.4f);
            var radius = 13f * scale;
            var center = new Vector2(max.X - pad - radius, centerY);
            drawList.AddCircleFilled(center, radius * (0.94f + 0.10f * pulse),
                ImGui.GetColorU32(new Vector4(Gold.X, Gold.Y, Gold.Z, 0.20f + 0.16f * pulse)), 28);
            Typography.DrawCentered(drawList, center, GameNumber.Label(best.Gap), Gold,
                TextStyles.SubheadlineEmphasized);
        }
        else
        {
            var gapLabel = GameNumber.Label(best.Gap);
            var gapSize = Typography.Measure(gapLabel, TextStyles.Title2);
            Typography.Draw(drawList, new Vector2(max.X - pad - gapSize.X, centerY - gapSize.Y * 0.5f),
                gapLabel, ui.TitleInk, TextStyles.Title2);
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private static string HeadlineFor(CardProgress best)
    {
        if (best.Gap <= 0)
        {
            return Loc.T(L.Casino.BingoProgressAllDone);
        }

        if (best.Gap == 1)
        {
            return best.GoalStage switch
            {
                BingoRules.StageTwoLines => Loc.T(L.Casino.BingoAwayReadyTwoLines),
                BingoRules.StageFullHouse => Loc.T(L.Casino.BingoAwayReadyFullHouse),
                _ => Loc.T(L.Casino.BingoAwayReadyLine),
            };
        }

        var count = GameNumber.Label(best.Gap);
        return best.GoalStage switch
        {
            BingoRules.StageTwoLines => Loc.T(L.Casino.BingoAwayTwoLines, count),
            BingoRules.StageFullHouse => Loc.T(L.Casino.BingoAwayFullHouse, count),
            _ => Loc.T(L.Casino.BingoAwayLine, count),
        };
    }

    private void DrawBallRail(ImDrawListPtr drawList, AppSkin ui, CasinoBingoRoomStateDto? board, Vector2 min,
        Vector2 max, float scale)
    {
        var balls = BingoRoundPlayback.CalledBalls(board);
        if (balls.Length == 0)
        {
            return;
        }

        var radius = 11f * scale;
        var gap = 6f * scale;
        var shown = balls.Length < RailCalls ? balls.Length : RailCalls;
        var step = radius * 2f + gap;
        var railWidth = shown * step - gap;
        var startX = (min.X + max.X) * 0.5f - railWidth * 0.5f + radius;
        var railY = max.Y - BallRailHeight * scale * 0.5f;
        var label = Loc.T(L.Casino.BingoRecentCalls);
        Typography.Draw(drawList, new Vector2(min.X + 12f * scale, railY - radius - 2f * scale), label,
            ui.MutedInk, TextStyles.Caption2);
        for (var index = 0; index < shown; index++)
        {
            var ball = balls[balls.Length - shown + index];
            var alpha = 0.40f + 0.60f * ((index + 1f) / shown);
            BingoCardArt.DrawBallChip(drawList, new Vector2(startX + index * step, railY), radius, ball, alpha,
                ui.Palette.HeaderInk);
        }
    }

    private void DrawLadder(AppSkin ui, CasinoBingoRoomStateDto? board, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var cardsInPlay = CardsInPlay(board);
        LadderFor(board, ladder);
        var prizeCardCap = PrizeCardCapOf(board);
        var capNote = cardsInPlay >= prizeCardCap
            ? Loc.T(L.Casino.BingoLadderCapped, GameNumber.Label(prizeCardCap))
            : Loc.T(L.Casino.BingoLadderGrows, GameNumber.Label(prizeCardCap));
        var inset = 14f * scale;
        var innerWidth = width - inset * 2f;
        var heading = Loc.T(L.Casino.BingoLadderHeading);
        var headingSize = Typography.Measure(heading, TextStyles.FootnoteEmphasized);
        var noteBlock = Typography.MeasureWrappedBlock(capNote, TextStyles.Caption2, innerWidth);
        var height = inset * 2f + headingSize.Y + 6f * scale + BingoRules.StageCount * LadderRowHeight * scale
            + noteBlock.Y;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);

        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset), heading, ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        var inPlay = Loc.T(L.Casino.BingoCardsInPlay, GameNumber.Label(cardsInPlay));
        var inPlaySize = Typography.Measure(inPlay, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(max.X - inset - inPlaySize.X, min.Y + inset), inPlay, ui.MutedInk,
            TextStyles.Caption1);

        var rowY = min.Y + inset + headingSize.Y + 6f * scale;
        for (var stage = 0; stage < BingoRules.StageCount; stage++)
        {
            DrawLadderRow(drawList, ui, board, stage, min.X + inset, rowY, innerWidth, scale);
            rowY += LadderRowHeight * scale;
        }

        Typography.DrawWrappedLeft(new Vector2(min.X + inset, rowY + 2f * scale), capNote, ui.MutedInk,
            TextStyles.Caption2, innerWidth);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawLadderRow(ImDrawListPtr drawList, AppSkin ui, CasinoBingoRoomStateDto? board, int stage,
        float left, float y, float width, float scale)
    {
        var awarded = StageAwarded(board, stage);
        var name = Loc.T(StageNames[stage]);
        var ink = awarded is null ? ui.BodyInk : Gold;
        Typography.Draw(drawList, new Vector2(left, y + 5f * scale), name, ink, TextStyles.Subheadline);

        var prize = awarded?.Prize ?? ladder[stage];
        var prizeText = prize.ToString("N0", Loc.Culture);
        var prizeSize = Typography.Measure(prizeText, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left + width - prizeSize.X, y + 5f * scale), prizeText, ink,
            TextStyles.SubheadlineEmphasized);

        if (awarded is null)
        {
            return;
        }

        var wonOn = Loc.T(L.Casino.BingoStageWonOn, name, GameNumber.Label(awarded.Ball));
        var fitted = Typography.FitText(wonOn, width - prizeSize.X - 12f * scale, TextStyles.Caption2);
        var wonSize = Typography.Measure(fitted, TextStyles.Caption2);
        Typography.Draw(drawList,
            new Vector2(left + width - prizeSize.X - wonSize.X - 10f * scale, y + 8f * scale), fitted,
            ui.MutedInk, TextStyles.Caption2);
    }

    internal static CasinoBingoStageDto? StageAwarded(CasinoBingoRoomStateDto? board, int stage)
    {
        var stages = board?.Stages;
        if (stages is null)
        {
            return null;
        }

        for (var index = 0; index < stages.Length; index++)
        {
            if (stages[index].Stage == stage)
            {
                return stages[index];
            }
        }

        return null;
    }

    private void DrawRoomSummary(AppSkin ui, CasinoBingoCardsDto? mine, bool calledOff, long remainingMs,
        float width, float scale, float delta)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var title = Loc.T(L.Casino.BingoRoomWrapped);
        var seconds = (int)((remainingMs + 999) / 1000);
        var next = Loc.T(L.Casino.BingoNextRoom, TimeText.Duration(seconds));
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var height = inset * 2f + titleSize.Y + 40f * scale;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(Palette.WithAlpha(settledPayout > 0 ? Gold : ui.Accent, 0.35f)), 1f * scale);

        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var nextSize = Typography.Measure(next, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(max.X - inset - nextSize.X, min.Y + inset + 2f * scale), next,
            ui.MutedInk, TextStyles.Caption1);

        var outcomeY = min.Y + inset + titleSize.Y + 8f * scale;
        if (calledOff)
        {
            Typography.Draw(drawList, new Vector2(min.X + inset, outcomeY), Loc.T(L.Casino.BingoCalledOff),
                ui.MutedInk, TextStyles.Subheadline);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
            return;
        }

        if (!Settled(mine))
        {
            Typography.Draw(drawList, new Vector2(min.X + inset, outcomeY), Loc.T(L.Casino.BingoCardsPending),
                ui.MutedInk, TextStyles.Subheadline);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
            return;
        }

        if (settledPayout > 0)
        {
            winRoll.Update((int)Math.Min(settledPayout, int.MaxValue), delta);
            var amount = "+" + ((long)winRoll.Display).ToString("N0", Loc.Culture);
            Typography.Draw(drawList, new Vector2(min.X + inset, outcomeY), Loc.T(L.Casino.BingoYouWon, amount),
                Gold, TextStyles.Title3.Scale * winRoll.PopScale, TextStyles.Title3.Weight);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(min.X + inset, outcomeY), Loc.T(L.Casino.BingoNoWin),
                ui.MutedInk, TextStyles.Subheadline);
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawComposer(AppSkin ui, CasinoStateDto state, CasinoSittingDto sitting,
        CasinoBingoCardsDto? mine, float width, float scale)
    {
        if (inlineReason.Length > 0)
        {
            DrawNote(ui, Loc.T(CasinoReasons.MessageFor(inlineReason)), width, scale);
        }

        var holding = HeldCards(mine);
        if (holding > 0)
        {
            DrawNote(ui, Loc.T(L.Casino.BingoHoldingFull, CardCountLabel(holding)), width, scale);
            return;
        }

        if (requestedCards > BingoRules.MaxCards)
        {
            requestedCards = BingoRules.MaxCards;
        }

        if (requestedCards < 1)
        {
            requestedCards = 1;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, origin, Loc.T(L.Casino.BingoBuyHeading), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        var price = Loc.T(L.Casino.BingoCardPrice, BingoRules.CardPrice.ToString("N0", Loc.Culture),
            GameNumber.Label(BingoRules.MaxCards));
        var priceSize = Typography.Measure(price, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(origin.X + width - priceSize.X, origin.Y), price, ui.MutedInk,
            TextStyles.Caption1);

        var segmentY = origin.Y + 20f * scale;
        DrawCountSegments(drawList, ui, origin.X, segmentY, width, BingoRules.MaxCards, scale);

        var stake = BingoRules.StakeFor(requestedCards);
        var thin = sitting.Stack < stake;
        var blocked = state.StakesPaused || state.Draining;
        var canBuy = !blocked && !thin && !rooms.StakeInFlight;
        var label = Loc.T(L.Casino.BingoBuyFor, CardCountLabel(requestedCards),
            stake.ToString("N0", Loc.Culture));
        var pillY = segmentY + SegmentHeight * scale + Metrics.Space.Sm * scale;
        var pillRect = new Rect(new Vector2(origin.X, pillY),
            new Vector2(origin.X + width, pillY + PillHeight * scale));
        if (DrawBuyPill(drawList, ui, pillRect, label, canBuy, scale))
        {
            inlineReason = string.Empty;
            rooms.BuyBingoCards(requestedCards);
        }

        var noteY = pillRect.Max.Y + 6f * scale;
        var note = blocked
            ? Loc.T(state.StakesPaused ? L.Casino.PausedTitle : L.Casino.DrainingTitle)
            : thin ? Loc.T(L.Casino.SlotsLowStack) : Loc.T(L.Casino.BingoOneBuyPerRoom);
        var noteBlock = Typography.MeasureWrappedBlock(note, TextStyles.Caption2, width);
        Typography.DrawWrappedLeft(new Vector2(origin.X, noteY), note, ui.MutedInk, TextStyles.Caption2, width);
        var finalNote = Loc.T(L.Casino.BingoCardsFinal);
        var finalY = noteY + noteBlock.Y + 2f * scale;
        var finalBlock = Typography.MeasureWrappedBlock(finalNote, TextStyles.Caption2, width);
        Typography.DrawWrappedLeft(new Vector2(origin.X, finalY), finalNote, ui.MutedInk, TextStyles.Caption2,
            width);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, finalY + finalBlock.Y - origin.Y + Metrics.Space.Md * scale));
        if (thin)
        {
            DrawCashierPill(ui, width, scale);
        }
    }

    private void DrawCashierPill(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pillRect = new Rect(new Vector2(origin.X + width * 0.25f, origin.Y),
            new Vector2(origin.X + width * 0.75f, origin.Y + 38f * scale));
        if (ui.GhostButton(pillRect, Loc.T(L.Casino.Cashier)))
        {
            openCashier();
        }

        ImGui.Dummy(new Vector2(width, 38f * scale + Metrics.Space.Sm * scale));
    }

    private void DrawCountSegments(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width,
        int headroom, float scale)
    {
        var height = SegmentHeight * scale;
        var rounding = height * 0.5f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.FieldSurface));
        var slotWidth = width / BingoRules.MaxCards;
        for (var count = 1; count <= BingoRules.MaxCards; count++)
        {
            var slotMin = new Vector2(left + (count - 1) * slotWidth, y);
            var slotMax = new Vector2(slotMin.X + slotWidth, max.Y);
            var enabled = count <= headroom;
            var selected = count == requestedCards;
            var hovered = enabled && UiInteract.Hover(slotMin, slotMax);
            if (selected)
            {
                var padding = new Vector2(3f * scale, 3f * scale);
                Squircle.Fill(drawList, slotMin + padding, slotMax - padding, rounding,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.9f)));
            }
            else if (hovered)
            {
                Squircle.Fill(drawList, slotMin, slotMax, rounding, ImGui.GetColorU32(ui.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var ink = selected ? ui.Palette.HeaderInk : enabled ? ui.BodyInk : ui.MutedInk;
            Typography.DrawCentered(drawList, (slotMin + slotMax) * 0.5f, GameNumber.Label(count),
                Palette.WithAlpha(ink, enabled ? 1f : 0.4f), TextStyles.Subheadline);
            if (enabled && UiInteract.Click(slotMin, slotMax, hovered))
            {
                requestedCards = count;
                inlineReason = string.Empty;
            }
        }
    }

    private static string CardCountLabel(int cards)
    {
        return cards == 1 ? Loc.T(L.Casino.BingoOneCard) : Loc.T(L.Casino.BingoCardCount, GameNumber.Label(cards));
    }

    private void DrawCards(AppSkin ui, CasinoBingoCardsDto? mine, bool calledOff, float width, float scale)
    {
        var holding = HeldCards(mine);
        if (holding == 0)
        {
            if (!calledOff)
            {
                DrawNote(ui, Loc.T(L.Casino.BingoNoCardsHint), width, scale);
            }

            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gap = CardGap * scale;
        var columns = holding > 1 ? 2 : 1;
        var cardWidth = columns == 2 ? (width - gap) * 0.5f : width * 0.64f;
        var cardHeight = BingoCardArt.HeightFor(cardWidth);
        var labelHeight = CardLabelHeight * scale;
        var rowCount = (holding + columns - 1) / columns;
        var rowStep = cardHeight + labelHeight + gap;
        for (var cardIndex = 0; cardIndex < holding; cardIndex++)
        {
            var column = cardIndex % columns;
            var row = cardIndex / columns;
            var cardMin = new Vector2(origin.X + column * (cardWidth + gap),
                origin.Y + row * rowStep + labelHeight);
            var card = new Rect(cardMin, new Vector2(cardMin.X + cardWidth, cardMin.Y + cardHeight));
            DrawCard(drawList, ui, card, mine, cardIndex, labelHeight, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowCount * rowStep - gap + Metrics.Space.Sm * scale));

        var footnote = Loc.T(L.Casino.BingoMarksAuto);
        var footBlock = Typography.MeasureWrappedBlock(footnote, TextStyles.Caption2, width);
        Typography.DrawWrappedLeft(ImGui.GetCursorScreenPos(), footnote, ui.MutedInk, TextStyles.Caption2, width);
        ImGui.Dummy(new Vector2(width, footBlock.Y));
    }

    private void DrawCard(ImDrawListPtr drawList, AppSkin ui, Rect card, CasinoBingoCardsDto? mine, int cardIndex,
        float labelHeight, float scale)
    {
        var rounding = Metrics.Radius.Sm * scale;
        var padding = new Vector2(3f * scale, 3f * scale);
        var plateMin = card.Min - padding;
        var plateMax = card.Max + padding;
        Squircle.Fill(drawList, plateMin, plateMax, rounding, ImGui.GetColorU32(ui.FieldSurface));

        var autoMask = playback.AutoMaskOf(cardIndex);
        var stampedMask = playback.StampedMaskOf(cardIndex);
        var label = Loc.T(L.Casino.BingoCardLabel, GameNumber.Label(cardIndex + 1));
        Typography.Draw(drawList, new Vector2(card.Min.X, card.Min.Y - labelHeight), label, ui.MutedInk,
            TextStyles.Caption2);

        if (BingoRules.ClosestLineGap(autoMask) == 1 || BingoRules.CellsRemaining(autoMask) == 1)
        {
            var chip = Loc.T(L.Casino.BingoOneAway);
            var chipSize = Typography.Measure(chip, TextStyles.Caption2);
            Typography.Draw(drawList, new Vector2(card.Max.X - chipSize.X, card.Min.Y - labelHeight), chip, Gold,
                TextStyles.Caption2);
        }

        if (Settled(mine) && mine!.Payout > 0)
        {
            Squircle.Stroke(drawList, plateMin, plateMax, rounding, ImGui.GetColorU32(Gold), 1.6f * scale);
        }

        var numbers = BingoRoundPlayback.CardAt(mine, cardIndex);
        BingoCardArt.Draw(drawList, ui, card, numbers, autoMask, stampedMask, playback, cardIndex, scale);
        HandleCardTaps(card, cardIndex, autoMask, stampedMask);
    }

    private void HandleCardTaps(Rect card, int cardIndex, int autoMask, int stampedMask)
    {
        var pending = autoMask & ~stampedMask;
        if (pending == 0 || !UiInteract.Hover(card.Min, card.Max))
        {
            return;
        }

        var cellSize = card.Width / BingoCardArt.Columns;
        for (var cellIndex = 0; cellIndex < BingoRules.Cells; cellIndex++)
        {
            if ((pending & (1 << cellIndex)) == 0)
            {
                continue;
            }

            var column = BingoRules.ColumnOfCell(cellIndex);
            var row = BingoRules.RowOfCell(cellIndex);
            var min = new Vector2(card.Min.X + column * cellSize, card.Min.Y + row * cellSize);
            var max = new Vector2(min.X + cellSize, min.Y + cellSize);
            var hovered = UiInteract.Hover(min, max);
            if (hovered && UiInteract.Click(min, max, hovered))
            {
                playback.Stamp(cardIndex, cellIndex);
                return;
            }
        }
    }

    private void DrawNote(AppSkin ui, string message, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - inset * 2f);
        var height = block.Y + inset * 2f;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + inset), message, ui.TitleInk,
            TextStyles.Footnote, width - inset * 2f);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawSeatMissing(AppSkin ui, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var hint = Loc.T(L.Casino.CabinetNoChipsHint);
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var block = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + block.Y + inset * 2f + 6f * scale + 44f * scale + Metrics.Space.Sm * scale;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + inset + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);

        var pillRect = new Rect(new Vector2(min.X + width * 0.2f, max.Y - inset - 44f * scale),
            new Vector2(min.X + width * 0.8f, max.Y - inset));
        if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.Cashier), true, true, ui.Theme))
        {
            openCashier();
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawClosed(Rect body, AppSkin ui, string reason, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pad = Metrics.Space.Md * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;
        var inset = 14f * scale;
        var title = Loc.T(L.Casino.BingoClosedTitle);
        var hint = Loc.T(CasinoReasons.TryMessage(reason, out var known) ? known : L.Casino.BingoClosedHint);
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var block = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + block.Y + inset * 2f + 6f * scale;
        var min = new Vector2(left, body.Min.Y + Metrics.Space.Lg * scale);
        var max = new Vector2(left + width, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + inset + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);

        var pillY = max.Y + Metrics.Space.Md * scale;
        var pillRect = new Rect(new Vector2(left + width * 0.2f, pillY),
            new Vector2(left + width * 0.8f, pillY + 44f * scale));
        if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.WheelBackToFloor), true, true, ui.Theme))
        {
            leaveRoom();
        }
    }

    private static bool DrawBuyPill(ImDrawListPtr drawList, AppSkin ui, Rect rect, string label, bool enabled,
        float scale)
    {
        var rounding = rect.Height * 0.5f;
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, enabled ? 1f : 0.4f)));
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = ui.Palette.HeaderInk;
        var fitted = Typography.FitText(label, rect.Width - 16f * scale, TextStyles.Subheadline);
        Typography.DrawCentered(drawList, rect.Center, fitted, enabled ? ink : Palette.WithAlpha(ink, 0.6f),
            TextStyles.Subheadline);
        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
