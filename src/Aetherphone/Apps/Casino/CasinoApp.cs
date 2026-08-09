using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp : IPhoneApp
{
    public string Id => "casino";
    public string DisplayName => Loc.T(L.Apps.Casino);
    public string Glyph => "Sa";
    public int BadgeCount => 0;

    private readonly AethernetSession session;
    private readonly CoinStore coins;
    private readonly AppSkin ui = new(AppPalettes.Casino);
    private readonly ViewRouter<CasinoRoute> router;
    private readonly RouterDraw<CasinoRoute> drawView;
    private readonly Action popRoute;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    public CasinoApp(AethernetSession session, CoinStore coins)
    {
        this.session = session;
        this.coins = coins;
        router = new ViewRouter<CasinoRoute>(CasinoRoute.Floor);
        drawView = DrawView;
        popRoute = PopRoute;
    }

    public void OnOpened()
    {
        router.Reset();
        coins.RefreshNow();
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

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);

        if (!session.IsSignedIn)
        {
            TourHolds.Hold(Id);
            ui.Body(context.Content);
            AppHeader.Draw(context, DisplayName, navigation.Back);
            var top = context.Content.Min.Y + AppHeader.Height * scale;
            var body = new Rect(new Vector2(context.Content.Min.X, top), context.Content.Max);
            EmptyState.Draw(body, ui, FontAwesomeIcon.UserLock, Loc.T(L.Casino.SignInTitle),
                Loc.T(L.Casino.SignInHint));
            return;
        }

        TourHolds.Release(Id);
        coins.EnsureFresh();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(CasinoRoute route, Rect area, int depth)
    {
        ui.Body(area);
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        switch (route.Screen)
        {
            case CasinoScreen.Cabinet:
                AppHeader.Draw(context, Loc.T(GameName(route.GameId)), popRoute);
                DrawPlaceholder(body, FontAwesomeIcon.Hammer);
                break;
            case CasinoScreen.Limits:
                AppHeader.Draw(context, Loc.T(L.Casino.LimitsRow), popRoute);
                DrawPlaceholder(body, FontAwesomeIcon.HandHoldingHeart);
                break;
            case CasinoScreen.History:
            case CasinoScreen.Fairness:
                AppHeader.Draw(context, DisplayName, popRoute);
                DrawPlaceholder(body, FontAwesomeIcon.Hammer);
                break;
            default:
                AppHeader.Draw(context, DisplayName, navigation.Back);
                DrawFloor(body);
                break;
        }
    }

    private void DrawPlaceholder(Rect body, FontAwesomeIcon icon)
    {
        EmptyState.Draw(body, ui, icon, Loc.T(L.Casino.CabinetSoonTitle), Loc.T(L.Casino.CabinetSoonHint));
    }

    private void PopRoute()
    {
        router.Pop();
    }

    private static LocString GameName(string gameId) => gameId switch
    {
        CasinoGames.Blackjack => L.Casino.GameBlackjack,
        CasinoGames.Holdem => L.Casino.GameHoldem,
        CasinoGames.Slots => L.Casino.GameSlots,
        CasinoGames.Scratch => L.Casino.GameScratch,
        CasinoGames.Bingo => L.Casino.GameBingo,
        CasinoGames.Wheel => L.Casino.GameWheel,
        CasinoGames.Barkeep => L.Casino.GameBarkeep,
        _ => L.Apps.Casino,
    };

    public void Dispose()
    {
    }
}
