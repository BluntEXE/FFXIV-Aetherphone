using Aetherphone.Core;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal static class ShellToast
{
    private static readonly ScreenToast Toast = new();

    public static void Show(string text) => Toast.Show(text);

    public static void Draw(Rect screen, PhoneTheme theme) => Toast.Draw(screen, ScreenToastStyle.From(theme));
}
