using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private void DrawInfo(PhoneContext context, Rect area, float scale)
    {
        ui.Body(area);
        var accentedContext = new PhoneContext(area, accentedTheme, context.Navigation);
        AppHeader.Draw(accentedContext, Loc.T(L.AetherStream.InfoTitle), () => router.Pop());

        var margin = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top), new Vector2(area.Max.X - margin, area.Max.Y));

        using (AppSurface.Begin(content))
        {
            DrawInfoEntry(L.AetherStream.InfoVpnTitle, L.AetherStream.InfoVpnBody, scale);
            DrawInfoEntry(L.AetherStream.InfoStartupTitle, L.AetherStream.InfoStartupBody, scale);
            DrawInfoEntry(L.AetherStream.InfoFailuresTitle, L.AetherStream.InfoFailuresBody, scale);
            DrawInfoEntry(L.AetherStream.InfoPartiesTitle, L.AetherStream.InfoPartiesBody, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawInfoEntry(LocString title, LocString body, float scale)
    {
        SettingsSection.Header(Loc.T(title), accentedTheme);
        SettingsSection.Hint(Loc.T(body), accentedTheme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }
}
