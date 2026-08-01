using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp : IPhoneApp
{
    private enum ShortcutsScreen : byte
    {
        Home,
        Editor,
        Appearance,
        Plugin,
        PluginPicker,
    }

    private const float RowHeight = 62f;
    private const float FlashSeconds = 2.6f;

    public string Id => "shortcuts";
    public string DisplayName => Loc.T(L.Apps.Shortcuts);
    public string Glyph => "S";
    public int BadgeCount => 0;

    private readonly ShortcutStore store;
    private readonly ShortcutRunner runner;
    private readonly PluginCatalog catalog;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Shortcuts);
    private readonly ViewRouter<ShortcutsScreen> router;
    private readonly RouterDraw<ShortcutsScreen> drawView;
    private readonly Action back;
    private readonly string[] tabOptions = new string[2];

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int activeTab;
    private float flashClock;
    private ShortcutRunOutcome flashOutcome;
    private string flashName = string.Empty;

    public ShortcutsApp(ShortcutStore store, ShortcutRunner runner, ConfirmService confirm)
    {
        this.store = store;
        this.runner = runner;
        this.confirm = confirm;
        catalog = store.Catalog;
        router = new ViewRouter<ShortcutsScreen>(ShortcutsScreen.Home);
        drawView = DrawView;
        back = GoBack;
        runner.Finished += OnRunFinished;
    }

    public void OnOpened()
    {
        router.Reset();
        catalog.Invalidate();
        pluginQuery = string.Empty;
    }

    public void OnClosed()
    {
        router.Reset();
        draft = null;
    }

    private void OnRunFinished(Guid id, string name, ShortcutRunOutcome outcome)
    {
        if (outcome == ShortcutRunOutcome.Cancelled)
        {
            return;
        }

        flashOutcome = outcome;
        flashName = name;
        flashClock = FlashSeconds;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;

        var delta = ImGui.GetIO().DeltaTime;
        if (flashClock > 0f)
        {
            flashClock -= delta;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, delta, drawView);
    }

    private void DrawView(ShortcutsScreen screen, Rect area, int depth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ui.Body(area);
        switch (screen)
        {
            case ShortcutsScreen.Editor:
                DrawEditor(area, scale);
                return;
            case ShortcutsScreen.Appearance:
                DrawAppearance(area, scale);
                return;
            case ShortcutsScreen.Plugin:
                DrawPluginDetail(area, scale);
                return;
            case ShortcutsScreen.PluginPicker:
                DrawPluginPicker(area, scale);
                return;
            default:
                DrawHome(area, scale);
                return;
        }
    }

    private void GoBack() => router.Pop();

    private void DrawHome(Rect content, float scale)
    {
        DrawTopBar(content, scale);

        var margin = Metrics.Space.Lg * scale;
        var segTop = content.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var segRow = new Rect(new Vector2(content.Min.X + margin, segTop),
            new Vector2(content.Max.X - margin, segTop + 30f * scale));
        tabOptions[0] = Loc.T(L.Shortcuts.TabShortcuts);
        tabOptions[1] = Loc.T(L.Shortcuts.TabPlugins);
        activeTab = SegmentStrip.Draw("shortcuts.tabs", segRow, tabOptions, activeTab, theme);

        var bodyTop = segRow.Max.Y + Metrics.Space.Sm * scale;
        if (activeTab == 1)
        {
            DrawPluginsTab(content, bodyTop, scale);
            return;
        }

        var body = new Rect(new Vector2(content.Min.X, bodyTop), content.Max);
        using (AppSurface.Begin(body))
        {
            DrawFlash(scale);
            DrawLibrary(body, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawTopBar(Rect content, float scale)
    {
        var centerY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, centerY), DisplayName, ui.TitleInk, 1.15f,
            FontWeight.SemiBold);
        if (activeTab != 0)
        {
            return;
        }

        var radius = 15f * scale;
        var buttonCenter = new Vector2(content.Max.X - Metrics.Space.Lg * scale - radius, centerY);
        if (ui.IconButton(buttonCenter, radius, FontAwesomeIcon.Plus.ToIconString(), ui.TitleInk,
                Palette.WithAlpha(ui.TitleInk, 0.12f), 0.6f, Loc.T(L.Shortcuts.NewShortcut)))
        {
            StartNewShortcut();
        }
    }

    private void DrawFlash(float scale)
    {
        if (flashClock <= 0f)
        {
            return;
        }

        var alpha = Math.Clamp(flashClock / 0.5f, 0f, 1f);
        var success = flashOutcome == ShortcutRunOutcome.Completed;
        var tint = success ? ui.Accent : theme.Danger;
        var ranName = flashName.Length > 0 ? flashName : Loc.T(L.Shortcuts.Untitled);
        var label = flashOutcome switch
        {
            ShortcutRunOutcome.Completed => Loc.T(L.Shortcuts.RunDone, ranName),
            ShortcutRunOutcome.PluginUnavailable => Loc.T(L.Shortcuts.RunPluginMissing),
            ShortcutRunOutcome.LinkRejected => Loc.T(L.Shortcuts.RunLinkRejected),
            _ => Loc.T(L.Shortcuts.RunRejected),
        };

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.20f * alpha)));
        var icon = success ? FontAwesomeIcon.Check : FontAwesomeIcon.ExclamationCircle;
        AppSkin.Icon(new Vector2(min.X + 18f * scale, min.Y + height * 0.5f), icon.ToIconString(),
            Palette.WithAlpha(tint, alpha), 0.8f);
        var textLeft = min.X + 34f * scale;
        var fitted = Typography.FitText(label, max.X - textLeft - 12f * scale, TextStyles.Footnote.Scale,
            TextStyles.Footnote.Weight);
        var textSize = Typography.Measure(fitted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + height * 0.5f - textSize.Y * 0.5f), fitted,
            Palette.WithAlpha(ui.TitleInk, alpha), TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawLibrary(Rect body, float scale)
    {
        var shortcuts = store.All;
        if (shortcuts.Count == 0)
        {
            DrawEmptyLibrary(body, scale);
            return;
        }

        var card = GroupCard.Begin(theme, shortcuts.Count, RowHeight);
        for (var index = 0; index < shortcuts.Count; index++)
        {
            DrawShortcutRow(card.NextRow(), shortcuts[index], scale);
        }

        card.End();
    }

    private void DrawEmptyLibrary(Rect body, float scale)
    {
        var center = new Vector2(body.Center.X, body.Min.Y + 76f * scale);
        AppSkin.Icon(new Vector2(center.X, center.Y - 22f * scale), FontAwesomeIcon.Bolt.ToIconString(),
            Palette.WithAlpha(ui.MutedInk, 0.7f), 1.6f);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 12f * scale), Loc.T(L.Shortcuts.LibraryEmpty),
            ui.TitleInk, TextStyles.Headline);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 34f * scale), Loc.T(L.Shortcuts.LibraryEmptyHint),
            ui.MutedInk, TextStyles.Footnote);
    }

    private void DrawShortcutRow(Rect row, ShortcutEntry entry, float scale)
    {
        var tile = 38f * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        ShortcutArt.DrawSurface(ImGui.GetWindowDrawList(), tileCenter, tile, entry, store.Icon(entry), scale);

        var editRadius = 15f * scale;
        var editCenter = new Vector2(row.Max.X - editRadius, row.Center.Y);
        var textLeft = row.Min.X + tile + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, editCenter.X - editRadius - 8f * scale - textLeft);

        var running = runner.IsRunning && runner.RunningId == entry.Id;
        var name = entry.Name.Length > 0 ? entry.Name : Loc.T(L.Shortcuts.Untitled);
        Marquee.DrawLeftAuto("shortcuts.row." + entry.Id, name, textLeft, row.Center.Y - 16f * scale, textWidth,
            TextStyles.Headline, ui.TitleInk);
        var subtitle = running ? Loc.T(L.Shortcuts.Running) : Summarise(entry);
        Marquee.DrawLeftAuto("shortcuts.row.sub." + entry.Id, subtitle, textLeft, row.Center.Y + 5f * scale, textWidth,
            TextStyles.Footnote, running ? ui.Accent : ui.MutedInk);

        if (ui.IconButton(editCenter, editRadius, FontAwesomeIcon.SlidersH.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 0.62f, Loc.T(L.Shortcuts.Edit)))
        {
            StartEditShortcut(entry);
            return;
        }

        var tapMax = new Vector2(editCenter.X - editRadius, row.Max.Y);
        if (UiInteract.HoverClick(row.Min, tapMax))
        {
            runner.Run(entry);
        }
    }

    private string Summarise(ShortcutEntry entry)
    {
        if (entry.Steps.Count == 0)
        {
            return Loc.T(L.Shortcuts.NoSteps);
        }

        var first = entry.Steps[0];
        var lead = first.Kind switch
        {
            ShortcutStepKind.OpenPlugin => Loc.T(L.Shortcuts.StepOpenNamed, catalog.DisplayName(first.Text)),
            ShortcutStepKind.OpenUrl => Loc.T(L.Shortcuts.StepOpenNamed, ShortcutCommandText.HostOf(first.Text)),
            ShortcutStepKind.Wait => Loc.T(L.Shortcuts.StepWaitNamed, Seconds(first.Seconds)),
            _ => first.Text,
        };

        if (entry.Steps.Count == 1)
        {
            return lead;
        }

        return string.Concat(lead, "  ", Loc.T(L.Shortcuts.MoreSteps, entry.Steps.Count - 1));
    }

    private static string Seconds(float value) => value.ToString("0.#", Loc.Culture);

    public void Dispose()
    {
        runner.Finished -= OnRunFinished;
    }
}
