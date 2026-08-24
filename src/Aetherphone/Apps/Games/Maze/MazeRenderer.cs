using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Maze;

internal sealed class MazeRenderer
{
    public static readonly Vector4 PlayerColor = new(1f, 0.85f, 0.30f, 1f);
    public static readonly Vector4 DotColor = new(1f, 0.95f, 0.85f, 1f);
    public static readonly Vector4 FrightColor = new(0.35f, 0.45f, 0.98f, 1f);
    private static readonly Vector4 FrightFlash = new(0.95f, 0.95f, 1f, 1f);
    private static readonly Vector4[] GhostColors =
    {
        new(0.98f, 0.35f, 0.35f, 1f), new(0.98f, 0.55f, 0.85f, 1f), new(0.40f, 0.90f, 0.95f, 1f), new(1f, 0.70f, 0.35f, 1f),
    };
    private const float FrightWarningSeconds = 2f;
    private const float MouthRate = 9f;
    private const float IdleMouth = 0.15f;
    private const float PelletPulseRate = 4f;

    public static Vector4 GhostColor(int personality) => GhostColors[personality % GhostColors.Length];

    public static Rect BoardRect(Rect area, out float cell)
    {
        cell = MathF.Max(3f, MathF.Min(area.Width / MazeBoard.Columns, area.Height / MazeBoard.Rows));
        var size = new Vector2(MazeBoard.Columns * cell, MazeBoard.Rows * cell);
        var min = area.Center - size * 0.5f;
        return new Rect(min, min + size);
    }

    public static Vector2 ToScreen(Rect board, float cell, Vector2 tile) => board.Min + (tile + new Vector2(0.5f, 0.5f)) * cell;

    public void Draw(MazeBoard board, Rect boardRect, float cell, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(boardRect.Min, boardRect.Max, true);
        var time = (float)ImGui.GetTime();
        DrawTiles(drawList, board, boardRect, cell, accent, scale, time);
        DrawGhosts(drawList, board, boardRect, cell, time);
        DrawPlayer(drawList, board, boardRect, cell, time);
        drawList.PopClipRect();
    }

    private static void DrawTiles(ImDrawListPtr drawList, MazeBoard board, Rect boardRect, float cell, Vector4 accent,
        float scale, float time)
    {
        var wallFill = ImGui.GetColorU32(GamePalette.Darken(accent, 0.4f) with { W = 0.92f });
        var wallEdge = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.25f) with { W = 0.55f });
        var door = ImGui.GetColorU32(accent with { W = 0.5f });
        var dot = ImGui.GetColorU32(DotColor);
        var inset = MathF.Max(0.5f, cell * 0.06f);
        var rounding = cell * 0.22f;
        var dotRadius = MathF.Max(1.2f, cell * 0.09f);
        var pulse = 0.7f + MathF.Abs(MathF.Sin(time * PelletPulseRate)) * 0.5f;
        for (var y = 0; y < MazeBoard.Rows; y++)
        {
            for (var x = 0; x < MazeBoard.Columns; x++)
            {
                var tile = board.Tile(x, y);
                var min = boardRect.Min + new Vector2(x * cell, y * cell);
                var center = min + new Vector2(cell * 0.5f, cell * 0.5f);
                switch (tile)
                {
                    case MazeBoard.Wall:
                        drawList.AddRectFilled(min + new Vector2(inset, inset), min + new Vector2(cell - inset, cell - inset),
                            wallFill, rounding);
                        drawList.AddRect(min + new Vector2(inset, inset), min + new Vector2(cell - inset, cell - inset),
                            wallEdge, rounding, ImDrawFlags.None, MathF.Max(1f, scale));
                        break;
                    case MazeBoard.Door:
                        drawList.AddRectFilled(new Vector2(min.X + inset, min.Y + cell * 0.42f),
                            new Vector2(min.X + cell - inset, min.Y + cell * 0.58f), door);
                        break;
                    case MazeBoard.Dot:
                        drawList.AddCircleFilled(center, dotRadius, dot, 8);
                        break;
                    case MazeBoard.Pellet:
                        ProgressRing.Glow(center, cell * 0.4f, PlayerColor, 0.5f * pulse);
                        drawList.AddCircleFilled(center, cell * 0.24f * pulse, ImGui.GetColorU32(PlayerColor), 14);
                        break;
                }
            }
        }
    }

    private static void DrawPlayer(ImDrawListPtr drawList, MazeBoard board, Rect boardRect, float cell, float time)
    {
        var center = ToScreen(boardRect, cell, board.PlayerPosition);
        var radius = cell * 0.42f;
        var direction = board.PlayerDirection;
        var moving = direction != Vector2.Zero && !board.Frozen;
        var mouth = moving ? MathF.Abs(MathF.Sin(time * MouthRate)) * 0.5f : IdleMouth;
        if (board.Dying)
        {
            var progress = board.DeathProgress;
            radius *= 1f - progress;
            mouth = IdleMouth + progress * (MathF.PI - IdleMouth);
            if (radius <= 0.5f)
            {
                return;
            }
        }

        var angle = direction == Vector2.Zero ? 0f : MathF.Atan2(direction.Y, direction.X);
        var color = ImGui.GetColorU32(PlayerColor);
        ProgressRing.Glow(center, radius * 1.3f, PlayerColor, 0.45f);
        drawList.PathClear();
        drawList.PathLineTo(center);
        drawList.PathArcTo(center, radius, angle + mouth, angle + MathF.PI * 2f - mouth, 24);
        drawList.PathFillConvex(color);
    }

    private static void DrawGhosts(ImDrawListPtr drawList, MazeBoard board, Rect boardRect, float cell, float time)
    {
        var warning = board.FrightRemaining > 0f && board.FrightRemaining < FrightWarningSeconds &&
            MathF.Sin(time * 14f) > 0f;
        for (var index = 0; index < MazeBoard.GhostCount; index++)
        {
            var ghost = board.GetGhost(index);
            var center = ToScreen(boardRect, cell, ghost.Position);
            var radius = cell * 0.42f;
            if (ghost.State == GhostState.Eyes)
            {
                DrawEyes(drawList, center, radius, ghost.Direction, new Vector4(1f, 1f, 1f, 1f));
                continue;
            }

            var body = ghost.State == GhostState.Frightened ? (warning ? FrightFlash : FrightColor) : GhostColor(ghost.Personality);
            DrawGhostBody(drawList, center, radius, body);
            if (ghost.State == GhostState.Frightened)
            {
                DrawFrightFace(drawList, center, radius, warning ? FrightColor : FrightFlash);
                continue;
            }

            DrawEyes(drawList, center, radius, ghost.Direction, new Vector4(1f, 1f, 1f, 1f));
        }
    }

    private static void DrawGhostBody(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        var fill = ImGui.GetColorU32(color);
        var domeCenter = new Vector2(center.X, center.Y - radius * 0.15f);
        var bottom = center.Y + radius;
        ProgressRing.Glow(center, radius * 1.25f, color, 0.35f);
        drawList.AddCircleFilled(domeCenter, radius, fill, 20);
        drawList.AddRectFilled(new Vector2(center.X - radius, domeCenter.Y), new Vector2(center.X + radius, bottom - radius * 0.3f),
            fill);
        var bump = radius / 3f;
        for (var bumpIndex = 0; bumpIndex < 3; bumpIndex++)
        {
            var bumpCenter = new Vector2(center.X - radius + bump + bumpIndex * bump * 2f, bottom - bump);
            drawList.AddCircleFilled(bumpCenter, bump, fill, 10);
        }
    }

    private static void DrawEyes(ImDrawListPtr drawList, Vector2 center, float radius, Vector2 direction, Vector4 color)
    {
        var eyeRadius = radius * 0.26f;
        var eyeY = center.Y - radius * 0.27f;
        var white = ImGui.GetColorU32(color);
        var pupil = ImGui.GetColorU32(new Vector4(0.1f, 0.12f, 0.3f, 1f));
        var look = direction * eyeRadius * 0.45f;
        for (var side = -1; side <= 1; side += 2)
        {
            var eye = new Vector2(center.X + side * radius * 0.38f, eyeY);
            drawList.AddCircleFilled(eye, eyeRadius, white, 12);
            drawList.AddCircleFilled(eye + look, eyeRadius * 0.5f, pupil, 8);
        }
    }

    private static void DrawFrightFace(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        var ink = ImGui.GetColorU32(color);
        var eyeRadius = radius * 0.14f;
        var eyeY = center.Y - radius * 0.27f;
        drawList.AddCircleFilled(new Vector2(center.X - radius * 0.36f, eyeY), eyeRadius, ink, 8);
        drawList.AddCircleFilled(new Vector2(center.X + radius * 0.36f, eyeY), eyeRadius, ink, 8);
        var mouthY = center.Y + radius * 0.35f;
        var thickness = MathF.Max(1f, radius * 0.12f);
        for (var segment = 0; segment < 4; segment++)
        {
            var from = new Vector2(center.X - radius * 0.6f + segment * radius * 0.3f, mouthY + (segment % 2 == 0 ? radius * 0.12f : -radius * 0.12f));
            var to = new Vector2(from.X + radius * 0.3f, mouthY + (segment % 2 == 0 ? -radius * 0.12f : radius * 0.12f));
            drawList.AddLine(from, to, ink, thickness);
        }
    }
}
