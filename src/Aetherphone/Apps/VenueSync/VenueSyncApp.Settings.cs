using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp
{
    private string settingsKeyInput = string.Empty;
    private bool settingsKeyVisible;
    private List<VenueSyncVenue> settingsVenues = new();
    private string? settingsError;
    private string? settingsCharacterLinkStatus;

    private void DrawSettings(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), "Sync Settings", back);

        if (settingsKeyInput.Length == 0 && configuration.VenueSyncApiKey.Length > 0)
        {
            settingsKeyInput = configuration.VenueSyncApiKey;
        }

        var cursorY = area.Min.Y + AppHeader.Height * scale;
        var left = area.Min.X + Metrics.Space.Lg * scale;
        var right = area.Max.X - Metrics.Space.Lg * scale;
        var width = MathF.Max(1f, right - left);

        Marquee.DrawLeftAuto("venuesync.settings.api-key-label", "API KEY", left, cursorY, width, TextStyles.Caption1,
            theme.TextMuted);
        cursorY += 20f * scale + Metrics.Space.Xs * scale;

        var eyeWidth = 60f * scale;
        var fieldWidth = MathF.Max(1f, width - eyeWidth - Metrics.Space.Sm * scale);
        ImGui.SetCursorScreenPos(new Vector2(left, cursorY));
        ImGui.SetNextItemWidth(fieldWidth);
        var flags = settingsKeyVisible ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password;
        if (ImGui.InputTextWithHint("##sync-api-key", "API key (vm_...)", ref settingsKeyInput, 128, flags))
        {
            configuration.VenueSyncApiKey = settingsKeyInput;
            configuration.Save();
        }

        var eyeRow = new Rect(new Vector2(left + fieldWidth + Metrics.Space.Sm * scale, cursorY),
            new Vector2(right, cursorY + Metrics.Size.FieldHeight * scale));
        if (SettingsRow.Action(eyeRow, settingsKeyVisible ? "Hide" : "Show", theme.TextMuted, theme))
        {
            settingsKeyVisible = !settingsKeyVisible;
        }

        cursorY += Metrics.Size.FieldHeight * scale + Metrics.Space.Md * scale;

        var venueLabel = string.IsNullOrEmpty(configuration.VenueSyncSelectedVenueName)
            ? "Select venue"
            : configuration.VenueSyncSelectedVenueName;
        var venueRow = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + Metrics.Size.Row * scale));
        if (SettingsRow.Disclosure(venueRow, "Venue", venueLabel, theme))
        {
            _ = LoadVenuesAsync();
        }

        cursorY += Metrics.Size.Row * scale + Metrics.Space.Sm * scale;

        foreach (var venue in settingsVenues)
        {
            var selected = venue.Id == configuration.VenueSyncSelectedVenueId;
            var row = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + Metrics.Size.Row * scale));
            if (SettingsRow.Selectable(row, venue.Name, selected, theme, $"venuesync.settings.venue-{venue.Id}"))
            {
                configuration.VenueSyncSelectedVenueId = venue.Id;
                configuration.VenueSyncSelectedVenueName = venue.Name;
                configuration.Save();
                state.EnsureShiftsFresh(true);
            }

            cursorY += Metrics.Size.Row * scale + Metrics.Space.Sm * scale;
        }

        if (settingsError is not null)
        {
            Marquee.DrawLeftAuto("venuesync.settings.error", settingsError, left, cursorY, width, TextStyles.Caption1,
                theme.Danger);
            cursorY += 20f * scale + Metrics.Space.Sm * scale;
        }

        cursorY += Metrics.Space.Lg * scale;

        var localName = gameData.LocalPlayer?.Name.TextValue;
        var localWorldId = gameData.LocalHomeWorldId;
        var localWorld = localWorldId != 0 ? gameData.WorldName(localWorldId) : string.Empty;
        if (string.IsNullOrEmpty(localName) || string.IsNullOrEmpty(localWorld))
        {
            Marquee.DrawLeftAuto("venuesync.settings.no-character", "No character detected — enter the game world first",
                left, cursorY, width, TextStyles.Body, theme.TextMuted);
            return;
        }

        Marquee.DrawLeftAuto("char-link-label", "Character detected", left, cursorY, width, TextStyles.Caption1,
            theme.TextMuted);
        cursorY += 20f * scale + Metrics.Space.Xs * scale;

        Marquee.DrawLeftAuto("char-link-name", $"{localName} @ {localWorld}", left, cursorY, width, TextStyles.Body,
            theme.TextStrong);
        cursorY += 24f * scale + Metrics.Space.Md * scale;

        var linkRow = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + Metrics.Size.Row * scale));
        var linkLabel = settingsCharacterLinkStatus ?? "Link this character";
        if (SettingsRow.Action(linkRow, linkLabel, theme.Accent, theme))
        {
            _ = LinkCharacterAsync(localName, localWorld);
        }
    }

    private async Task LoadVenuesAsync()
    {
        try
        {
            var response = await client.GetVenuesAsync(CancellationToken.None).ConfigureAwait(false);
            if (response is not null)
            {
                settingsVenues = response.Venues;
                settingsError = null;
            }
            else
            {
                settingsError = "Failed to load venues — check your API key";
            }
        }
        catch (Exception)
        {
            settingsError = "Failed to load venues — check your API key";
        }
    }

    private async Task LinkCharacterAsync(string characterName, string world)
    {
        settingsCharacterLinkStatus = "Linking…";
        try
        {
            var response = await client.LinkCharacterAsync(characterName, world, CancellationToken.None)
                .ConfigureAwait(false);
            if (response?.Character is not null)
            {
                settingsCharacterLinkStatus = "Linked ✓";
            }
            else
            {
                settingsCharacterLinkStatus = response?.Error ?? "Failed to link — tap to retry";
            }
        }
        catch (Exception)
        {
            settingsCharacterLinkStatus = "Failed to link — tap to retry";
        }
    }
}
