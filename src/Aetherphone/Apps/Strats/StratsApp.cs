using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Config;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp : IPhoneApp
{
    private enum StratsScreen : byte
    {
        Index,
        Fight,
        Viewer,
    }

    private readonly record struct StratsView(StratsScreen Screen, string FightKey = "", int PhaseIndex = -1,
        int MechIndex = -1, bool PlayerImage = false);

    public string Id => StratsContent.AppId;
    public string DisplayName => Loc.T(L.Apps.Strats);
    public string Glyph => "St";
    public int BadgeCount => 0;

    private readonly StratsManifestStore manifestStore;
    private readonly StratsGuideStore guideStore;
    private readonly RemoteImageCache images;
    private readonly Configuration configuration;
    private readonly SettingsSnapshotStore<StratsSnapshot> snapshotStore;
    private readonly StratsSnapshot snapshot;
    private readonly StratsSelection selection = new();
    private readonly AppSkin ui = new(AppPalettes.Strats);
    private readonly ViewRouter<StratsView> router;
    private readonly RouterDraw<StratsView> drawView;
    private readonly Action back;
    private readonly Action closeViewer;
    private readonly RichTextBlock richText = new();
    private readonly PhotoZoomView zoom = new();
    private readonly ChipRail tabRail = new();
    private readonly Dictionary<string, ChipRail> toggleRails = new(StringComparer.Ordinal);
    private ResolvedFight? resolved;
    private FightDoc? resolvedDoc;
    private bool timelineOpen;
    private bool linksOpen;
    private bool selectionDirty;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    public StratsApp(StratsManifestStore manifestStore, StratsGuideStore guideStore, RemoteImageCache images,
        Configuration configuration)
    {
        this.manifestStore = manifestStore;
        this.guideStore = guideStore;
        this.images = images;
        this.configuration = configuration;
        router = new ViewRouter<StratsView>(new StratsView(StratsScreen.Index));
        drawView = DrawView;
        back = () => router.Pop();
        closeViewer = CloseViewer;
        snapshotStore = new SettingsSnapshotStore<StratsSnapshot>(configuration,
            static config => config.StratsSettings,
            static (config, snapshot) => config.StratsSettings = snapshot);
        snapshot = snapshotStore.Load() ?? new StratsSnapshot();
    }

    public void OnOpened()
    {
        router.Reset();
        manifestStore.EnsureFresh(false);
    }

    public void OnClosed()
    {
        PersistSelection();
        AppLandscape.Release(Id);
        router.Reset();
        zoom.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        manifestStore.EnsureFresh(false);
        if (router.Current.Screen != StratsScreen.Viewer)
        {
            AppLandscape.Release(Id);
        }

        var screen = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(StratsView view, Rect area, int depth)
    {
        ui.Body(area);
        switch (view.Screen)
        {
            case StratsScreen.Fight:
                DrawFight(area, view);
                break;
            case StratsScreen.Viewer:
                DrawViewer(area, view);
                break;
            default:
                DrawIndex(area);
                break;
        }
    }

    private void OpenFight(ManifestFight fight)
    {
        PersistSelection();
        snapshot.Fights.TryGetValue(fight.Key, out var saved);
        selection.Load(fight.Key, saved, snapshot.DefaultSlot);
        resolved = null;
        resolvedDoc = null;
        timelineOpen = false;
        linksOpen = false;
        router.Push(new StratsView(StratsScreen.Fight, fight.Key));
    }

    private void OpenViewer(StratsView view)
    {
        zoom.Reset();
        router.Push(view);
        AppLandscape.Request(Id);
    }

    private void CloseViewer()
    {
        AppLandscape.Release(Id);
        router.Pop();
    }

    private void PersistSelection()
    {
        if (!selectionDirty || selection.FightKey.Length == 0)
        {
            return;
        }

        snapshot.Fights[selection.FightKey] = selection.Capture();
        snapshot.DefaultSlot = selection.Slot;
        snapshotStore.Save(snapshot);
        selectionDirty = false;
    }

    private void TouchSelection()
    {
        selection.Touch();
        selectionDirty = true;
    }

    private ResolvedFight? ResolveCurrent(FightDoc doc)
    {
        if (resolved is not null && ReferenceEquals(resolvedDoc, doc) && resolved.Revision == selection.Revision)
        {
            return resolved;
        }

        resolved = StratsResolver.Build(doc, selection);
        resolvedDoc = doc;
        richText.Clear();
        return resolved;
    }

    public void Dispose()
    {
    }
}
