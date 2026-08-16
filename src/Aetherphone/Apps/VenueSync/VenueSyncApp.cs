using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp : IPhoneApp
{
    public string Id => "venue-sync";
    public string DisplayName => Loc.T(L.Apps.VenueSync);
    public string Glyph => "VM";
    public int BadgeCount => 0;

    private readonly VenueSyncApiClient client;
    private readonly VenueSyncState state;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly ViewRouter<VenueSyncRoute> router;
    private readonly RouterDraw<VenueSyncRoute> drawView;
    private readonly Action back;

    // Populated at the top of Draw() and reused by sub-screens so they don't need to
    // reconstruct PhoneContext themselves, mirrors the pattern used by VenuesApp/FeedbackApp.
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    // AppSkin instance for content-app conventions (SectionHeading, EmptyState, hero Card),
    // mirrors the Notes/Calendar pattern of deriving a palette from the live PhoneTheme rather
    // than a hardcoded per-app palette.
    private readonly AppSkin ui = new(AppPalettes.VenueSync(PhoneTheme.Default));

    public VenueSyncApp(VenueSyncApiClient client, VenueSyncState state, Configuration configuration,
        GameData gameData)
    {
        this.client = client;
        this.state = state;
        this.configuration = configuration;
        this.gameData = gameData;
        router = new ViewRouter<VenueSyncRoute>(VenueSyncRoute.Dashboard);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        state.EnsureShiftsFresh(false);
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        ui.Palette = AppPalettes.VenueSync(theme);
        state.EnsureShiftsFresh(false);
        router.Draw(context.Content, theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(VenueSyncRoute route, Rect area, int depth)
    {
        switch (route)
        {
            case VenueSyncRoute.Dashboard:
                DrawDashboard(area);
                break;
            case VenueSyncRoute.Shifts:
                DrawShifts(area);
                break;
            case VenueSyncRoute.Sales:
                DrawSales(area);
                break;
            case VenueSyncRoute.Settings:
                DrawSettings(area);
                break;
            default:
                break;
        }
    }

    // VenueSyncState/VenueSyncApiClient are constructed and owned by the app registry/services
    // layer, not by this app, so there is nothing for this app to dispose.
    public void Dispose()
    {
    }

    // Shared field chrome for row-based InputText fields (Sales/Settings), matching AppSkin.Field's
    // own painting: a Metrics.Radius.Field squircle in FieldSurface, transparent ImGui frame so the
    // stock frame background doesn't show through, and TitleInk text.
    private bool DrawStyledField(Rect rect, string imguiId, string hint, ref string value, int maxLength,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Field * scale, ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + Metrics.Space.Md * scale,
            rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Width - Metrics.Space.Md * 2f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            return ImGui.InputTextWithHint(imguiId, hint, ref value, maxLength, flags);
        }
    }
}
