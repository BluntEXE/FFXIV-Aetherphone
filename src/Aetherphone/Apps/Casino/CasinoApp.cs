using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Confirm;
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

    private static readonly CashierDrawer.GameOption[] PlayableGames =
    {
        new(CasinoGames.Slots, L.Casino.GameSlots),
        new(CasinoGames.Scratch, L.Casino.GameScratch),
        new(CasinoGames.Barkeep, L.Casino.GameBarkeep),
    };

    private readonly AethernetSession session;
    private readonly CoinStore coins;
    private readonly Core.Casino.CasinoStore casino;
    private readonly ConfirmService confirm;
    private readonly CashierDrawer cashier;
    private readonly AppSkin ui = new(AppPalettes.Casino);
    private readonly ViewRouter<CasinoRoute> router;
    private readonly RouterDraw<CasinoRoute> drawView;
    private readonly Action popRoute;
    private readonly Action openLimits;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect screenArea;

    public CasinoApp(AethernetSession session, CoinStore coins, Core.Casino.CasinoStore casino,
        ConfirmService confirm)
    {
        this.session = session;
        this.coins = coins;
        this.casino = casino;
        this.confirm = confirm;
        cashier = new CashierDrawer(casino, coins, confirm);
        router = new ViewRouter<CasinoRoute>(CasinoRoute.Floor);
        drawView = DrawView;
        popRoute = PopRoute;
        openLimits = OpenLimits;
    }

    public void OnOpened()
    {
        router.Reset();
        cashier.Close();
        ResetLimitsEditor();
        coins.RefreshNow();
        casino.RefreshNow();
    }

    public void OnClosed()
    {
        router.Reset();
        cashier.Close();
        ResetLimitsEditor();
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
        casino.EnsureFresh();
        screenArea = context.Content;
        cashier.Gate();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        cashier.Draw(screenArea, ui, PlayableGames, openLimits);
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
                DrawLimits(body);
                break;
            case CasinoScreen.History:
            case CasinoScreen.Fairness:
                AppHeader.Draw(context, DisplayName, popRoute);
                DrawPlaceholder(body, FontAwesomeIcon.Hammer);
                break;
            default:
                DrawFloorHeader(context, area);
                DrawFloor(body);
                break;
        }
    }

    private void DrawFloorHeader(in PhoneContext context, Rect area)
    {
        var cashierLabel = Loc.T(L.Casino.Cashier);
        var reserve = AppSkin.HeaderActionWidth(cashierLabel) + 18f * UiScale.Current;
        AppHeader.Draw(context, "casino.header", DisplayName, reserve, navigation.Back);
        if (ui.HeaderAction(area, cashierLabel, !cashier.IsOpen))
        {
            cashier.Open();
        }
    }

    private void OpenLimits()
    {
        if (router.Current.Screen != CasinoScreen.Limits)
        {
            router.Push(new CasinoRoute(CasinoScreen.Limits));
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
