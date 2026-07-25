using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp : IPhoneApp
{
    private const float CopiedSeconds = 1.6f;
    private const float BottomNavHeight = 52f;
    private const int NavSlotCount = 4;

    public string Id => "yellowpages";
    public string DisplayName => Loc.T(L.Apps.YellowPages);
    public string Glyph => "Yp";
    public int BadgeCount => socialNotifications.UnseenCount(Id);

    private readonly YellowPagesStore store;
    private readonly YellowPagesLauncher launcher;
    private readonly SocialNotificationService socialNotifications;
    private readonly GramDmLauncher gramDmLauncher;
    private readonly MusterStore musters;
    private readonly AethernetApi api;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly PhotoLibrary library;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly AppSkin ui = new(AppPalettes.YellowPages);
    private readonly ViewRouter<YellowPagesRoute> router;
    private readonly RouterDraw<YellowPagesRoute> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private YellowPagesTab activeTab = YellowPagesTab.Browse;
    private float copiedTimer;
    private string copiedKey = string.Empty;
    private bool lifestreamAvailable;

    public YellowPagesApp(YellowPagesStore store, YellowPagesLauncher launcher,
        SocialNotificationService socialNotifications, GramDmLauncher gramDmLauncher, MusterStore musters,
        AethernetApi api, GameData gameData, RemoteImageCache images, LodestoneService lodestone,
        PhotoLibrary library, WallpaperImageCache wallpaperImages, Configuration configuration,
        ConfirmService confirm, ReportService report)
    {
        this.store = store;
        this.launcher = launcher;
        this.socialNotifications = socialNotifications;
        this.gramDmLauncher = gramDmLauncher;
        this.musters = musters;
        this.api = api;
        this.gameData = gameData;
        this.images = images;
        this.lodestone = lodestone;
        this.library = library;
        this.wallpaperImages = wallpaperImages;
        this.configuration = configuration;
        this.confirm = confirm;
        this.report = report;
        router = new ViewRouter<YellowPagesRoute>(YellowPagesRoute.Browse);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = YellowPagesTab.Browse;
        socialNotifications.MarkSeen(Id);
        lifestreamAvailable = LifestreamBridge.IsAvailable();
        if (launcher.TryConsumeDetail(out var adId))
        {
            ResetDetailState();
            router.Push(YellowPagesRoute.Detail(adId), false);
        }

        store.SyncNow();
        RefreshBrowse();
    }

    public void OnClosed()
    {
        router.Reset();
        ResetDetailState();
        ResetComposeForm();
        copiedTimer = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);
        if (!store.IsSignedIn)
        {
            var rowCenterY = context.Content.Min.Y + AppHeader.Height * scale * 0.5f;
            Typography.DrawCentered(new Vector2(context.Content.Center.X, rowCenterY), DisplayName,
                AppPalettes.YellowPages.TitleInk, 1.3f, FontWeight.Bold);
            var body = new Rect(new Vector2(context.Content.Min.X, context.Content.Min.Y + AppHeader.Height * scale),
                context.Content.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.YellowPages.SetUpAccount),
                AppPalettes.YellowPages.MutedInk);
            return;
        }

        if (copiedTimer > 0f)
        {
            copiedTimer -= ImGui.GetIO().DeltaTime;
        }

        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (picked is not null)
        {
            AddComposePhoto(picked);
        }

        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
        }
    }

    private void DrawView(YellowPagesRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case YellowPagesScreen.Detail:
                DrawDetail(area, route.AdId!);
                break;
            case YellowPagesScreen.Compose:
                DrawCompose(area);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - BottomNavHeight * scale), area.Max);
        var tabArea = new Rect(area.Min, new Vector2(area.Max.X, navRect.Min.Y));
        switch (activeTab)
        {
            case YellowPagesTab.Saved:
                DrawSaved(tabArea);
                break;
            case YellowPagesTab.Mine:
                DrawMine(tabArea);
                break;
            default:
                DrawBrowse(tabArea);
                break;
        }

        DrawBottomNav(navRect);
    }

    private void DrawBottomNav(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f);
        var slot = bar.Width / NavSlotCount;
        var centerY = bar.Center.Y;
        DrawNavItem(new Vector2(bar.Min.X + slot * 0.5f, centerY), FontAwesomeIcon.ThLarge,
            Loc.T(L.YellowPages.BrowseTab), YellowPagesTab.Browse, scale);
        DrawNavItem(new Vector2(bar.Min.X + slot * 1.5f, centerY), FontAwesomeIcon.Heart,
            Loc.T(L.YellowPages.SavedTab), YellowPagesTab.Saved, scale);
        DrawNavPost(new Vector2(bar.Min.X + slot * 2.5f, centerY), scale);
        DrawNavItem(new Vector2(bar.Min.X + slot * 3.5f, centerY), FontAwesomeIcon.Bullhorn,
            Loc.T(L.YellowPages.MineTab), YellowPagesTab.Mine, scale);
    }

    private void DrawNavItem(Vector2 center, FontAwesomeIcon icon, string label, YellowPagesTab tab, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var active = activeTab == tab;
        var ink = active ? ui.Accent : AppPalettes.YellowPages.MutedInk;
        AppSkin.Icon(drawList, center - new Vector2(0f, 8f * scale), icon.ToIconString(), ink, 0.95f);
        Typography.DrawCentered(drawList, center + new Vector2(0f, 11f * scale), label, ink, 0.62f,
            active ? FontWeight.SemiBold : FontWeight.Medium);
        var half = new Vector2(26f * scale, 22f * scale);
        var hovered = UiInteract.Hover(center - half, center + half);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(center - half, center + half, hovered) || active)
        {
            return;
        }

        activeTab = tab;
        switch (tab)
        {
            case YellowPagesTab.Saved:
                store.RefreshSaved();
                break;
            case YellowPagesTab.Mine:
                store.SyncNow();
                break;
            default:
                RefreshBrowse();
                break;
        }
    }

    private void DrawNavPost(Vector2 center, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 17f * scale;
        var hovered = UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var fill = hovered ? Palette.Mix(ui.Accent, new Vector4(1f, 1f, 1f, 1f), 0.12f) : ui.Accent;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 40);
        AppSkin.Icon(drawList, center, FontAwesomeIcon.Plus.ToIconString(), new Vector4(0.11f, 0.08f, 0.02f, 1f),
            0.95f);
        HoverTooltip.Show(new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)),
            Loc.T(L.YellowPages.PostAd), HoverLabelSide.Above);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(center - new Vector2(radius, radius), center + new Vector2(radius, radius), hovered))
        {
            ResetComposeForm();
            router.Push(YellowPagesRoute.Compose);
        }
    }

    private void RefreshBrowse()
    {
        store.RefreshDirectory(configuration.YellowPagesCategoryFilter, browseOpenNow, browseSearch);
    }

    private void OpenDetail(string adId)
    {
        ResetDetailState();
        router.Push(YellowPagesRoute.Detail(adId));
    }

    private void Copy(string key, string text)
    {
        ImGui.SetClipboardText(text);
        copiedKey = key;
        copiedTimer = CopiedSeconds;
    }

    private bool JustCopied(string key) =>
        copiedTimer > 0f && string.Equals(copiedKey, key, StringComparison.Ordinal);

    private void SubmitReport(string adId, string? reason, Action<bool> done)
    {
        _ = Task.Run(async () =>
        {
            var ok = false;
            try
            {
                ok = await api.Safety.ReportAsync("ad", adId, reason, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[YellowPages] report failed: {exception.Message}");
            }

            done(ok);
        });
    }

    private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static int TrimmedLength(string value)
    {
        var start = 0;
        var end = value.Length - 1;
        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return end - start + 1;
    }

    public void Dispose()
    {
    }
}
