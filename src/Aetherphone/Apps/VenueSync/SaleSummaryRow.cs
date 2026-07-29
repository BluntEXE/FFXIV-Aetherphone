using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.VenueSync;

internal static class SaleSummaryRow
{
    public const float Height = 40f;

    public static void Draw(Rect row, int count, decimal total, PhoneTheme theme)
    {
        var text = count == 0 ? "No sales logged this session" : $"{count} sale{(count == 1 ? "" : "s")} · {total:N0}g";
        Marquee.DrawLeftAuto("sale-summary", text, row.Min.X, row.Min.Y, row.Width, TextStyles.Body, theme.TextStrong);
    }
}
