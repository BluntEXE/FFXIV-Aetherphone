using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.WordRun;

internal readonly struct KeyboardPress
{
    public readonly char Letter;
    public readonly bool Enter;
    public readonly bool Backspace;

    public KeyboardPress(char letter, bool enter, bool backspace)
    {
        Letter = letter;
        Enter = enter;
        Backspace = backspace;
    }
}

internal sealed class WordRunRenderer
{
    public const float FlipStagger = 0.08f;
    public const float FlipDuration = 0.24f;
    public const float RevealSeconds = FlipStagger * (WordRunBoard.WordLength - 1) + FlipDuration;
    public const float ShakeSeconds = 0.4f;
    public static readonly Vector4 CorrectColor = new(0.33f, 0.70f, 0.42f, 1f);
    public static readonly Vector4 PresentColor = new(0.80f, 0.65f, 0.26f, 1f);
    public static readonly Vector4 AbsentColor = new(0.30f, 0.31f, 0.36f, 1f);
    private static readonly Vector4 EmptyStroke = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 EntryFill = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 InkLight = new(0.98f, 0.98f, 1f, 1f);
    private static readonly string[] KeyboardRows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
    private static readonly string[] LetterLabels = BuildLetterLabels();
    private const float KeyGapFraction = 0.06f;
    private const float WideKeyFactor = 1.5f;

    private static string[] BuildLetterLabels()
    {
        var labels = new string[WordRunBoard.LetterCount];
        for (var index = 0; index < labels.Length; index++)
        {
            labels[index] = ((char)('A' + index)).ToString();
        }

        return labels;
    }

    public static Vector4 TileColor(WordTile tile)
    {
        switch (tile)
        {
            case WordTile.Correct:
                return CorrectColor;
            case WordTile.Present:
                return PresentColor;
            default:
                return AbsentColor;
        }
    }

    public static GameGrid Grid(Rect area) => GameGrid.Centered(area, WordRunBoard.WordLength, WordRunBoard.MaxGuesses, 0.12f);

    public void DrawBoard(WordRunBoard board, Rect area, int revealRow, float revealSeconds, float shakeRemaining, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var grid = Grid(area);
        var rounding = grid.Pitch * 0.14f;
        var shakeOffset = 0f;
        if (shakeRemaining > 0f)
        {
            var elapsed = ShakeSeconds - shakeRemaining;
            shakeOffset = MathF.Sin(elapsed * 40f) * 3f * scale * (1f - elapsed / ShakeSeconds);
        }

        for (var row = 0; row < WordRunBoard.MaxGuesses; row++)
        {
            for (var column = 0; column < WordRunBoard.WordLength; column++)
            {
                var cell = grid.Cell(column, row);
                if (row < board.RowCount)
                {
                    DrawJudgedTile(drawList, cell, board.Letter(row, column), board.Tile(row, column), rounding, scale,
                        row == revealRow ? (revealSeconds - column * FlipStagger) / FlipDuration : 1f);
                    continue;
                }

                if (row == board.RowCount && board.Outcome == WordOutcome.Playing)
                {
                    var shaken = new Rect(cell.Min + new Vector2(shakeOffset, 0f), cell.Max + new Vector2(shakeOffset, 0f));
                    DrawEntryTile(drawList, shaken, column < board.EntryLength ? board.EntryLetter(column) : '\0', rounding, scale);
                    continue;
                }

                Squircle.Stroke(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(EmptyStroke), 1f * scale);
            }
        }
    }

    private static void DrawEntryTile(ImDrawListPtr drawList, Rect cell, char letter, float rounding, float scale)
    {
        Squircle.Fill(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(EntryFill));
        Squircle.Stroke(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(EmptyStroke with { W = letter == '\0' ? 0.22f : 0.6f }),
            1.2f * scale);
        if (letter == '\0')
        {
            return;
        }

        Typography.DrawCentered(drawList, cell.Center, LetterLabels[letter - 'A'], InkLight, TextStyles.Title2.Scale,
            TextStyles.Title2.Weight);
    }

    private static void DrawJudgedTile(ImDrawListPtr drawList, Rect cell, char letter, WordTile tile, float rounding, float scale,
        float flip)
    {
        var label = LetterLabels[letter - 'A'];
        if (flip < 0.5f)
        {
            var squash = flip < 0f ? 1f : 1f - flip * 2f;
            var closed = Squash(cell, squash);
            Squircle.Fill(drawList, closed.Min, closed.Max, rounding, ImGui.GetColorU32(EntryFill));
            Squircle.Stroke(drawList, closed.Min, closed.Max, rounding, ImGui.GetColorU32(EmptyStroke with { W = 0.6f }), 1.2f * scale);
            if (squash > 0.35f)
            {
                Typography.DrawCentered(drawList, cell.Center, label, InkLight, TextStyles.Title2.Scale * squash, TextStyles.Title2.Weight);
            }

            return;
        }

        var open = flip >= 1f ? 1f : (flip - 0.5f) * 2f;
        var revealed = Squash(cell, open);
        var color = TileColor(tile);
        Squircle.FillVerticalGradient(drawList, revealed.Min, revealed.Max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.12f)), ImGui.GetColorU32(GamePalette.Darken(color, 0.14f)));
        if (open > 0.7f)
        {
            Typography.DrawCentered(drawList, cell.Center, label, InkLight, TextStyles.Title2.Scale * open, TextStyles.Title2.Weight);
        }
    }

    private static Rect Squash(Rect cell, float amount)
    {
        var halfHeight = cell.Height * 0.5f * MathF.Max(0.02f, amount);
        return new Rect(new Vector2(cell.Min.X, cell.Center.Y - halfHeight), new Vector2(cell.Max.X, cell.Center.Y + halfHeight));
    }

    public KeyboardPress DrawKeyboard(WordRunBoard board, Rect area, Vector4 accent, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var gap = MathF.Max(2f * scale, area.Width * KeyGapFraction * 0.1f);
        var sidePad = 4f * scale;
        var keyWidth = MathF.Floor((area.Width - sidePad * 2f - gap * 9f) / 10f);
        var keyHeight = MathF.Min(46f * scale, (area.Height - gap * 2f) / 3f);
        var wide = MathF.Floor(keyWidth * WideKeyFactor);
        var letter = '\0';
        var enter = false;
        var backspace = false;
        var canSubmit = board.EntryLength == WordRunBoard.WordLength;
        var canErase = board.EntryLength > 0;
        var top = area.Min.Y + (area.Height - keyHeight * 3f - gap * 2f) * 0.5f;
        for (var rowIndex = 0; rowIndex < KeyboardRows.Length; rowIndex++)
        {
            var rowLetters = KeyboardRows[rowIndex];
            var rowWidth = rowLetters.Length * keyWidth + (rowLetters.Length - 1) * gap;
            var extras = rowIndex == KeyboardRows.Length - 1;
            if (extras)
            {
                rowWidth += (wide + gap) * 2f;
            }

            var x = area.Center.X - rowWidth * 0.5f;
            var y = top + rowIndex * (keyHeight + gap);
            if (extras)
            {
                if (DrawIconKey(drawList, new Rect(new Vector2(x, y), new Vector2(x + wide, y + keyHeight)), FontAwesomeIcon.Check,
                        accent, theme, canSubmit, scale))
                {
                    enter = true;
                }

                x += wide + gap;
            }

            for (var index = 0; index < rowLetters.Length; index++)
            {
                var keyRect = new Rect(new Vector2(x, y), new Vector2(x + keyWidth, y + keyHeight));
                var letterIndex = rowLetters[index] - 'A';
                if (DrawLetterKey(drawList, keyRect, letterIndex, board.KeyState(letterIndex), accent, theme, scale))
                {
                    letter = rowLetters[index];
                }

                x += keyWidth + gap;
            }

            if (extras && DrawIconKey(drawList, new Rect(new Vector2(x, y), new Vector2(x + wide, y + keyHeight)),
                    FontAwesomeIcon.Backspace, accent, theme, canErase, scale))
            {
                backspace = true;
            }
        }

        return new KeyboardPress(letter, enter, backspace);
    }

    private static bool DrawLetterKey(ImDrawListPtr drawList, Rect key, int letterIndex, byte state, Vector4 accent, PhoneTheme theme,
        float scale)
    {
        var hovered = UiInteract.Hover(key.Min, key.Max);
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var radius = key.Height * 0.22f;
        Vector4 ink;
        switch (state)
        {
            case WordRunBoard.KeyCorrect:
                Squircle.Fill(drawList, key.Min, key.Max, radius, ImGui.GetColorU32(CorrectColor));
                ink = InkLight;
                break;
            case WordRunBoard.KeyPresent:
                Squircle.Fill(drawList, key.Min, key.Max, radius, ImGui.GetColorU32(PresentColor));
                ink = InkLight;
                break;
            case WordRunBoard.KeyAbsent:
                Squircle.Fill(drawList, key.Min, key.Max, radius, ImGui.GetColorU32(AbsentColor with { W = 0.55f }));
                ink = theme.TextMuted;
                break;
            default:
                Material.Frosted(drawList, key.Min, key.Max, radius, scale, held ? 1f : 0.85f);
                ink = theme.TextStrong;
                break;
        }

        if (held)
        {
            Squircle.Stroke(drawList, key.Min, key.Max, radius, ImGui.GetColorU32(accent with { W = 0.9f }), 1.5f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, key.Center, LetterLabels[letterIndex], ink, TextStyles.Headline.Scale, TextStyles.Headline.Weight);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool DrawIconKey(ImDrawListPtr drawList, Rect key, FontAwesomeIcon icon, Vector4 accent, PhoneTheme theme,
        bool armed, float scale)
    {
        var hovered = UiInteract.Hover(key.Min, key.Max);
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var radius = key.Height * 0.22f;
        Material.Frosted(drawList, key.Min, key.Max, radius, scale, armed ? (held ? 1f : 0.85f) : 0.45f);
        if (armed)
        {
            Squircle.Stroke(drawList, key.Min, key.Max, radius, ImGui.GetColorU32(accent with { W = held ? 0.9f : 0.45f }), 1.2f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ProgressRing.CenterIcon(drawList, key.Center, icon, armed ? theme.TextStrong : theme.TextMuted, key.Height * 0.42f);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
