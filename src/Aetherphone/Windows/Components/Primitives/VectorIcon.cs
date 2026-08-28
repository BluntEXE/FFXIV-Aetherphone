using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class VectorIcon
{
    private const float OpMove = 0f;
    private const float OpLine = 1f;
    private const float OpCubic = 2f;
    private const float OpArc = 3f;
    private const float OpClose = 4f;
    private const float MinThickness = 1f;
    private const float ArcSegmentsPerRadian = 7f;

    private readonly float[] commands;
    private readonly float viewBox;

    private VectorIcon(float[] commands, float viewBox)
    {
        this.commands = commands;
        this.viewBox = viewBox;
    }

    public static VectorIcon Parse(string path, float viewBox = 24f)
    {
        var parser = new PathParser(path);
        return new VectorIcon(parser.Run(), viewBox);
    }

    public void Stroke(ImDrawListPtr drawList, Vector2 center, float size, uint color, float strokeWidth,
        bool roundCaps = true)
    {
        var unit = size / viewBox;
        var thickness = MathF.Max(MinThickness, strokeWidth * unit);
        var capRadius = thickness * 0.5f;
        var origin = center - new Vector2(viewBox * 0.5f * unit, viewBox * 0.5f * unit);
        var open = false;
        var subpathStart = Vector2.Zero;
        var current = Vector2.Zero;
        var index = 0;
        drawList.PathClear();
        while (index < commands.Length)
        {
            var op = commands[index++];
            if (op == OpMove)
            {
                FlushOpen(drawList, ref open, color, thickness, roundCaps, capRadius, subpathStart, current);
                current = Map(commands[index], commands[index + 1], origin, unit);
                index += 2;
                subpathStart = current;
                drawList.PathLineTo(current);
                open = true;
            }
            else if (op == OpLine)
            {
                current = Map(commands[index], commands[index + 1], origin, unit);
                index += 2;
                drawList.PathLineTo(current);
            }
            else if (op == OpCubic)
            {
                var control1 = Map(commands[index], commands[index + 1], origin, unit);
                var control2 = Map(commands[index + 2], commands[index + 3], origin, unit);
                current = Map(commands[index + 4], commands[index + 5], origin, unit);
                index += 6;
                drawList.PathBezierCubicCurveTo(control1, control2, current, 0);
            }
            else if (op == OpArc)
            {
                var arcCenter = Map(commands[index], commands[index + 1], origin, unit);
                var radius = commands[index + 2] * unit;
                var startAngle = commands[index + 3];
                var endAngle = commands[index + 4];
                index += 5;
                var segments = Math.Max(3, (int)MathF.Ceiling(MathF.Abs(endAngle - startAngle) * ArcSegmentsPerRadian));
                drawList.PathArcTo(arcCenter, radius, startAngle, endAngle, segments);
                current = arcCenter + new Vector2(MathF.Cos(endAngle), MathF.Sin(endAngle)) * radius;
            }
            else
            {
                drawList.PathStroke(color, ImDrawFlags.Closed, thickness);
                open = false;
                current = subpathStart;
            }
        }

        FlushOpen(drawList, ref open, color, thickness, roundCaps, capRadius, subpathStart, current);
    }

    private static void FlushOpen(ImDrawListPtr drawList, ref bool open, uint color, float thickness, bool roundCaps,
        float capRadius, Vector2 start, Vector2 end)
    {
        if (!open)
        {
            return;
        }

        drawList.PathStroke(color, ImDrawFlags.None, thickness);
        if (roundCaps)
        {
            drawList.AddCircleFilled(start, capRadius, color, 8);
            drawList.AddCircleFilled(end, capRadius, color, 8);
        }

        open = false;
    }

    private static Vector2 Map(float x, float y, Vector2 origin, float unit) => origin + new Vector2(x, y) * unit;

    private ref struct PathParser
    {
        private readonly ReadOnlySpan<char> text;
        private int cursor;
        private readonly List<float> output;
        private float currentX;
        private float currentY;
        private float startX;
        private float startY;
        private float lastControlX;
        private float lastControlY;
        private bool lastWasCubic;

        public PathParser(string path)
        {
            text = path.AsSpan();
            cursor = 0;
            output = new List<float>(64);
            currentX = 0f;
            currentY = 0f;
            startX = 0f;
            startY = 0f;
            lastControlX = 0f;
            lastControlY = 0f;
            lastWasCubic = false;
        }

        public float[] Run()
        {
            var command = '\0';
            while (true)
            {
                SkipSeparators();
                if (cursor >= text.Length)
                {
                    break;
                }

                if (char.IsLetter(text[cursor]))
                {
                    command = text[cursor++];
                }

                Execute(command);
            }

            return output.ToArray();
        }

        private void Execute(char command)
        {
            var relative = char.IsLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                {
                    var x = ReadNumber();
                    var y = ReadNumber();
                    if (relative)
                    {
                        x += currentX;
                        y += currentY;
                    }

                    Emit(OpMove, x, y);
                    startX = x;
                    startY = y;
                    SetCurrent(x, y, false);
                    break;
                }
                case 'L':
                {
                    var x = ReadNumber();
                    var y = ReadNumber();
                    if (relative)
                    {
                        x += currentX;
                        y += currentY;
                    }

                    Emit(OpLine, x, y);
                    SetCurrent(x, y, false);
                    break;
                }
                case 'H':
                {
                    var x = ReadNumber();
                    if (relative)
                    {
                        x += currentX;
                    }

                    Emit(OpLine, x, currentY);
                    SetCurrent(x, currentY, false);
                    break;
                }
                case 'V':
                {
                    var y = ReadNumber();
                    if (relative)
                    {
                        y += currentY;
                    }

                    Emit(OpLine, currentX, y);
                    SetCurrent(currentX, y, false);
                    break;
                }
                case 'C':
                {
                    var x1 = ReadNumber();
                    var y1 = ReadNumber();
                    var x2 = ReadNumber();
                    var y2 = ReadNumber();
                    var x = ReadNumber();
                    var y = ReadNumber();
                    if (relative)
                    {
                        x1 += currentX;
                        y1 += currentY;
                        x2 += currentX;
                        y2 += currentY;
                        x += currentX;
                        y += currentY;
                    }

                    EmitCubic(x1, y1, x2, y2, x, y);
                    break;
                }
                case 'S':
                {
                    var x2 = ReadNumber();
                    var y2 = ReadNumber();
                    var x = ReadNumber();
                    var y = ReadNumber();
                    if (relative)
                    {
                        x2 += currentX;
                        y2 += currentY;
                        x += currentX;
                        y += currentY;
                    }

                    var x1 = lastWasCubic ? currentX * 2f - lastControlX : currentX;
                    var y1 = lastWasCubic ? currentY * 2f - lastControlY : currentY;
                    EmitCubic(x1, y1, x2, y2, x, y);
                    break;
                }
                case 'A':
                {
                    var radiusX = ReadNumber();
                    ReadNumber();
                    ReadNumber();
                    var largeArc = ReadFlag();
                    var sweep = ReadFlag();
                    var x = ReadNumber();
                    var y = ReadNumber();
                    if (relative)
                    {
                        x += currentX;
                        y += currentY;
                    }

                    EmitArc(radiusX, largeArc, sweep, x, y);
                    SetCurrent(x, y, false);
                    break;
                }
                case 'Z':
                    output.Add(OpClose);
                    SetCurrent(startX, startY, false);
                    break;
                default:
                    cursor = text.Length;
                    break;
            }
        }

        private void EmitCubic(float x1, float y1, float x2, float y2, float x, float y)
        {
            output.Add(OpCubic);
            output.Add(x1);
            output.Add(y1);
            output.Add(x2);
            output.Add(y2);
            output.Add(x);
            output.Add(y);
            lastControlX = x2;
            lastControlY = y2;
            SetCurrent(x, y, true);
        }

        private void EmitArc(float radius, bool largeArc, bool sweep, float x, float y)
        {
            var deltaX = (currentX - x) * 0.5f;
            var deltaY = (currentY - y) * 0.5f;
            var half = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (half <= 0.0001f)
            {
                return;
            }

            var effectiveRadius = MathF.Max(radius, half);
            var offsetScale = MathF.Sqrt(MathF.Max(0f, effectiveRadius * effectiveRadius - half * half)) / half;
            if (largeArc == sweep)
            {
                offsetScale = -offsetScale;
            }

            var centerX = (currentX + x) * 0.5f + offsetScale * deltaY;
            var centerY = (currentY + y) * 0.5f - offsetScale * deltaX;
            var startAngle = MathF.Atan2(currentY - centerY, currentX - centerX);
            var endAngle = MathF.Atan2(y - centerY, x - centerX);
            if (sweep && endAngle < startAngle)
            {
                endAngle += MathF.PI * 2f;
            }
            else if (!sweep && endAngle > startAngle)
            {
                endAngle -= MathF.PI * 2f;
            }

            output.Add(OpArc);
            output.Add(centerX);
            output.Add(centerY);
            output.Add(effectiveRadius);
            output.Add(startAngle);
            output.Add(endAngle);
        }

        private void Emit(float op, float x, float y)
        {
            output.Add(op);
            output.Add(x);
            output.Add(y);
        }

        private void SetCurrent(float x, float y, bool cubic)
        {
            currentX = x;
            currentY = y;
            lastWasCubic = cubic;
        }

        private void SkipSeparators()
        {
            while (cursor < text.Length && (text[cursor] == ' ' || text[cursor] == ','))
            {
                cursor++;
            }
        }

        private bool ReadFlag()
        {
            SkipSeparators();
            var flag = cursor < text.Length && text[cursor] == '1';
            cursor++;
            return flag;
        }

        private float ReadNumber()
        {
            SkipSeparators();
            var start = cursor;
            if (cursor < text.Length && (text[cursor] == '-' || text[cursor] == '+'))
            {
                cursor++;
            }

            var seenDot = false;
            while (cursor < text.Length)
            {
                var character = text[cursor];
                if (char.IsDigit(character))
                {
                    cursor++;
                    continue;
                }

                if (character == '.' && !seenDot)
                {
                    seenDot = true;
                    cursor++;
                    continue;
                }

                break;
            }

            return float.Parse(text[start..cursor], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
