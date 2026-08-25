using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Strats;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp
{
    private void DrawAbout(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Strats.About), back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var manifest = manifestStore.Manifest;
        if (manifest is null)
        {
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawAboutHero(manifest, scale);
            SettingsSection.Header(Loc.T(L.Strats.SiteLinks), theme);
            var links = manifest.Credits.Links;
            var card = GroupCard.Begin(theme, links.Length);
            for (var index = 0; index < links.Length; index++)
            {
                var link = links[index];
                if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.ExternalLinkAlt, ui.Accent, link.Label,
                        string.Empty, theme, false, link.Url))
                {
                    UrlActions.AskThenOpen(link.Url);
                }
            }

            card.End();
            DrawStratSources(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawAboutHero(StratsManifest manifest, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Lg * scale;
        var innerWidth = width - pad * 2f;
        var bodyHeight = Typography.MeasureWrapped(Loc.T(L.Strats.AboutBody), innerWidth, TextStyles.Subheadline.Scale);
        var licenseHeight = Typography.LineHeight(TextStyles.Footnote);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var authorHeight = Typography.LineHeight(TextStyles.Subheadline);
        var height = pad + titleHeight + 4f * scale + authorHeight + Metrics.Space.Md * scale + bodyHeight +
            Metrics.Space.Md * scale + licenseHeight + pad;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Card * scale);
        var y = origin.Y + pad;
        Typography.Draw(drawList, new Vector2(origin.X + pad, y), manifest.Credits.SiteName, ui.TitleInk,
            TextStyles.Title3);
        y += titleHeight + 4f * scale;
        Typography.Draw(drawList, new Vector2(origin.X + pad, y), Loc.T(L.Strats.MadeBy, manifest.Credits.Author),
            ui.MutedInk, TextStyles.Subheadline);
        y += authorHeight + Metrics.Space.Md * scale;
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, y), Loc.T(L.Strats.AboutBody), ui.BodyInk,
            TextStyles.Subheadline, innerWidth);
        y += bodyHeight + Metrics.Space.Md * scale;
        Typography.Draw(drawList, new Vector2(origin.X + pad, y), Loc.T(L.Strats.License, manifest.Credits.License),
            ui.MutedInk, TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawStratSources(float scale)
    {
        if (resolved is null)
        {
            return;
        }

        var links = resolved.Strat.Links;
        if (links.Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        SettingsSection.Header(Loc.T(L.Strats.StratSources, resolved.Doc.Abbrev, resolved.Strat.Label), theme);
        var card = GroupCard.Begin(theme, links.Length);
        for (var index = 0; index < links.Length; index++)
        {
            var link = links[index];
            if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.Link, ui.Accent, link.Label, string.Empty, theme,
                    false, link.Url))
            {
                UrlActions.AskThenOpen(link.Url);
            }
        }

        card.End();
    }
}
