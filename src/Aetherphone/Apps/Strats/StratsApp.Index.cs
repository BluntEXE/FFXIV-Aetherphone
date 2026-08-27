using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Strats;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp
{
    private const float CreditsRowHeight = 32f;

    private void DrawIndex(Rect area)
    {
        var scale = UiScale.Current;
        DrawIndexHeader(area, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        UiAnchors.Report("strats.fights", body);
        var manifest = manifestStore.Manifest;
        if (manifest is null)
        {
            DrawIndexState(body, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            for (var groupIndex = 0; groupIndex < manifest.Groups.Length; groupIndex++)
            {
                var group = manifest.Groups[groupIndex];
                if (group.Fights.Length == 0)
                {
                    continue;
                }

                SettingsSection.Header(group.Title, theme);
                var card = GroupCard.Begin(theme, group.Fights.Length);
                for (var fightIndex = 0; fightIndex < group.Fights.Length; fightIndex++)
                {
                    var fight = group.Fights[fightIndex];
                    var row = card.NextRow();
                    if (SettingsRow.Disclosure(row, fight.Title, fight.Subtitle, theme, fight.Key))
                    {
                        OpenFight(fight);
                    }
                }

                card.End();
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            }

            DrawCreditsRow(manifest, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawIndexHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), DisplayName, AppPalettes.Strats.TitleInk, 1.3f,
            FontWeight.Bold);
        if (manifestStore.State == StratsState.Loading && manifestStore.Manifest is null)
        {
            LoadingPulse.Spinner(new Vector2(area.Max.X - 22f * scale, rowCenterY), 8f * scale, ui.Accent);
        }
    }

    private void DrawIndexState(Rect body, float scale)
    {
        if (manifestStore.State == StratsState.Failed)
        {
            if (EmptyState.Draw(body, ui, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Strats.LoadFailed),
                    Loc.T(L.Strats.LoadFailedHint), Loc.T(L.Strats.Retry)))
            {
                manifestStore.EnsureFresh(true);
            }

            return;
        }

        Skeleton.Rows(ImGui.GetWindowDrawList(),
            new Rect(new Vector2(body.Min.X + 14f * scale, body.Min.Y + 16f * scale),
                new Vector2(body.Max.X - 14f * scale, body.Max.Y - 12f * scale)), 52f, 10f, scale);
    }

    private void DrawCreditsRow(StratsManifest manifest, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + CreditsRowHeight * scale));
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var ink = hovered ? AppPalettes.Strats.BodyInk : AppPalettes.Strats.MutedInk;
        Typography.DrawCentered(row.Center, Loc.T(L.Strats.PoweredBy, manifest.Credits.SiteName), ink,
            TextStyles.FootnoteEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered) && manifest.Credits.SiteUrl.Length > 0)
        {
            UrlActions.AskThenOpen(manifest.Credits.SiteUrl);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, CreditsRowHeight * scale));
    }
}
