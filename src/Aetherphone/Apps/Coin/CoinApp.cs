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

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp : IPhoneApp
{
    private const int TabWallet = 0;
    private const int TabShop = 1;
    private const int TabHistory = 2;

    public string Id => "coin";
    public string DisplayName => Loc.T(L.Apps.Coin);
    public string Glyph => "Ac";
    public int BadgeCount => 0;

    private readonly AethernetSession session;
    private readonly CoinStore store;
    private readonly CoinCatalogStore catalog;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Coin);
    private readonly ViewRouter<CoinRoute> router;
    private readonly RouterDraw<CoinRoute> drawView;
    private readonly string[] tabOptions = new string[3];
    private readonly string[] filterOptions = new string[3];
    private readonly PullToRefresh walletRefresh = new();
    private readonly PullToRefresh historyRefresh = new();
    private readonly CoinFloat floats = new();

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int activeTab;
    private int historyFilter;

    public CoinApp(AethernetSession session, CoinStore store, CoinCatalogStore catalog, ConfirmService confirm)
    {
        this.session = session;
        this.store = store;
        this.catalog = catalog;
        this.confirm = confirm;
        router = new ViewRouter<CoinRoute>(CoinRoute.Root);
        drawView = DrawView;
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = TabWallet;
        historyFilter = CoinLedgerList.FilterAll;
        store.RefreshNow();
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
            EmptyState.Draw(body, ui, FontAwesomeIcon.UserLock, Loc.T(L.Coin.SignInTitle),
                Loc.T(L.Coin.SignInHint));
            return;
        }

        TourHolds.Release(Id);
        store.EnsureFresh();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(CoinRoute route, Rect area, int depth)
    {
        ui.Body(area);
        if (GuideIntents.Consume("coin.tab.shop"))
        {
            activeTab = TabShop;
        }

        var scale = UiScale.Current;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), DisplayName, navigation.Back);
        var helpCenter = new Vector2(area.Max.X - 26f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        if (ui.IconButton(helpCenter, 16f * scale, FontAwesomeIcon.QuestionCircle.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 1.1f, Loc.T(L.Coin.HelpTitle), HoverLabelSide.Below))
        {
            confirm.Alert(Loc.T(L.Coin.HelpTitle), Loc.T(L.Coin.HelpBody), Loc.T(L.Onboarding.GotIt));
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var inset = 16f * scale;
        var segRow = new Rect(
            new Vector2(area.Min.X + inset, top + 4f * scale),
            new Vector2(area.Max.X - inset, top + 34f * scale));
        UiAnchors.Report("coin.tabs", segRow);
        tabOptions[TabWallet] = Loc.T(L.Coin.TabWallet);
        tabOptions[TabShop] = Loc.T(L.Coin.TabShop);
        tabOptions[TabHistory] = Loc.T(L.Coin.TabHistory);
        activeTab = SegmentStrip.Draw("coin.tabs", segRow, tabOptions, activeTab, ui.Palette);

        var body = new Rect(new Vector2(area.Min.X, segRow.Max.Y + 10f * scale), area.Max);
        switch (activeTab)
        {
            case TabShop:
                DrawShop(body);
                break;
            case TabHistory:
                DrawHistory(body);
                break;
            default:
                DrawWallet(body);
                break;
        }

        floats.Draw(ImGui.GetWindowDrawList(), ui.Palette.Accent, ui.MutedInk, ImGui.GetIO().DeltaTime);
    }

    public void Dispose()
    {
    }
}
