using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Mods;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Mods;

internal sealed partial class ModsApp
{
    private const int OneClickPasswordMaxLength = 128;

    private string oneClickDraft = string.Empty;
    private bool oneClickDraftLoaded;

    private void DrawSettings(Rect area)
    {
        var scale = UiScale.Current;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var inset = 16f * scale;
        var title = Loc.T(L.Mods.TabSettings);
        var titleY = rowCenterY - Typography.Measure(title, TextStyles.Title2).Y * 0.5f;
        Marquee.DrawLeftAuto(ImGui.GetWindowDrawList(), "mods.settings.title", title, area.Min.X + inset, titleY,
            MathF.Max(1f, area.Width - inset * 2f), TextStyles.Title2, ui.TitleInk);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            DrawContentSettings(scale);
            DrawHeliosphereSettings(scale);
            DrawPenumbraSettings();
            DrawAboutSettings(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawContentSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Mods.ContentSection), theme);
        var card = GroupCard.Begin(theme, 5);
        var nsfw = SettingsRow.Bool(card.NextRow(), Loc.T(L.Mods.ShowNsfw), snapshot.ShowNsfw, theme, "mods.nsfw");
        if (nsfw != snapshot.ShowNsfw)
        {
            if (nsfw)
            {
                AskNsfwConsent();
            }
            else
            {
                snapshot.ShowNsfw = false;
                SnapshotChanged();
            }
        }

        var nsfl = SettingsRow.Bool(card.NextRow(), Loc.T(L.Mods.ShowNsfl), snapshot.ShowNsfl, theme, "mods.nsfl",
            null, !snapshot.ShowNsfw);
        if (nsfl != snapshot.ShowNsfl && snapshot.ShowNsfw)
        {
            snapshot.ShowNsfl = nsfl;
            SnapshotChanged();
        }

        var warnings = SettingsRow.Bool(card.NextRow(), Loc.T(L.Mods.ShowContentWarnings),
            snapshot.ShowContentWarnings, theme, "mods.cw");
        if (warnings != snapshot.ShowContentWarnings)
        {
            snapshot.ShowContentWarnings = warnings;
            SnapshotChanged();
        }

        var hidePaid = SettingsRow.Bool(card.NextRow(), Loc.T(L.Mods.HidePaid), snapshot.HidePaid, theme,
            "mods.paid");
        if (hidePaid != snapshot.HidePaid)
        {
            snapshot.HidePaid = hidePaid;
            SnapshotChanged();
        }

        var blur = SettingsRow.Bool(card.NextRow(), Loc.T(L.Mods.BlurSensitive), snapshot.BlurSensitive, theme,
            "mods.blur");
        if (blur != snapshot.BlurSensitive)
        {
            snapshot.BlurSensitive = blur;
            snapshotDirty = true;
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Mods.ContentHint), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private void AskNsfwConsent()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Mods.NsfwConfirmTitle),
            Message = Loc.T(L.Mods.NsfwConfirmBody),
            ConfirmLabel = Loc.T(L.Mods.NsfwConfirmYes),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Common.Loading),
            FailedMessage = string.Empty,
            Danger = false,
            ConfirmAsync = done =>
            {
                snapshot.ShowNsfw = true;
                SnapshotChanged();
                PersistSnapshot();
                done(true);
            },
        });
    }

    private void DrawHeliosphereSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Mods.HeliosphereSection), theme);
        var card = GroupCard.Begin(theme, 2);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Mods.PluginStatus), HeliospherePluginStatus(), theme);
        if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.ExternalLinkAlt, ui.Accent, Loc.T(L.Mods.OneClickLink),
                string.Empty, theme))
        {
            UrlActions.AskThenOpen(ModsContent.OneClickSettingsUrl);
        }

        card.End();
        if (!oneClickDraftLoaded)
        {
            oneClickDraft = snapshot.OneClickPassword;
            oneClickDraftLoaded = true;
        }

        ui.Field(Loc.T(L.Mods.OneClickPassword), "mods.oneclick", ref oneClickDraft, OneClickPasswordMaxLength,
            false);
        if (!string.Equals(oneClickDraft, snapshot.OneClickPassword, StringComparison.Ordinal))
        {
            snapshot.OneClickPassword = oneClickDraft;
            snapshotDirty = true;
        }

        SettingsSection.Hint(Loc.T(L.Mods.OneClickHint), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private string HeliospherePluginStatus()
    {
        if (PluginLoaded())
        {
            var version = HeliosphereBridge.InstalledPluginVersion();
            return version.Length == 0 ? Loc.T(L.Mods.StatusRunning) : Loc.T(L.Mods.StatusRunning) + " " + version;
        }

        return HeliosphereBridge.InstalledPluginVersion().Length == 0
            ? Loc.T(L.Mods.StatusMissing)
            : Loc.T(L.Mods.StatusNotLoaded);
    }

    private void DrawPenumbraSettings()
    {
        SettingsSection.Header(Loc.T(L.Mods.PenumbraSection), theme);
        var card = GroupCard.Begin(theme, 3);
        var available = PenumbraBridge.IsAvailable();
        SettingsRow.Info(card.NextRow(), Loc.T(L.Mods.PluginStatus),
            Loc.T(available ? L.Mods.StatusRunning : L.Mods.StatusMissing), theme);
        var apiVersion = available && PenumbraBridge.TryGetApiVersion(out var breaking, out var features)
            ? breaking + "." + features
            : string.Empty;
        SettingsRow.Info(card.NextRow(), Loc.T(L.Mods.ApiVersion), apiVersion, theme);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Mods.ModDirectory), hub.Library.ModRoot, theme);
        card.End();
    }

    private void DrawAboutSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Mods.AboutSection), theme);
        var card = GroupCard.Begin(theme, 3);
        if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.Globe, ui.Accent, Loc.T(L.Mods.OpenSite), string.Empty,
                theme))
        {
            UrlActions.AskThenOpen(ModsContent.SiteUrl);
        }

        if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.FileContract, ui.Accent, Loc.T(L.Mods.Terms),
                string.Empty, theme))
        {
            UrlActions.AskThenOpen(ModsContent.TermsUrl);
        }

        if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.LifeRing, ui.Accent, Loc.T(L.Mods.Support),
                string.Empty, theme))
        {
            UrlActions.AskThenOpen(ModsContent.SupportUrl);
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Mods.AboutBody), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }
}
