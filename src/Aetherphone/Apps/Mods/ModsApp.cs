using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Mods;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Mods;

internal sealed partial class ModsApp : IPhoneApp
{
    private enum ModsTab : byte
    {
        Discover,
        Installed,
        Settings,
    }

    private enum ModsScreen : byte
    {
        Root,
        Detail,
        Setup,
    }

    private readonly record struct ModsView(ModsScreen Screen, Guid PackageId = default);

    private const double LibraryRefreshDelaySeconds = 6.0;
    private const double PluginCheckSeconds = 1.0;
    private const int NoPendingOutcome = -1;

    public string Id => ModsContent.AppId;
    public string DisplayName => Loc.T(L.Apps.Mods);
    public string Glyph => "M";
    public int BadgeCount => hub.Library.UpdateCount;

    private readonly ModsHub hub;
    private readonly RemoteImageCache images;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly SettingsSnapshotStore<ModsSnapshot> snapshotStore;
    private readonly ModsSnapshot snapshot;
    private readonly AppSkin ui = new(AppPalettes.Mods);
    private readonly ViewRouter<ModsView> router;
    private readonly RouterDraw<ModsView> drawView;
    private readonly Action back;
    private readonly BottomTabBar bottomNav = new();
    private readonly NavTab[] navTabs = new NavTab[3];
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly StoreWork work = new("mods");
    private ModsTab tab = ModsTab.Discover;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int pendingInstallOutcome = NoPendingOutcome;
    private bool pendingInstallPrompt;
    private bool snapshotDirty;
    private double libraryRefreshAt;
    private bool pluginLoaded;
    private double pluginCheckedAt = -PluginCheckSeconds;

    public ModsApp(ModsHub hub, RemoteImageCache images, Configuration configuration, ConfirmService confirm)
    {
        this.hub = hub;
        this.images = images;
        this.configuration = configuration;
        this.confirm = confirm;
        router = new ViewRouter<ModsView>(new ModsView(ModsScreen.Root));
        drawView = DrawView;
        back = () => router.Pop();
        snapshotStore = new SettingsSnapshotStore<ModsSnapshot>(configuration,
            static config => config.ModsSettings,
            static (config, snapshot) => config.ModsSettings = snapshot);
        snapshot = snapshotStore.Load() ?? new ModsSnapshot();
    }

    public void OnOpened()
    {
        router.Reset();
        EnsureCategories();
        EnsureSearch();
        hub.Library.Refresh(false);
    }

    public void OnClosed()
    {
        router.Reset();
        photoViewer.Close();
        sortMenu.Close();
        PersistSnapshot();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        sortMenu.Gate();
        var scale = UiScale.Current;
        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ui.Backdrop(screen);
        FlushInstallOutcome();
        if (libraryRefreshAt > 0d && ImGui.GetTime() >= libraryRefreshAt)
        {
            libraryRefreshAt = 0d;
            hub.Library.Refresh(true);
        }

        var stage = new Rect(content.Min,
            new Vector2(content.Max.X, content.Max.Y - BottomTabBar.LabelledHeight * scale));
        using (ImRaii.PushId((int)tab))
        {
            router.Draw(stage, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        DrawTabBar(new Rect(new Vector2(content.Min.X, stage.Max.Y), content.Max));
        DrawSortMenu(screen);
        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
        }
    }

    private void DrawView(ModsView view, Rect area, int depth)
    {
        ui.Body(area);
        switch (view.Screen)
        {
            case ModsScreen.Detail:
                DrawDetail(area, view.PackageId);
                break;
            case ModsScreen.Setup:
                DrawSetup(area);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        switch (tab)
        {
            case ModsTab.Installed:
                DrawInstalled(area);
                break;
            case ModsTab.Settings:
                DrawSettings(area);
                break;
            default:
                DrawDiscover(area);
                break;
        }
    }

    private void DrawTabBar(Rect bar)
    {
        navTabs[0] = new NavTab(FontAwesomeIcon.Compass, Loc.T(L.Mods.TabDiscover));
        navTabs[1] = new NavTab(FontAwesomeIcon.CheckCircle, Loc.T(L.Mods.TabInstalled), hub.Library.UpdateCount);
        navTabs[2] = new NavTab(FontAwesomeIcon.Cog, Loc.T(L.Mods.TabSettings));
        var tapped = bottomNav.Draw(bar, ui, theme, navTabs, (int)tab, true);
        if (tapped >= 0)
        {
            SelectTab((ModsTab)tapped);
        }
    }

    private void SelectTab(ModsTab wanted)
    {
        router.Reset();
        if (tab == wanted)
        {
            return;
        }

        if (tab == ModsTab.Settings)
        {
            PersistSnapshot();
        }

        tab = wanted;
        if (wanted == ModsTab.Installed)
        {
            hub.Library.Refresh(false);
        }
    }

    private void OpenDetail(Guid packageId)
    {
        ResetDetail(packageId);
        hub.Details.Request(packageId);
        router.Push(new ModsView(ModsScreen.Detail, packageId));
    }

    private void OpenSetup() => router.Push(new ModsView(ModsScreen.Setup));

    private bool PluginLoaded()
    {
        var now = ImGui.GetTime();
        if (now - pluginCheckedAt >= PluginCheckSeconds)
        {
            pluginCheckedAt = now;
            pluginLoaded = HeliosphereBridge.IsPluginLoaded();
        }

        return pluginLoaded;
    }

    private void StartInstall(Guid packageId, Guid variantId, Guid versionId)
    {
        if (!PluginLoaded())
        {
            OpenSetup();
            return;
        }

        var password = snapshot.OneClickPassword;
        pendingInstallPrompt = string.IsNullOrWhiteSpace(password);
        work.Run("install", async token =>
        {
            var outcome = await hub.Bridge.InstallAsync(packageId, variantId, versionId, password, token)
                .ConfigureAwait(false);
            Interlocked.Exchange(ref pendingInstallOutcome, (int)outcome);
        });
    }

    private void FlushInstallOutcome()
    {
        var outcome = Interlocked.Exchange(ref pendingInstallOutcome, NoPendingOutcome);
        if (outcome == NoPendingOutcome)
        {
            return;
        }

        switch ((InstallOutcome)outcome)
        {
            case InstallOutcome.Sent:
                ShellToast.Show(Loc.T(pendingInstallPrompt ? L.Mods.InstallPrompt : L.Mods.InstallSent));
                hub.Library.Invalidate();
                libraryRefreshAt = ImGui.GetTime() + LibraryRefreshDelaySeconds;
                break;
            case InstallOutcome.PluginNotRunning:
                pluginCheckedAt = -PluginCheckSeconds;
                ShellToast.Show(Loc.T(L.Mods.InstallPluginMissing));
                break;
            default:
                ShellToast.Show(Loc.T(L.Mods.InstallFailed));
                break;
        }
    }

    private void SnapshotChanged()
    {
        snapshotDirty = true;
        EnsureSearch();
    }

    private void PersistSnapshot()
    {
        if (!snapshotDirty)
        {
            return;
        }

        snapshotStore.Save(snapshot);
        snapshotDirty = false;
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
