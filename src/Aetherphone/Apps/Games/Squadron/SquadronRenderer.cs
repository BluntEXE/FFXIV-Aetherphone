using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Squadron;

internal sealed class SquadronRenderer
{
    public static readonly Vector4 RaptorColor = new(1f, 0.62f, 0.30f, 1f);
    public static readonly Vector4 WardenColor = new(0.98f, 0.45f, 0.62f, 1f);
    public static readonly Vector4 BulletColor = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 ShotColor = new(1f, 0.75f, 0.35f, 1f);
    private const float BeamFillAlpha = 0.2f;
    private const float BeamEdgeAlpha = 0.45f;
    private const float RespawnBlinkSeconds = 0.12f;
    private static readonly PixelSprite[][] Sprites =
    {
        new[]
        {
            new PixelSprite("#......#", "##.##.##", ".######.", ".######.", "..####..", "...##..."),
            new PixelSprite("........", ".#.##.#.", "########", ".######.", "..####..", "...##..."),
        },
        new[]
        {
            new PixelSprite("##....##", "###..###", ".######.", "..####..", ".##..##.", "..#..#.."),
            new PixelSprite("........", "##....##", "########", ".######.", ".##..##.", "..#..#.."),
        },
        new[]
        {
            new PixelSprite("#.####.#", "########", "##.##.##", "########", ".##..##.", "#......#"),
            new PixelSprite("#.####.#", ".######.", "##.##.##", "########", "..#..#..", ".#....#."),
        },
    };

    public static readonly PixelSprite Fighter = new("...#...", "...#...", "..###..", ".#####.", "###.###");

    public static Vector4 KindColor(ShipKind kind, Vector4 accent)
    {
        switch (kind)
        {
            case ShipKind.Drone:
                return GamePalette.Lighten(accent, 0.35f);
            case ShipKind.Raptor:
                return RaptorColor;
            default:
                return WardenColor;
        }
    }

    public void Draw(SquadronBoard board, Rect field, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(field.Min, field.Max, true);
        var factor = field.Width / SquadronBoard.Width;
        var frame = board.AnimFrame ? 1 : 0;
        var unit = SquadronBoard.ShipWidth / 8f * factor;
        for (var index = 0; index < board.ShipCount; index++)
        {
            var ship = board.GetShip(index);
            if (ship.State is ShipState.Waiting or ShipState.Gone)
            {
                continue;
            }

            var center = ToScreen(field, factor, ship.Position);
            var extent = board.BeamExtent(in ship);
            if (extent > 0f)
            {
                DrawBeam(drawList, field, factor, ship.Position, extent);
            }

            var color = KindColor(ship.Kind, accent);
            Sprites[(int)ship.Kind][frame].DrawCentered(drawList, center, unit, ImGui.GetColorU32(color));
            if (ship.HoldsCaptive)
            {
                DrawFighter(drawList, center - new Vector2(0f, SquadronBoard.ShipHeight * factor), factor * 0.8f, accent, 0.8f);
            }
        }

        DrawPlayer(drawList, board, field, factor, accent);
        DrawBullets(drawList, board, field, factor);
        DrawShots(drawList, board, field, factor, scale);
        if (board.RescueActive)
        {
            DrawFighter(drawList, ToScreen(field, factor, board.RescuePosition), factor, accent, 0.9f);
        }

        if (board.CaptureActive)
        {
            DrawFighter(drawList, ToScreen(field, factor, board.CapturePosition), factor, accent, 0.9f);
        }

        drawList.PopClipRect();
    }

    private static Vector2 ToScreen(Rect field, float factor, Vector2 world) => field.Min + world * factor;

    private static void DrawBeam(ImDrawListPtr drawList, Rect field, float factor, Vector2 shipPosition, float extent)
    {
        var top = ToScreen(field, factor, new Vector2(shipPosition.X, shipPosition.Y + SquadronBoard.ShipHeight * 0.5f));
        var reach = (SquadronBoard.PlayerRowY - (shipPosition.Y + SquadronBoard.ShipHeight * 0.5f)) * extent * factor;
        var bottomHalf = (SquadronBoard.BeamTopHalfWidth + (SquadronBoard.BeamBottomHalfWidth - SquadronBoard.BeamTopHalfWidth) * extent) * factor;
        var topHalf = SquadronBoard.BeamTopHalfWidth * factor;
        var bottomY = top.Y + reach;
        var fill = ImGui.GetColorU32(WardenColor with { W = BeamFillAlpha });
        var edge = ImGui.GetColorU32(WardenColor with { W = BeamEdgeAlpha });
        var topLeft = new Vector2(top.X - topHalf, top.Y);
        var topRight = new Vector2(top.X + topHalf, top.Y);
        var bottomLeft = new Vector2(top.X - bottomHalf, bottomY);
        var bottomRight = new Vector2(top.X + bottomHalf, bottomY);
        drawList.AddTriangleFilled(topLeft, topRight, bottomRight, fill);
        drawList.AddTriangleFilled(topLeft, bottomRight, bottomLeft, fill);
        drawList.AddLine(topLeft, bottomLeft, edge, 1.5f);
        drawList.AddLine(topRight, bottomRight, edge, 1.5f);
    }

    private static void DrawPlayer(ImDrawListPtr drawList, SquadronBoard board, Rect field, float factor, Vector4 accent)
    {
        if (board.CaptureActive)
        {
            return;
        }

        if (board.Respawning && (int)(board.RespawnRemaining / RespawnBlinkSeconds) % 2 == 0)
        {
            return;
        }

        var center = ToScreen(field, factor, board.PlayerCenter);
        if (!board.Dual)
        {
            DrawFighter(drawList, center, factor, accent, 1f);
            return;
        }

        var offset = new Vector2(SquadronBoard.PlayerWidth * 0.5f * factor, 0f);
        DrawFighter(drawList, center - offset, factor, accent, 1f);
        DrawFighter(drawList, center + offset, factor, accent, 1f);
    }

    public static void DrawFighter(ImDrawListPtr drawList, Vector2 center, float factor, Vector4 accent, float alpha)
    {
        var unit = SquadronBoard.PlayerWidth / Fighter.Width * factor;
        ProgressRing.Glow(center, SquadronBoard.PlayerWidth * factor * 0.7f, accent, 0.35f * alpha);
        Fighter.DrawCentered(drawList, center, unit, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.2f) with { W = alpha }));
    }

    private static void DrawBullets(ImDrawListPtr drawList, SquadronBoard board, Rect field, float factor)
    {
        var color = ImGui.GetColorU32(BulletColor);
        var halfWidth = MathF.Max(1f, 0.5f * factor);
        for (var index = 0; index < board.BulletCount; index++)
        {
            var position = ToScreen(field, factor, board.GetBullet(index));
            ProgressRing.Glow(position, 2.5f * factor, BulletColor, 0.5f);
            drawList.AddRectFilled(position - new Vector2(halfWidth, 3f * factor), position + new Vector2(halfWidth, 0f), color);
        }
    }

    private static void DrawShots(ImDrawListPtr drawList, SquadronBoard board, Rect field, float factor, float scale)
    {
        var color = ImGui.GetColorU32(ShotColor);
        for (var index = 0; index < board.ShotCount; index++)
        {
            var shot = board.GetShot(index);
            var head = ToScreen(field, factor, shot.Position);
            var tail = ToScreen(field, factor, shot.Position - Vector2.Normalize(shot.Velocity) * 2.4f);
            ProgressRing.Glow(head, 2.2f * factor, ShotColor, 0.45f);
            drawList.AddLine(tail, head, color, MathF.Max(1.5f, 2f * scale));
        }
    }
}
