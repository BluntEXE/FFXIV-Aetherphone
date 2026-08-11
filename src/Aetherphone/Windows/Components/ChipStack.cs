using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ChipStack
{
    public const int DenominationCount = 6;

    public const int MaxColumns = 3;

    public const int MaxDiscsPerColumn = 4;

    public static readonly long[] Denominations = { 1000, 500, 100, 25, 5, 1 };

    private const float DiscRadius = 9f;
    private const float DiscStep = 4.2f;
    private const float ColumnGap = 3f;
    private const float LabelGap = 5f;

    private static readonly Vector4[] DiscColors =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(0.62f, 0.45f, 0.90f, 1f),
        new(0.30f, 0.58f, 0.94f, 1f),
        new(0.17f, 0.70f, 0.54f, 1f),
        new(0.90f, 0.36f, 0.46f, 1f),
        new(0.60f, 0.65f, 0.70f, 1f),
    };

    public static Vector4 ColorFor(long denomination)
    {
        for (var index = 0; index < Denominations.Length; index++)
        {
            if (Denominations[index] == denomination)
            {
                return DiscColors[index];
            }
        }

        return DiscColors[DenominationCount - 1];
    }

    public static int Breakdown(long amount, Span<int> counts)
    {
        counts.Clear();
        if (amount <= 0)
        {
            return 0;
        }

        var used = 0;
        var remaining = amount;
        for (var index = 0; index < Denominations.Length && used < MaxColumns; index++)
        {
            var denomination = Denominations[index];
            if (remaining < denomination)
            {
                continue;
            }

            var count = (int)(remaining / denomination);
            remaining -= count * denomination;
            counts[index] = count > MaxDiscsPerColumn ? MaxDiscsPerColumn : count;
            used++;
        }

        return used;
    }

    public static float HeightFor(float scale)
    {
        return (DiscRadius * 2f + DiscStep * (MaxDiscsPerColumn - 1) + LabelGap + 14f) * scale;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 baseCenter, long amount, float scale, Vector4 ink)
    {
        if (amount <= 0)
        {
            return;
        }

        Span<int> counts = stackalloc int[DenominationCount];
        var columns = Breakdown(amount, counts);
        var radius = DiscRadius * scale;
        var step = DiscStep * scale;
        var columnWidth = radius * 2f + ColumnGap * scale;
        var left = baseCenter.X - (columns - 1) * columnWidth * 0.5f;
        var column = 0;
        for (var index = 0; index < DenominationCount; index++)
        {
            if (counts[index] == 0)
            {
                continue;
            }

            var centerX = left + column * columnWidth;
            for (var disc = 0; disc < counts[index]; disc++)
            {
                var center = new Vector2(centerX, baseCenter.Y - disc * step);
                DrawDisc(drawList, center, radius, DiscColors[index]);
            }

            column++;
        }

        var label = amount.ToString("N0", Loc.Culture);
        Typography.DrawCentered(drawList, new Vector2(baseCenter.X, baseCenter.Y + radius + LabelGap * scale + 6f * scale),
            label, ink, TextStyles.Caption2);
    }

    private static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.18f), radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 20);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 20);
        drawList.AddCircle(center, radius * 0.62f, ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(color, 0.35f), 0.9f)),
            20, radius * 0.16f);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)), 20, radius * 0.12f);
    }
}
