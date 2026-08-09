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
        new(CasinoGames.Blackjack, L.Casino.GameBlackjack),
        new(CasinoGames.Slots, L.Casino.GameSlots),
        new(CasinoGames.Scratch, L.Casino.GameScratch),
        new(CasinoGames.Barkeep, L.Casino.GameBarkeep),
        new(CasinoGames.Wheel, L.Casino.GameWheel),
        new(CasinoGames.Bingo, L.Casino.GameBingo),
    };

    private readonly AethernetSession session;
    private readonly CoinStore coins;
    private readonly Core.Casino.CasinoStore casino;
    private readonly Core.Casino.CasinoPlayStore casinoPlay;
    private readonly Core.Casino.CasinoHistoryStore history;
    private readonly Core.Casino.CasinoRoomsStore casinoRooms;
    private readonly Core.Casino.CasinoTablesStore casinoTables;
    private readonly Core.Casino.CasinoSpinStore casinoSpin;
    private readonly Core.Casino.CasinoLauncher launcher;
    private readonly ConfirmService confirm;
    private readonly CashierDrawer cashier;
    private readonly Cabinets.SlotsCabinet slots;
    private readonly Cabinets.ScratchCabinet scratch;
    private readonly Cabinets.BarkeepCabinet barkeep;
    private readonly Cabinets.WheelCabinet wheel;
    private readonly Cabinets.BingoCabinet bingo;
    private readonly Cabinets.DailySpinCabinet dailySpin;
    private readonly Tables.BlackjackTable blackjack;
    private readonly Tables.TableBrowser browser;
    private readonly Tables.TableDoor tableDoor;
    private readonly AppSkin ui = new(AppPalettes.Casino);
    private readonly ViewRouter<CasinoRoute> router;
    private readonly RouterDraw<CasinoRoute> drawView;
    private readonly Action popRoute;
    private readonly Action openLimits;
    private readonly Action<string> openTable;
    private readonly Action<string> openDoorFromRow;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect screenArea;
    private string pendingTableId = string.Empty;

    public CasinoApp(AethernetSession session, CoinStore coins, Core.Casino.CasinoStore casino,
        Core.Casino.CasinoPlayStore casinoPlay, Core.Casino.CasinoHistoryStore history,
        Core.Casino.CasinoRoomsStore casinoRooms, Core.Casino.CasinoTablesStore casinoTables,
        Core.Casino.CasinoSpinStore casinoSpin, Core.Casino.CasinoTurnNotifier casinoTurns,
        Core.Casino.CasinoLauncher launcher, Core.Games.GameStatsStore gameStats, ConfirmService confirm)
    {
        this.session = session;
        this.coins = coins;
        this.casino = casino;
        this.casinoPlay = casinoPlay;
        this.history = history;
        this.casinoRooms = casinoRooms;
        this.casinoTables = casinoTables;
        this.casinoSpin = casinoSpin;
        this.launcher = launcher;
        this.confirm = confirm;
        cashier = new CashierDrawer(casino, coins, confirm);
        slots = new Cabinets.SlotsCabinet(casino, casinoPlay, OpenCashier);
        scratch = new Cabinets.ScratchCabinet(casino, casinoPlay, OpenCashier);
        barkeep = new Cabinets.BarkeepCabinet(casino, casinoPlay, gameStats, OpenCashier);
        wheel = new Cabinets.WheelCabinet(casino, casinoRooms, OpenCashier, PopRoute);
        bingo = new Cabinets.BingoCabinet(casino, casinoRooms, OpenCashier, PopRoute);
        dailySpin = new Cabinets.DailySpinCabinet(casinoSpin);
        blackjack = new Tables.BlackjackTable(casino, casinoRooms, casinoTables, casinoTurns, OpenCashier, PopRoute);
        openTable = OpenTable;
        openDoorFromRow = OpenDoor;
        browser = new Tables.TableBrowser(casinoTables, openTable, openDoorFromRow);
        tableDoor = new Tables.TableDoor(casinoTables, confirm, openTable);
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
        scratch.Reset();
        barkeep.Reset();
        wheel.Reset();
        bingo.Reset();
        dailySpin.Reset();
        blackjack.Reset();
        browser.Reset();
        tableDoor.Reset();
        pendingTableId = string.Empty;
        ResetLimitsEditor();
        historyLoadFailed = false;
        history.Invalidate();
        coins.RefreshNow();
        casino.RefreshNow();
        casinoRooms.RefreshNow();
        casinoTables.RefreshNow();
        casinoPlay.RecoverPendingRound();
        ConsumeLaunch();
    }

    public void OnClosed()
    {
        router.Reset();
        cashier.Close();
        slots.Reset();
        scratch.Reset();
        barkeep.Reset();
        wheel.Reset();
        bingo.Reset();
        dailySpin.Reset();
        blackjack.Reset();
        browser.Reset();
        tableDoor.Reset();
        pendingTableId = string.Empty;
        ResetLimitsEditor();
    }

    // Opening an app that is already open re-fires this, which is exactly what a turn alert tapped
    // while the casino is on screen depends on: the deep link is consumed here or not at all.
    private void ConsumeLaunch()
    {
        if (!launcher.TryConsume(out var launch))
        {
            return;
        }

        if (launch.Kind == Core.Casino.CasinoLaunchKind.Table && launch.TableId.Length > 0)
        {
            OpenTable(launch.TableId);
            return;
        }

        if (launch.Kind == Core.Casino.CasinoLaunchKind.Tables)
        {
            OpenTables();
        }
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
        casinoRooms.EnsureFresh();
        casinoTables.EnsureFresh();
        ConsumeTableAnswers();
        screenArea = context.Content;
        barkeep.Tick();
        cashier.Gate();
        slots.Gate();
        scratch.Gate();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        slots.DrawOverlay(screenArea, ui);
        scratch.DrawOverlay(screenArea, ui);
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
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Scratch, StringComparison.Ordinal):
                DrawScratchHeader(context, area);
                scratch.Draw(body, ui);
                break;
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Barkeep, StringComparison.Ordinal):
                AppHeader.Draw(context, Loc.T(L.Casino.GameBarkeep), popRoute);
                barkeep.Draw(body, ui);
                break;
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Wheel, StringComparison.Ordinal):
                AppHeader.Draw(context, Loc.T(L.Casino.GameWheel), popRoute);
                wheel.Draw(body, ui);
                break;
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Bingo, StringComparison.Ordinal):
                AppHeader.Draw(context, Loc.T(L.Casino.GameBingo), popRoute);
                bingo.Draw(body, ui);
                break;
            case CasinoScreen.Tables:
                AppHeader.Draw(context, Loc.T(L.Casino.TablesTitle), popRoute);
                browser.Draw(body, ui);
                break;
            case CasinoScreen.TableDoor:
                AppHeader.Draw(context, Loc.T(L.Casino.DoorTitle), popRoute);
                tableDoor.Draw(body, ui);
                break;
            case CasinoScreen.Table:
                AppHeader.Draw(context, Loc.T(L.Casino.GameBlackjack), popRoute);
                blackjack.Draw(body, ui);
                break;
            case CasinoScreen.DailySpin:
                AppHeader.Draw(context, Loc.T(L.Casino.GameDailySpin), popRoute);
                dailySpin.Draw(body, ui);
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
                AppHeader.Draw(context, Loc.T(L.Casino.HistoryRow), popRoute);
                DrawHistory(body);
                break;
            case CasinoScreen.Fairness:
                AppHeader.Draw(context, Loc.T(L.Casino.FairnessRow), popRoute);
                DrawFairness(body);
                break;
            case CasinoScreen.RoundDetail:
                AppHeader.Draw(context, Loc.T(L.Casino.RoundDetailTitle), popRoute);
                DrawRoundDetail(body, route.RoundId);
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

    private void DrawScratchHeader(in PhoneContext context, Rect area)
    {
        var oddsLabel = Loc.T(L.Casino.ScratchOdds);
        var reserve = AppSkin.HeaderActionWidth(oddsLabel) + 18f * UiScale.Current;
        AppHeader.Draw(context, "casino.scratchHeader", Loc.T(L.Casino.GameScratch), reserve, popRoute);
        if (ui.HeaderAction(area, oddsLabel, !scratch.OddsOpen))
        {
            scratch.OpenOdds();
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
        scratch.CloseOdds();
        wheel.Reset();
        bingo.Reset();
        dailySpin.Reset();
        blackjack.Reset();
        router.Pop();
    }

    private void OpenTables()
    {
        if (router.Current.Screen == CasinoScreen.Tables)
        {
            return;
        }

        browser.Enter();
        router.Push(new CasinoRoute(CasinoScreen.Tables, CasinoGames.Blackjack));
    }

    private void OpenTable(string tableId)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        var current = router.Current;
        if (current.Screen == CasinoScreen.Table
            && string.Equals(current.TableId, tableId, StringComparison.Ordinal))
        {
            return;
        }

        blackjack.Enter(tableId);
        router.Push(new CasinoRoute(CasinoScreen.Table, CasinoGames.Blackjack, string.Empty, tableId));
    }

    private void OpenDoor(string tableId)
    {
        OpenDoor(tableId, string.Empty);
    }

    private void OpenDoor(string tableId, string inviteToken)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        tableDoor.Enter(tableId, inviteToken);
        if (router.Current.Screen == CasinoScreen.TableDoor)
        {
            return;
        }

        router.Push(new CasinoRoute(CasinoScreen.TableDoor, CasinoGames.Blackjack, string.Empty, tableId));
    }

    // The quick seat answer is the pivot of the whole three tap path: it names the table, the buy-in
    // fills the cashier, and the table is entered the moment the chips actually exist. Entering
    // before the buy-in lands would seat a player at a felt with nothing to bet.
    private void ConsumeTableAnswers()
    {
        var quick = casinoTables.TakeQuickSeat();
        if (quick is not null)
        {
            pendingTableId = quick.RoomId;
            if (!SeatedAt(Core.Casino.CasinoWire.BlackjackKind))
            {
                cashier.Open(GameIndexOf(CasinoGames.Blackjack), quick.SuggestedBuyIn);
            }
        }

        var hosted = casinoTables.TakeHostedTable();
        if (hosted is not null)
        {
            OpenDoor(hosted.RoomId, hosted.InviteToken);
        }

        var resolved = casinoTables.TakeResolvedTable();
        if (resolved is not null)
        {
            if (resolved.Owner)
            {
                OpenDoor(resolved.RoomId, resolved.InviteToken);
            }
            else
            {
                OpenTable(resolved.RoomId);
            }
        }

        if (pendingTableId.Length == 0 || !SeatedAt(Core.Casino.CasinoWire.BlackjackKind))
        {
            return;
        }

        var target = pendingTableId;
        pendingTableId = string.Empty;
        cashier.Close();
        OpenTable(target);
    }

    private void OpenDailySpin()
    {
        if (router.Current.Screen == CasinoScreen.DailySpin)
        {
            return;
        }

        dailySpin.Enter();
        router.Push(new CasinoRoute(CasinoScreen.DailySpin));
    }

    internal void OpenGame(string gameId)
    {
        if (string.Equals(gameId, CasinoGames.Blackjack, StringComparison.Ordinal))
        {
            // The tile never picks a table itself. Asking the server which felt has room is what
            // keeps five phones from racing onto the same open seat off one stale directory page.
            casinoTables.QuickSeat(Core.Casino.CasinoStakeTiers.Any);
            return;
        }

        if (string.Equals(gameId, CasinoGames.Slots, StringComparison.Ordinal))
        {
            if (!SeatedAt(Core.Casino.CasinoWire.SlotsKind))
            {
                cashier.Open(GameIndexOf(gameId));
                return;
            }

            slots.Enter();
        }
        else if (string.Equals(gameId, CasinoGames.Scratch, StringComparison.Ordinal))
        {
            if (!SeatedAt(Core.Casino.CasinoWire.ScratchKind))
            {
                cashier.Open(GameIndexOf(gameId));
                return;
            }

            scratch.Enter();
        }
        else if (string.Equals(gameId, CasinoGames.Barkeep, StringComparison.Ordinal))
        {
            barkeep.Enter();
        }
        else if (string.Equals(gameId, CasinoGames.Wheel, StringComparison.Ordinal))
        {
            if (!SeatedAt(Core.Casino.CasinoWire.WheelKind))
            {
                cashier.Open(GameIndexOf(gameId));
                return;
            }

            wheel.Enter();
        }
        else if (string.Equals(gameId, CasinoGames.Bingo, StringComparison.Ordinal))
        {
            if (!SeatedAt(Core.Casino.CasinoWire.BingoKind))
            {
                cashier.Open(GameIndexOf(gameId));
                return;
            }

            bingo.Enter();
        }

        router.Push(new CasinoRoute(CasinoScreen.Cabinet, gameId));
    }

    private bool SeatedAt(string wireKind)
    {
        var sitting = casino.State?.Sitting;
        return sitting is not null && string.Equals(sitting.GameKind, wireKind, StringComparison.Ordinal);
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
        CasinoGames.DailySpin => L.Casino.GameDailySpin,
        _ => L.Apps.Casino,
    };

    public void Dispose()
    {
    }
}
