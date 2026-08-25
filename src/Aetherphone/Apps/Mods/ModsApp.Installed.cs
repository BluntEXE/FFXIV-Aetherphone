using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Mods;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Mods;

internal sealed partial class ModsApp
{
    private void DrawInstalled(Rect area)
    {
        var scale = UiScale.Current;
        var library = hub.Library;
        library.Refresh(false);
        DrawInstalledHeader(area, library, scale);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var installed = library.Installed;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            if (installed.Length == 0)
            {
                DrawInstalledState(body, library, scale);
            }
            else
            {
                DrawInstalledSections(installed, library, scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawInstalledHeader(Rect area, ModsLibraryStore library, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var inset = 16f * scale;
        var actionCenter = new Vector2(area.Max.X - inset - 11f * scale, rowCenterY);
        if (library.Scanning || library.CheckingUpdates)
        {
            LoadingPulse.Spinner(actionCenter, 8f * scale, ui.Accent);
        }
        else if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.Sync.ToIconString(), ui.MutedInk,
                     AppSkin.Transparent, 0.82f, Loc.T(L.Common.Refresh), HoverLabelSide.Below))
        {
            library.Refresh(true);
        }

        var title = Loc.T(L.Mods.TabInstalled);
        var maxWidth = MathF.Max(1f, area.Width - inset * 2f - 40f * scale);
        var titleY = rowCenterY - Typography.Measure(title, TextStyles.Title2).Y * 0.5f;
        Marquee.DrawLeftAuto(ImGui.GetWindowDrawList(), "mods.installed.title", title, area.Min.X + inset, titleY,
            maxWidth, TextStyles.Title2, ui.TitleInk);
    }

    private void DrawInstalledState(Rect body, ModsLibraryStore library, float scale)
    {
        switch (library.State)
        {
            case ModsLibraryState.NoPenumbra:
                if (EmptyState.Draw(body, ui, FontAwesomeIcon.Plug, Loc.T(L.Mods.PenumbraMissing),
                        Loc.T(L.Mods.PenumbraMissingHint), Loc.T(L.Mods.SetupTitle)))
                {
                    OpenSetup();
                }

                break;
            case ModsLibraryState.Failed:
                if (EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Mods.LibraryFailed),
                        Loc.T(L.Mods.LoadFailedHint), Loc.T(L.Mods.Retry)))
                {
                    library.Refresh(true);
                }

                break;
            case ModsLibraryState.Ready:
                if (EmptyState.Draw(body, ui, FontAwesomeIcon.Cube, Loc.T(L.Mods.NoneInstalled),
                        Loc.T(L.Mods.NoneInstalledHint), Loc.T(L.Mods.BrowseMods)))
                {
                    SelectTab(ModsTab.Discover);
                }

                break;
            default:
                LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 110f * scale), 13f * scale, ui.Accent,
                    ui.MutedInk, Loc.T(L.Mods.Scanning));
                break;
        }
    }

    private void DrawInstalledSections(InstalledMod[] installed, ModsLibraryStore library, float scale)
    {
        if (library.UpdateCount > 0)
        {
            ui.SectionHeading(Loc.T(L.Mods.Updates));
            for (var index = 0; index < installed.Length; index++)
            {
                if (installed[index].HasUpdate)
                {
                    DrawInstalledCard(installed[index], library, true, scale);
                }
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }
        else if (library.CheckingUpdates)
        {
            ui.HelpText(Loc.T(L.Mods.CheckingUpdates));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }

        ui.SectionHeading(Loc.T(L.Mods.InstalledSection));
        for (var index = 0; index < installed.Length; index++)
        {
            DrawInstalledCard(installed[index], library, false, scale);
        }

        if (library.CollectionName.Length > 0)
        {
            ui.HelpText(Loc.T(L.Mods.Collection, library.CollectionName));
        }
    }

    private void DrawInstalledCard(InstalledMod mod, ModsLibraryStore library, bool updateRow, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ModCard.Height * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (ImGui.IsRectVisible(card.Min, card.Max))
        {
            var cover = mod.HasCover ? images.GetKeyed(mod.CoverKey, mod.CoverLoader) : null;
            switch (ModCard.DrawInstalled(card, mod, cover, ui, theme, updateRow))
            {
                case InstalledCardResult.Open:
                    OpenDetail(mod.PackageId);
                    break;
                case InstalledCardResult.Toggled:
                    if (mod.Enabled is { } enabled && !library.SetEnabled(mod, !enabled))
                    {
                        CopyToast.Show(Loc.T(L.Mods.ToggleFailed));
                    }

                    break;
                case InstalledCardResult.Update:
                    StartInstall(mod.PackageId, mod.VariantId, mod.NewestVersionId);
                    break;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + ModCard.Gap * scale));
    }
}
