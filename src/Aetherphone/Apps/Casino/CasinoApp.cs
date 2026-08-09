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
    private readonly Core.Casino.CasinoPlayStore casinoPlay;
    private readonly ConfirmService confirm;
    private readonly CashierDrawer cashier;
    private readonly Cabinets.SlotsCabinet slots;
    private readonly AppSkin ui = new(AppPalettes.Casino);
    private readonly ViewRouter<CasinoRoute> router;
    private readonly RouterDraw<CasinoRoute> drawView;
    private readonly Action popRoute;
    private readonly Action openLimits;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect screenArea;

    public CasinoApp(AethernetSession session, CoinStore coins, Core.Casino.CasinoStore casino,
        Core.Casino.CasinoPlayStore casinoPlay, ConfirmService confirm)
    {
        this.session = session;
        this.coins = coins;
        this.casino = casino;
        this.casinoPlay = casinoPlay;
        this.confirm = confirm;
        cashier = new CashierDrawer(casino, coins, confirm);
        slots = new Cabinets.SlotsCabinet(casino, casinoPlay, OpenCashier);
        router = new ViewRouter<CasinoRoute>(CasinoRoute.Floor);
        drawView = DrawView;
        popRoute = PopRoute;
        openLimits = OpenLimits;
    }

    public void OnOpened()
    {
        router.Reset();
        cashier.Close();
        slots.Reset();
        ResetLimitsEditor();
        coins.RefreshNow();
        casino.RefreshNow();
        casinoPlay.RecoverPendingRound();
    }

    public void OnClosed()
    {
        router.Reset();
        cashier.Close();
        slots.Reset();
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
        slots.Gate();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        slots.DrawOverlay(screenArea, ui);
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
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Slots, StringComparison.Ordinal):
                DrawSlotsHeader(context, area);
                slots.Draw(body, ui);
                break;
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

    private void DrawSlotsHeader(in PhoneContext context, Rect area)
    {
        var paysLabel = Loc.T(L.Casino.SlotsPays);
        var reserve = AppSkin.HeaderActionWidth(paysLabel) + 18f * UiScale.Current;
        AppHeader.Draw(context, "casino.slotsHeader", Loc.T(L.Casino.GameSlots), reserve, popRoute);
        if (ui.HeaderAction(area, paysLabel, !slots.PayTableOpen))
        {
            slots.OpenPayTable();
        }
    }

    private void OpenCashier()
    {
        cashier.Open();
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
        slots.ClosePayTable();
        router.Pop();
    }

    internal void OpenGame(string gameId)
    {
        if (string.Equals(gameId, CasinoGames.Slots, StringComparison.Ordinal))
        {
            var sitting = casino.State?.Sitting;
            var seated = sitting is not null
                && string.Equals(sitting.GameKind, Core.Casino.CasinoWire.SlotsKind, StringComparison.Ordinal);
            if (!seated)
            {
                cashier.Open(GameIndexOf(gameId));
                return;
            }

            slots.Enter();
        }

        router.Push(new CasinoRoute(CasinoScreen.Cabinet, gameId));
    }

    private static int GameIndexOf(string gameId)
    {
        for (var index = 0; index < PlayableGames.Length; index++)
        {
            if (string.Equals(PlayableGames[index].GameId, gameId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
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
