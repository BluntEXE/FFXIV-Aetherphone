using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal enum AetherStreamScreen : byte
{
    Main,
    Settings,
    Join,
}

internal sealed partial class AetherStreamApp : IPhoneApp
{
    private readonly VideoPlayer video;
    private readonly ScreenController screen;
    private readonly AetherStreamQueue queue;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly RemoteImageCache remoteImages;
    private readonly HttpService http;
    private readonly LodestoneService lodestone;
    private readonly WatchAlongSession watchAlong;
    private readonly AetherStreamScreenWindow screenWindow;
    private readonly AccountClient joinAccount;
    private readonly StoreWork joinWork = new("aetherstream.join");
    private readonly StoreWork dependencyWork = new("aetherstream.dependencies");
    private readonly AppSkin ui = new(AppPalettes.AetherStream);
    private readonly ViewRouter<AetherStreamScreen> router = new(AetherStreamScreen.Main);

    private readonly SheetSurface upNextSheet = new("aetherstream.upNext");
    private readonly SheetSurface partySheet = new("aetherstream.party");
    private readonly SheetSurface screenSheet = new("aetherstream.screen");

    private PhoneTheme theme = PhoneTheme.Default;
    private PhoneTheme accentedTheme = PhoneTheme.Default;

    internal AetherStreamApp(VideoPlayer video, ScreenController screen, AetherStreamQueue queue,
        Configuration configuration, ConfirmService confirm, RemoteImageCache remoteImages, HttpService http,
        AethernetSession aethernetSession, LodestoneService lodestone, WatchAlongSession watchAlong,
        AetherStreamScreenWindow screenWindow)
    {
        this.video = video;
        this.screen = screen;
        this.queue = queue;
        this.configuration = configuration;
        this.confirm = confirm;
        this.remoteImages = remoteImages;
        this.http = http;
        this.lodestone = lodestone;
        this.watchAlong = watchAlong;
        this.screenWindow = screenWindow;
        joinAccount = new AethernetApi(http, aethernetSession, "aetherstream").Account;
        video.SetVolume((int)(configuration.VideoVolume * 100));
        video.HardwareDecoding = configuration.VideoHardwareDecoding;
        video.AllowInsecureDirectUrls = configuration.VideoAllowInsecureDirectUrls;
        video.MaxQualityHeight = configuration.VideoMaxQualityHeight;
    }

    public string Id => "aetherstream";
    public string DisplayName => Loc.T(L.Apps.AetherStream);
    public string Glyph => "V";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => watchAlong.PendingRequests.Count + watchAlong.PendingQueueSuggestions.Count;

    public void OnOpened()
    {
        _ = screen.Engine.Dependencies.EnsureReadyAsync(CancellationToken.None);
        watchAlong.RequestNearbyStreams();
    }

    public void OnClosed()
    {
        router.Reset();
        upNextSheet.Close();
        partySheet.Close();
        screenSheet.Close();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        ui.Theme = context.Theme;
        accentedTheme = AccentedTheme(context.Theme);
        var scale = UiScale.Current;
        var screenRect = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screenRect);
        var localContext = context;
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, (screenState, area, _) =>
        {
            switch (screenState)
            {
                case AetherStreamScreen.Settings:
                    DrawSettings(localContext, area, scale);
                    return;
                case AetherStreamScreen.Join:
                    DrawJoinScreen(localContext, area, scale);
                    return;
                default:
                    DrawMain(localContext, area, scale);
                    return;
            }
        });
    }

    private void DrawMain(PhoneContext context, Rect area, float scale)
    {
        if (NeedsSetup)
        {
            DrawSetupGate(area, scale);
            return;
        }

        ui.Body(area);

        using (InputShield.Engage(SheetsCapturePointer))
        {
            DrawMainHeader(context, area, scale);
            var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
            DrawNowPlaying(body, scale);
        }

        DrawUpNextSheet(area, scale);
        DrawPartySheet(area, scale);
        DrawScreenSheet(area, scale);
    }

    private void DrawMainHeader(PhoneContext context, Rect area, float scale)
    {
        var areaContext = new PhoneContext(area, context.Theme, context.Navigation);
        AppHeader.Draw(areaContext, DisplayName);
        var radius = 13f * scale;
        var center = new Vector2(area.Max.X - Metrics.Space.Lg * scale - radius,
            area.Min.Y + AppHeader.Height * scale * 0.5f);
        if (ui.IconButton(center, radius, FontAwesomeIcon.Cog.ToIconString(), ui.TitleInk,
                Palette.WithAlpha(ui.TitleInk, 0.12f), 0.55f))
        {
            router.Push(AetherStreamScreen.Settings);
        }
    }

    private bool SheetsCapturePointer =>
        upNextSheet.CapturesPointer || partySheet.CapturesPointer || screenSheet.CapturesPointer;

    public void Dispose()
    {
        joinWork.Dispose();
        dependencyWork.Dispose();
    }
}
