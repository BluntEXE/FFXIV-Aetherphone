using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PhoneCasePreview
{
    private const float IslandWidthFraction = 0.34f;
    private const float IslandHeightFraction = 0.030f;
    private const float IslandTopFraction = 0.028f;

    public static void Draw(ImDrawListPtr drawList, Rect body, Vector4 caseColor, PhoneTheme theme, float scale)
    {
        var finish = new CaseFinish(caseColor);
        var chassis = ChassisGeometry.Preview(body);
        DeviceChrome.DrawShell(drawList, chassis, scale, finish, theme.ScreenBase);
        DrawIsland(drawList, chassis.Screen, finish);
    }

    private static void DrawIsland(ImDrawListPtr drawList, Rect screen, in CaseFinish finish)
    {
        var width = screen.Width * IslandWidthFraction;
        var height = MathF.Max(screen.Height * IslandHeightFraction, 2f);
        var top = screen.Min.Y + screen.Height * IslandTopFraction;
        var min = new Vector2(screen.Center.X - width * 0.5f, top);
        var max = new Vector2(screen.Center.X + width * 0.5f, top + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(finish.Glass), height * 0.5f);
    }
}
