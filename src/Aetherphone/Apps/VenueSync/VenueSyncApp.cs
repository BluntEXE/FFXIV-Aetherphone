using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;
using Aetherphone.Core.VenueSync;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp : IPhoneApp
{
    public string Id => "venue-sync";
    public string DisplayName => "Venue Sync";
    public string Glyph => "Vs";
    public int BadgeCount => 0;

    private readonly VenueSyncApiClient client;
    private readonly VenueSyncState state;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly ViewRouter<VenueSyncRoute> router;
    private readonly RouterDraw<VenueSyncRoute> drawView;

    // Populated at the top of Draw() and reused by sub-screens so they don't need to
    // reconstruct PhoneContext themselves — mirrors the pattern used by VenuesApp/FeedbackApp.
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    public VenueSyncApp(VenueSyncApiClient client, VenueSyncState state, Configuration configuration,
        GameData gameData)
    {
        this.client = client;
        this.state = state;
        this.configuration = configuration;
        this.gameData = gameData;
        router = new ViewRouter<VenueSyncRoute>(VenueSyncRoute.Dashboard);
        drawView = DrawView;
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
            default:
                // Shifts/Sales/Settings screens land in later tasks (Task 8+); nothing to draw yet.
                break;
        }
    }

    // VenueSyncState/VenueSyncApiClient are constructed and owned by the app registry/services
    // layer, not by this app, so there is nothing for this app to dispose.
    public void Dispose()
    {
    }
}
