using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.GameRooms;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

// The pool table. Nothing here simulates: a shot is an angle and a power sent to the server, and
// what comes back is a trace of straight runs the balls are walked along at wall-clock speed.
// Shooting is a pull-back drag from anywhere on the felt, so the whole table is the control and
// the cue ball never has to be tapped precisely; ball in hand is one tap where the cue should go.
internal sealed class OnlinePoolTable
{
    private static readonly Vector4 Felt = new(0.16f, 0.46f, 0.30f, 1f);
    private static readonly Vector4 FeltEdge = new(0.10f, 0.32f, 0.20f, 1f);
    private static readonly Vector4 Rail = new(0.36f, 0.22f, 0.12f, 1f);
    private static readonly Vector4 Pocket = new(0.05f, 0.05f, 0.06f, 1f);
    private static readonly Vector4 CueWhite = new(0.97f, 0.96f, 0.92f, 1f);
    private static readonly Vector4 EightBlack = new(0.10f, 0.10f, 0.12f, 1f);
    private static readonly Vector4 ResignTint = new(0.85f, 0.35f, 0.32f, 1f);
    private static readonly Vector4 AimLine = new(1f, 1f, 1f, 0.55f);

    private static readonly Vector4[] BallColors =
    {
        new(0.98f, 0.80f, 0.15f, 1f),
        new(0.16f, 0.36f, 0.86f, 1f),
        new(0.86f, 0.18f, 0.16f, 1f),
        new(0.42f, 0.22f, 0.62f, 1f),
        new(0.95f, 0.46f, 0.12f, 1f),
        new(0.14f, 0.55f, 0.32f, 1f),
        new(0.55f, 0.16f, 0.18f, 1f),
    };

    private const float MaxDragUnits = 0.5f;
    private const int CueBall = 0;
    private const int EightBall = 8;

    private readonly GameRoomsStore store;

    private int replayActionCount = -1;
    private long replayStartedTick;
    private float replayDurationMs;
    private bool dragging;
    private Vector2 dragStart;

    public OnlinePoolTable(GameRoomsStore store)
    {
        this.store = store;
    }

    public void Reset()
    {
        replayActionCount = -1;
        dragging = false;
    }

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot,
        PoolRoomStateDto board, string notice)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var accent = Core.Apps.AppAccents.For("games");
        GameScene.Ambient(drawList, body, Felt);

        var players = board.Players ?? Array.Empty<PoolPlayerDto>();
        var balls = board.Balls ?? Array.Empty<PoolBallDto>();
        var mySeat = SeatOf(players, store.AccountId);
        var live = board.WinnerSeat < 0 && board.EndKind.Length == 0;
        var myTurn = live && mySeat >= 0 && board.TurnSeat == mySeat;

        var rowHeight = 28f * scale;
        var tableWidth = body.Width - 16f * scale;
        var tableHeight = tableWidth * (GameRoomWire.PoolTableHeight / GameRoomWire.PoolTableWidth);
        var maxHeight = body.Height - (rowHeight * 2f + 110f * scale);
        if (tableHeight > maxHeight)
        {
            tableHeight = maxHeight;
            tableWidth = tableHeight * (GameRoomWire.PoolTableWidth / GameRoomWire.PoolTableHeight);
        }

        var origin = new Vector2(body.Center.X - tableWidth * 0.5f, body.Min.Y + rowHeight + 10f * scale);
        var unit = tableWidth / GameRoomWire.PoolTableWidth;
        var replayFraction = ReplayProgress(board);

        DrawSeatRow(drawList, theme, scale,
            new Rect(new Vector2(origin.X, body.Min.Y + 4f * scale), new Vector2(origin.X + tableWidth, body.Min.Y + 4f * scale + rowHeight)),
            board, players, mySeat >= 0 ? 1 - mySeat : 1, snapshot, accent);
        DrawTable(drawList, origin, unit, scale);
        DrawBalls(drawList, origin, unit, scale, board, balls, replayFraction);

        var replaying = replayFraction < 1f;
        if (myTurn && !replaying)
        {
            if (board.BallInHand)
            {
                HandlePlacement(drawList, origin, unit, scale, balls);
            }
            else
            {
                HandleShot(drawList, origin, unit, scale, balls);
            }
        }
        else
        {
            dragging = false;
        }

        var myRowTop = origin.Y + tableHeight + 8f * scale;
        DrawSeatRow(drawList, theme, scale,
            new Rect(new Vector2(origin.X, myRowTop), new Vector2(origin.X + tableWidth, myRowTop + rowHeight)),
            board, players, mySeat >= 0 ? mySeat : 0, snapshot, accent);
        DrawStatus(drawList, theme, scale, body, myRowTop + rowHeight + 10f * scale, board, players, mySeat,
            myTurn, replaying, notice);

        if (mySeat >= 0 && live && GameHud.Button(
                new Vector2(body.Center.X, body.Max.Y - 24f * scale),
                new Vector2(110f * scale, 30f * scale), Loc.T(L.Games.OnlineResign), ResignTint, theme)
            && !store.ActInFlight)
        {
            store.SendResign();
        }
    }

    // A shot's trace starts playing the moment its action count arrives and runs on the wall
    // clock, so a spectator and the shooter see the same motion however late the event landed.
    private float ReplayProgress(PoolRoomStateDto board)
    {
        var trace = board.LastShot ?? Array.Empty<PoolTraceDto>();
        if (trace.Length == 0)
        {
            replayActionCount = board.ActionCount;
            return 1f;
        }

        if (replayActionCount != board.ActionCount)
        {
            replayActionCount = board.ActionCount;
            replayStartedTick = Environment.TickCount64;
            replayDurationMs = 0f;
            for (var index = 0; index < trace.Length; index++)
            {
                var end = trace[index].AtMs + trace[index].DurationMs;
                if (end > replayDurationMs)
                {
                    replayDurationMs = end;
                }
            }
        }

        if (replayDurationMs <= 0f)
        {
            return 1f;
        }

        var elapsed = Environment.TickCount64 - replayStartedTick;
        return elapsed >= replayDurationMs ? 1f : elapsed / replayDurationMs;
    }

    private static void DrawTable(ImDrawListPtr drawList, Vector2 origin, float unit, float scale)
    {
        var railWidth = 10f * scale;
        var width = GameRoomWire.PoolTableWidth * unit;
        var height = GameRoomWire.PoolTableHeight * unit;
        var min = origin - new Vector2(railWidth, railWidth);
        var max = origin + new Vector2(width + railWidth, height + railWidth);
        Elevation.Card(drawList, min, max, 8f * scale, scale);
        Squircle.Fill(drawList, min, max, 8f * scale, ImGui.GetColorU32(Rail));
        drawList.AddRectFilledMultiColor(origin, origin + new Vector2(width, height),
            ImGui.GetColorU32(Felt), ImGui.GetColorU32(Felt), ImGui.GetColorU32(FeltEdge),
            ImGui.GetColorU32(FeltEdge));

        var pocketRadius = GameRoomWire.PoolPocketRadius * unit;
        for (var pocket = 0; pocket < 6; pocket++)
        {
            var px = pocket % 3 * (GameRoomWire.PoolTableWidth * 0.5f);
            var py = pocket < 3 ? 0f : GameRoomWire.PoolTableHeight;
            var center = origin + new Vector2(px * unit, py * unit);
            drawList.AddCircleFilled(center, pocketRadius, ImGui.GetColorU32(Pocket), 28);
            drawList.AddCircle(center, pocketRadius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.5f)), 28,
                1.5f * scale);
        }

        // The head string, where the cue starts and the eye lines up the break.
        var headX = origin.X + GameRoomWire.PoolTableWidth * 0.25f * unit;
        drawList.AddLine(new Vector2(headX, origin.Y), new Vector2(headX, origin.Y + height),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f * scale);
    }

    private static void DrawBalls(ImDrawListPtr drawList, Vector2 origin, float unit, float scale,
        PoolRoomStateDto board, PoolBallDto[] balls, float replayFraction)
    {
        var radius = GameRoomWire.PoolBallRadius * unit;
        var trace = board.LastShot ?? Array.Empty<PoolTraceDto>();
        var replayMs = replayFraction * ReplayLength(trace);
        for (var index = 0; index < balls.Length; index++)
        {
            var ball = balls[index];
            if (!TryPositionAt(ball, trace, replayFraction, replayMs, out var x, out var y))
            {
                continue;
            }

            DrawBall(drawList, origin + new Vector2(x * unit, y * unit), radius, ball.Number, scale);
        }
    }

    private static float ReplayLength(PoolTraceDto[] trace)
    {
        var length = 0f;
        for (var index = 0; index < trace.Length; index++)
        {
            var end = trace[index].AtMs + trace[index].DurationMs;
            if (end > length)
            {
                length = end;
            }
        }

        return length;
    }

    // Where a ball is at this instant of the replay: on its run if one is in flight, at the start
    // of its first run before the shot reaches it, at rest once its last run ended. A ball that
    // dropped shows until the instant it did.
    private static bool TryPositionAt(PoolBallDto ball, PoolTraceDto[] trace, float replayFraction,
        float replayMs, out float x, out float y)
    {
        x = ball.X;
        y = ball.Y;
        if (replayFraction >= 1f || trace.Length == 0)
        {
            return !ball.Pocketed;
        }

        PoolTraceDto? first = null;
        PoolTraceDto? last = null;
        for (var index = 0; index < trace.Length; index++)
        {
            var run = trace[index];
            if (run.Ball != ball.Number)
            {
                continue;
            }

            first ??= run;
            last = run;
            if (replayMs >= run.AtMs && replayMs <= run.AtMs + run.DurationMs)
            {
                var t = run.DurationMs <= 0f ? 1f : (replayMs - run.AtMs) / run.DurationMs;
                x = run.FromX + (run.ToX - run.FromX) * t;
                y = run.FromY + (run.ToY - run.FromY) * t;
                return true;
            }
        }

        if (first is null)
        {
            return !ball.Pocketed;
        }

        if (replayMs < first.AtMs)
        {
            x = first.FromX;
            y = first.FromY;
            return true;
        }

        if (last is not null && replayMs > last.AtMs + last.DurationMs)
        {
            x = last.ToX;
            y = last.ToY;
            return !ball.Pocketed;
        }

        for (var index = trace.Length - 1; index >= 0; index--)
        {
            var run = trace[index];
            if (run.Ball == ball.Number && run.AtMs + run.DurationMs <= replayMs)
            {
                x = run.ToX;
                y = run.ToY;
                return true;
            }
        }

        return true;
    }

    private static void DrawBall(ImDrawListPtr drawList, Vector2 center, float radius, int number, float scale)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.18f), radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 24);
        if (number == CueBall)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CueWhite), 24);
        }
        else if (number == EightBall)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(EightBlack), 24);
        }
        else
        {
            var color = BallColors[(number - 1) % 7];
            if (number < EightBall)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 24);
            }
            else
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CueWhite), 24);
                drawList.PushClipRect(center - new Vector2(radius, radius * 0.5f),
                    center + new Vector2(radius, radius * 0.5f), true);
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 24);
                drawList.PopClipRect();
            }
        }

        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.35f), radius * 0.28f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f)), 12);
        if (number != CueBall && radius >= 7f * scale)
        {
            var label = number.ToString(Loc.Culture);
            var badge = radius * 0.55f;
            drawList.AddCircleFilled(center, badge, ImGui.GetColorU32(CueWhite), 16);
            Typography.DrawCentered(drawList, center, label, EightBlack,
                MathF.Max(0.42f, badge / (9f * scale)), FontWeight.SemiBold);
        }
    }

    private void HandleShot(ImDrawListPtr drawList, Vector2 origin, float unit, float scale, PoolBallDto[] balls)
    {
        var cue = balls.Length > CueBall ? balls[CueBall] : null;
        if (cue is null || cue.Pocketed)
        {
            return;
        }

        var cueCenter = origin + new Vector2(cue.X * unit, cue.Y * unit);
        var tableMin = origin;
        var tableMax = origin + new Vector2(GameRoomWire.PoolTableWidth * unit, GameRoomWire.PoolTableHeight * unit);
        var mouse = ImGui.GetMousePos();
        var overTable = mouse.X >= tableMin.X && mouse.X <= tableMax.X && mouse.Y >= tableMin.Y
            && mouse.Y <= tableMax.Y;

        if (!dragging && overTable && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            dragStart = mouse;
        }

        if (!dragging)
        {
            if (overTable)
            {
                var direction = mouse - cueCenter;
                DrawAim(drawList, cueCenter, direction, unit, scale, 0f);
            }

            return;
        }

        // Pull back: the cue fires away from where the finger went, harder the further it went.
        var pull = mouse - dragStart;
        var pullUnits = pull.Length() / unit;
        var power = MathF.Min(1f, pullUnits / MaxDragUnits);
        var shotDirection = -pull;
        DrawAim(drawList, cueCenter, shotDirection, unit, scale, power);

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            dragging = false;
            if (power >= 0.05f && shotDirection.LengthSquared() > 0f && !store.ActInFlight)
            {
                store.SendShoot(MathF.Atan2(shotDirection.Y, shotDirection.X), power);
            }
        }
    }

    private static void DrawAim(ImDrawListPtr drawList, Vector2 cueCenter, Vector2 direction, float unit,
        float scale, float power)
    {
        if (direction.LengthSquared() <= 0f)
        {
            return;
        }

        var normalized = Vector2.Normalize(direction);
        var reach = unit * (0.35f + 0.65f * MathF.Max(power, 0.2f));
        drawList.AddLine(cueCenter, cueCenter + normalized * reach, ImGui.GetColorU32(AimLine), 1.5f * scale);
        var cueLength = unit * 0.5f;
        var cueBack = cueCenter - normalized * (GameRoomWire.PoolBallRadius * unit * 1.4f + power * unit * 0.25f);
        drawList.AddLine(cueBack, cueBack - normalized * cueLength,
            ImGui.GetColorU32(new Vector4(0.82f, 0.62f, 0.34f, 0.95f)), 4f * scale);
        if (power > 0f)
        {
            var barMin = cueCenter + new Vector2(-30f * scale, GameRoomWire.PoolBallRadius * unit + 10f * scale);
            var barMax = barMin + new Vector2(60f * scale, 6f * scale);
            drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.4f)), 3f * scale);
            drawList.AddRectFilled(barMin, new Vector2(barMin.X + 60f * scale * power, barMax.Y),
                ImGui.GetColorU32(new Vector4(0.98f, 0.62f, 0.20f, 0.95f)), 3f * scale);
        }
    }

    private void HandlePlacement(ImDrawListPtr drawList, Vector2 origin, float unit, float scale,
        PoolBallDto[] balls)
    {
        var mouse = ImGui.GetMousePos();
        var radius = GameRoomWire.PoolBallRadius;
        var x = (mouse.X - origin.X) / unit;
        var y = (mouse.Y - origin.Y) / unit;
        var inside = x >= 0f && x <= GameRoomWire.PoolTableWidth && y >= 0f && y <= GameRoomWire.PoolTableHeight;
        if (!inside)
        {
            return;
        }

        x = Math.Clamp(x, radius, GameRoomWire.PoolTableWidth - radius);
        y = Math.Clamp(y, radius, GameRoomWire.PoolTableHeight - radius);
        var clear = true;
        for (var index = 1; index < balls.Length; index++)
        {
            if (balls[index].Pocketed)
            {
                continue;
            }

            var deltaX = balls[index].X - x;
            var deltaY = balls[index].Y - y;
            if (deltaX * deltaX + deltaY * deltaY < radius * radius * 4.2f)
            {
                clear = false;
                break;
            }
        }

        var ghost = origin + new Vector2(x * unit, y * unit);
        drawList.AddCircleFilled(ghost, radius * unit, ImGui.GetColorU32(CueWhite with { W = clear ? 0.6f : 0.25f }), 24);
        drawList.AddCircle(ghost, radius * unit,
            ImGui.GetColorU32(clear ? new Vector4(1f, 1f, 1f, 0.9f) : ResignTint), 24, 1.5f * scale);
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (clear && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !store.ActInFlight)
        {
            store.SendPlace(MathF.Round(x, 4), MathF.Round(y, 4));
        }
    }

    private void DrawSeatRow(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect row,
        PoolRoomStateDto board, PoolPlayerDto[] players, int seat, GameRoomSnapshotDto snapshot, Vector4 accent)
    {
        if (seat < 0 || seat >= players.Length)
        {
            return;
        }

        var player = players[seat];
        var isMover = seat == board.TurnSeat && board.WinnerSeat < 0 && board.EndKind.Length == 0;
        var groupLabel = board.OpenTable
            ? Loc.T(L.Games.OnlineGroupOpen)
            : player.Group == GameRoomWire.PoolGroupSolids
                ? Loc.T(L.Games.OnlineGroupSolids)
                : player.Group == GameRoomWire.PoolGroupStripes
                    ? Loc.T(L.Games.OnlineGroupStripes)
                    : string.Empty;
        var remaining = RemainingOfGroup(board, player.Group);
        if (!board.OpenTable && player.Group != 0 && remaining == 0)
        {
            groupLabel = Loc.T(L.Games.OnlineOnTheEight);
        }

        var name = player.DisplayName;
        if (player.Away)
        {
            name = name + " · " + Loc.T(L.Games.OnlineAway);
        }

        var ringReserve = 26f * scale;
        var textLeft = row.Min.X + 4f * scale;
        var textWidth = row.Width - ringReserve - 8f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - 8f * scale),
            Typography.FitText(name + " · " + groupLabel, textWidth, TextStyles.SubheadlineEmphasized),
            isMover ? theme.TextStrong : theme.TextMuted, TextStyles.SubheadlineEmphasized);

        if (!board.OpenTable && player.Group != 0)
        {
            var dotsLeft = row.Max.X - ringReserve - 8f * scale - remaining * 9f * scale;
            for (var index = 0; index < remaining; index++)
            {
                drawList.AddCircleFilled(new Vector2(dotsLeft + index * 9f * scale, row.Center.Y), 3f * scale,
                    ImGui.GetColorU32(player.Group == GameRoomWire.PoolGroupSolids ? BallColors[1] : CueWhite), 12);
            }
        }

        if (isMover)
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var left = store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, nowMs);
            TurnTimerRing.Draw(drawList, new Vector2(row.Max.X - 12f * scale, row.Center.Y), 9f * scale, left,
                board.TurnSeconds, accent, scale);
        }
    }

    private static int RemainingOfGroup(PoolRoomStateDto board, int group)
    {
        if (group == 0)
        {
            return 0;
        }

        var balls = board.Balls ?? Array.Empty<PoolBallDto>();
        var remaining = 0;
        for (var index = 0; index < balls.Length; index++)
        {
            var number = balls[index].Number;
            var ballGroup = number is >= 1 and <= 7 ? GameRoomWire.PoolGroupSolids
                : number is >= 9 and <= 15 ? GameRoomWire.PoolGroupStripes : 0;
            if (ballGroup == group && !balls[index].Pocketed)
            {
                remaining++;
            }
        }

        return remaining;
    }

    private void DrawStatus(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect body, float y,
        PoolRoomStateDto board, PoolPlayerDto[] players, int mySeat, bool myTurn, bool replaying, string notice)
    {
        string status;
        if (notice.Length > 0)
        {
            status = notice;
        }
        else if (!replaying && board.LastFoul.Length > 0 && board.LastSeat != board.TurnSeat)
        {
            status = Loc.T(GamesOnlineText.FoulMessage(board.LastFoul));
            if (myTurn)
            {
                status = status + " · " + Loc.T(board.BallInHand ? L.Games.OnlineBallInHand : L.Games.OnlineYourTurn);
            }
        }
        else if (myTurn)
        {
            status = board.BallInHand
                ? Loc.T(L.Games.OnlineBallInHand)
                : board.BreakPending
                    ? Loc.T(L.Games.OnlineBreakShot) + " · " + Loc.T(L.Games.OnlineShootHint)
                    : Loc.T(L.Games.OnlineYourTurn) + " · " + Loc.T(L.Games.OnlineShootHint);
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

        if (status.Length == 0)
        {
            return;
        }

        Typography.DrawWrappedCentered(drawList, new Vector2(body.Center.X, y + 10f * scale), status,
            myTurn ? theme.TextStrong : theme.TextMuted, TextStyles.Footnote, body.Width - 28f * scale);
    }

    private static int SeatOf(PoolPlayerDto[] players, string userId)
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
