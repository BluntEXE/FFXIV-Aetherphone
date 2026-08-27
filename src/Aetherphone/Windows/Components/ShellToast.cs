using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ShellToast
{
    private static readonly ScreenToast Toast = new();
    private static Vector2 anchor;

    public static void Show() => Show(Loc.T(L.Common.Copied));

    public static void Show(string text)
    {
        anchor = ImGui.GetMousePos();
        Toast.Show(text);
    }

    public static void Draw(Rect host, PhoneTheme theme)
    {
        if (!host.Contains(anchor))
        {
            return;
        }

        Toast.Draw(host, ScreenToastStyle.From(theme));
    }
}
