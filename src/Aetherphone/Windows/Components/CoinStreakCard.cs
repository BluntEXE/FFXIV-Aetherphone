using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class CoinStreakCard
{
    private const float CardHeight = 110f;
    private const float Rounding = 20f;
    private const float TileSize = 36f;
    private const float ButtonHeight = 34f;
    private const float ButtonWidth = 108f;
    private const float DotRadius = 5f;
    private const float TrackThickness = 2f;
    private const int WeekDays = 7;

    public static bool Draw(CoinWalletDto wallet, in AppPalette palette, PhoneTheme theme, bool enabled,
        out Rect buttonRect)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = CardHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = Rounding * scale;
        var accent = palette.Accent;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var inset = 16f * scale;
        var tileSize = TileSize * scale;
        var rowCenterY = min.Y + 34f * scale;
        var tileCenter = new Vector2(min.X + inset + tileSize * 0.5f, rowCenterY);
        IconTile.Draw(tileCenter, tileSize, IconTile.Surface(accent), FontAwesomeIcon.Fire);

        var buttonWidth = MathF.Min(ButtonWidth * scale, width * 0.42f);
        buttonRect = new Rect(
            new Vector2(max.X - inset - buttonWidth, rowCenterY - ButtonHeight * scale * 0.5f),
            new Vector2(max.X - inset, rowCenterY + ButtonHeight * scale * 0.5f));

        var textLeft = tileCenter.X + tileSize * 0.5f + 12f * scale;
        var textWidth = MathF.Max(40f * scale, buttonRect.Min.X - 10f * scale - textLeft);
        var title = Loc.T(L.Coin.StreakDays, wallet.StreakDays.ToString("N0", Loc.Culture));
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var hint = wallet.CheckInAvailable ? Loc.T(L.Coin.StreakClaim) : Loc.T(L.Coin.StreakNext);
        var hintSize = Typography.Measure(hint, TextStyles.Footnote);
        var textTop = rowCenterY - (titleSize.Y + hintSize.Y + 3f * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(title, textWidth, TextStyles.SubheadlineEmphasized), palette.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleSize.Y + 3f * scale),
            Typography.FitText(hint, textWidth, TextStyles.Footnote), palette.MutedInk, TextStyles.Footnote);

        DrawWeek(drawList, wallet, accent, palette, min, max, inset, scale);

        var label = wallet.CheckInAvailable ? Loc.T(L.Coin.CheckIn) : Loc.T(L.Coin.CheckedIn);
        var pressed = AppSkin.PillButton(buttonRect, label, wallet.CheckInAvailable, enabled, theme);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 4f * scale));
        return pressed;
    }

    private static void DrawWeek(ImDrawListPtr drawList, CoinWalletDto wallet, Vector4 accent, in AppPalette palette,
        Vector2 min, Vector2 max, float inset, float scale)
    {
        var cycle = wallet.StreakDays % WeekDays;
        var filled = cycle == 0 && wallet.StreakDays > 0 ? WeekDays : cycle;
        var pending = wallet.CheckInAvailable && filled < WeekDays ? filled : -1;
        var radius = DotRadius * scale;
        var centerY = max.Y - 24f * scale;
        var left = min.X + inset + radius;
        var right = max.X - inset - radius;
        var step = (right - left) / (WeekDays - 1);

        drawList.AddLine(new Vector2(left, centerY), new Vector2(right, centerY),
            ImGui.GetColorU32(Palette.WithAlpha(palette.TitleInk, 0.08f)), TrackThickness * scale);
        if (filled > 1)
        {
            drawList.AddLine(new Vector2(left, centerY), new Vector2(left + step * (filled - 1), centerY),
                ImGui.GetColorU32(Palette.WithAlpha(accent, 0.55f)), TrackThickness * scale);
        }

        for (var dayIndex = 0; dayIndex < WeekDays; dayIndex++)
        {
            var center = new Vector2(left + step * dayIndex, centerY);
            if (dayIndex < filled)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent), 20);
                continue;
            }

            if (dayIndex == pending)
            {
                var breath = 0.45f + 0.35f * Pulse.Wave(Pulse.Breath);
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.16f)), 20);
                drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, breath)), 20,
                    1.6f * scale);
                continue;
            }

            drawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32(Palette.WithAlpha(palette.TitleInk, 0.12f)), 20);
        }
    }
}
