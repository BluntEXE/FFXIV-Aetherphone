using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Invaders;

internal sealed class InvadersRenderer
{
    public static readonly Vector4 BombColor = new(1f, 0.62f, 0.30f, 1f);
    public static readonly Vector4 BulletColor = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 SaucerColor = new(0.98f, 0.45f, 0.62f, 1f);
    private static readonly Vector4 TopRowColor = new(0.98f, 0.95f, 0.90f, 1f);
    private static readonly PixelSprite[][] InvaderSprites =
    {
        new[]
        {
            new PixelSprite("...##...", "..####..", ".######.", "##.##.##", "########", "..#..#.."),
            new PixelSprite("...##...", "..####..", ".######.", "##.##.##", "########", ".#.##.#."),
        },
        new[]
        {
            new PixelSprite("..#..#..", "#.####.#", "########", "##.##.##", ".######.", "#......#"),
            new PixelSprite("..#..#..", ".######.", "########", "##.##.##", ".######.", "..#..#.."),
        },
        new[]
        {
            new PixelSprite(".######.", "########", "##.##.##", "########", ".#.##.#.", "#.#..#.#"),
            new PixelSprite(".######.", "########", "##.##.##", "########", "..#..#..", ".##..##."),
        },
    };

    public static readonly PixelSprite Cannon = new("...#...", "..###..", ".#####.", "#######", "#######");
    private static readonly PixelSprite Saucer = new("...######...", ".##########.", "############", "..#..##..#..");

    public void Draw(InvadersBoard board, Rect field, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(field.Min, field.Max, true);
        var factor = field.Width / InvadersBoard.Width;
        DrawGround(drawList, field, factor, accent);
        DrawShields(drawList, board, field, factor, accent);
        DrawInvaders(drawList, board, field, factor, accent);
        DrawSaucer(drawList, board, field, factor);
        DrawPlayer(drawList, board, field, factor, accent);
        DrawBullet(drawList, board, field, factor, scale);
        DrawBombs(drawList, board, field, factor, scale);
        drawList.PopClipRect();
    }

    public static Vector4 KindColor(int kind, Vector4 accent)
    {
        switch (kind)
        {
            case 0:
                return TopRowColor;
            case 1:
                return GamePalette.Lighten(accent, 0.3f);
            default:
                return accent;
        }
    }

    private static Vector2 ToScreen(Rect field, float factor, Vector2 world) => field.Min + world * factor;

    private static void DrawGround(ImDrawListPtr drawList, Rect field, float factor, Vector4 accent)
    {
        var groundY = field.Min.Y + (InvadersBoard.PlayerY + InvadersBoard.PlayerHeight * 0.5f + 2f) * factor;
        drawList.AddLine(new Vector2(field.Min.X, groundY), new Vector2(field.Max.X, groundY),
            ImGui.GetColorU32(accent with { W = 0.5f }), MathF.Max(1f, 0.6f * factor));
    }

    private static void DrawShields(ImDrawListPtr drawList, InvadersBoard board, Rect field, float factor, Vector4 accent)
    {
        var fill = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.12f) with { W = 0.85f });
        var cell = InvadersBoard.ShieldCell * factor;
        var inset = MathF.Max(0.5f, cell * 0.08f);
        for (var shield = 0; shield < InvadersBoard.ShieldCount; shield++)
        {
            for (var column = 0; column < InvadersBoard.ShieldColumns; column++)
            {
                for (var row = 0; row < InvadersBoard.ShieldRows; row++)
                {
                    if (!board.ShieldCellAlive(shield, column, row))
                    {
                        continue;
                    }

                    var min = ToScreen(field, factor, InvadersBoard.ShieldCellPosition(shield, column, row));
                    drawList.AddRectFilled(min + new Vector2(inset, inset), min + new Vector2(cell - inset, cell - inset),
                        fill);
                }
            }
        }
    }

    private static void DrawInvaders(ImDrawListPtr drawList, InvadersBoard board, Rect field, float factor, Vector4 accent)
    {
        var unit = InvadersBoard.InvaderWidth / 8f * factor;
        var frame = board.AnimFrame ? 1 : 0;
        for (var row = 0; row < InvadersBoard.Rows; row++)
        {
            var kind = InvadersBoard.RowKinds[row];
            var color = ImGui.GetColorU32(KindColor(kind, accent));
            var sprite = InvaderSprites[kind][frame];
            for (var column = 0; column < InvadersBoard.Columns; column++)
            {
                if (!board.InvaderAlive(column, row))
                {
                    continue;
                }

                sprite.Draw(drawList, ToScreen(field, factor, board.InvaderPosition(column, row)), unit, color);
            }
        }
    }

    private static void DrawSaucer(ImDrawListPtr drawList, InvadersBoard board, Rect field, float factor)
    {
        if (!board.SaucerActive)
        {
            return;
        }

        var center = ToScreen(field, factor, new Vector2(board.SaucerX, InvadersBoard.SaucerY));
        var unit = InvadersBoard.SaucerHalfWidth * 2f / Saucer.Width * factor;
        ProgressRing.Glow(center, InvadersBoard.SaucerHalfWidth * factor * 1.4f, SaucerColor, 0.6f);
        Saucer.DrawCentered(drawList, center, unit, ImGui.GetColorU32(SaucerColor));
    }

    private static void DrawPlayer(ImDrawListPtr drawList, InvadersBoard board, Rect field, float factor, Vector4 accent)
    {
        if (board.Respawning && Pulse.Wave(Pulse.Fast) > 0.5f)
        {
            return;
        }

        var center = ToScreen(field, factor, new Vector2(board.PlayerX, InvadersBoard.PlayerY - InvadersBoard.PlayerHeight * 0.5f));
        var unit = InvadersBoard.PlayerWidth / Cannon.Width * factor;
        ProgressRing.Glow(center, InvadersBoard.PlayerWidth * factor * 0.7f, accent, 0.35f);
        Cannon.DrawCentered(drawList, center, unit, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.2f)));
    }

    private static void DrawBullet(ImDrawListPtr drawList, InvadersBoard board, Rect field, float factor, float scale)
    {
        if (!board.HasBullet)
        {
            return;
        }

        var position = ToScreen(field, factor, board.Bullet);
        var halfWidth = MathF.Max(1f, 0.5f * factor);
        ProgressRing.Glow(position, 2.5f * factor, BulletColor, 0.5f);
        drawList.AddRectFilled(position - new Vector2(halfWidth, 3f * factor), position + new Vector2(halfWidth, 0f),
            ImGui.GetColorU32(BulletColor));
    }

    private static void DrawBombs(ImDrawListPtr drawList, InvadersBoard board, Rect field, float factor, float scale)
    {
        var color = ImGui.GetColorU32(BombColor);
        var halfWidth = MathF.Max(1f, 0.6f * factor);
        for (var index = 0; index < board.BombCount; index++)
        {
            var position = ToScreen(field, factor, board.GetBomb(index));
            ProgressRing.Glow(position, 2.5f * factor, BombColor, 0.45f);
            drawList.AddRectFilled(position - new Vector2(halfWidth, 0f), position + new Vector2(halfWidth, 3f * factor),
                color);
        }
    }
}
