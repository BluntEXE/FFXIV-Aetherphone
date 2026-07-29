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

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header("API KEY", theme);

            var apiKeyCard = GroupCard.Begin(theme, 1, Metrics.Size.FieldHeight);
            var keyRow = apiKeyCard.NextRow();
            var eyeWidth = 60f * scale;
            var fieldWidth = MathF.Max(1f, keyRow.Width - eyeWidth - Metrics.Space.Sm * scale);
            ImGui.SetCursorScreenPos(keyRow.Min);
            ImGui.SetNextItemWidth(fieldWidth);
            var flags = settingsKeyVisible ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password;
            if (ImGui.InputTextWithHint("##sync-api-key", "API key (vm_...)", ref settingsKeyInput, 128, flags))
            {
                configuration.VenueSyncApiKey = settingsKeyInput;
                configuration.Save();
            }

            var eyeRow = new Rect(new Vector2(keyRow.Min.X + fieldWidth + Metrics.Space.Sm * scale, keyRow.Min.Y),
                keyRow.Max);
            if (AppSkin.PillButton(eyeRow, settingsKeyVisible ? "Hide" : "Show", filled: false, theme))
            {
                settingsKeyVisible = !settingsKeyVisible;
            }

            apiKeyCard.End();

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

            var venueLabel = string.IsNullOrEmpty(configuration.VenueSyncSelectedVenueName)
                ? "Select venue"
                : configuration.VenueSyncSelectedVenueName;
            var venueCard = GroupCard.Begin(theme, 1 + settingsVenues.Count, Metrics.Size.Row);
            if (SettingsRow.Disclosure(venueCard.NextRow(), "Venue", venueLabel, theme))
            {
                _ = LoadVenuesAsync();
            }

            foreach (var venue in settingsVenues)
            {
                var selected = venue.Id == configuration.VenueSyncSelectedVenueId;
                if (SettingsRow.Selectable(venueCard.NextRow(), venue.Name, selected, theme,
                        $"venuesync.settings.venue-{venue.Id}"))
                {
                    configuration.VenueSyncSelectedVenueId = venue.Id;
                    configuration.VenueSyncSelectedVenueName = venue.Name;
                    configuration.Save();
                    state.EnsureShiftsFresh(true);
                }
            }

            venueCard.End();

            if (settingsError is not null)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
                var errorOrigin = ImGui.GetCursorScreenPos();
                var errorWidth = ImGui.GetContentRegionAvail().X;
                Marquee.DrawLeftAuto("venuesync.settings.error", settingsError, errorOrigin.X, errorOrigin.Y,
                    errorWidth, TextStyles.Caption1, theme.Danger);
                ImGui.Dummy(new Vector2(0f, 20f * scale));
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

            var localName = gameData.LocalPlayer?.Name.TextValue;
            var localWorldId = gameData.LocalHomeWorldId;
            var localWorld = localWorldId != 0 ? gameData.WorldName(localWorldId) : string.Empty;
            if (string.IsNullOrEmpty(localName) || string.IsNullOrEmpty(localWorld))
            {
                var noCharOrigin = ImGui.GetCursorScreenPos();
                var noCharWidth = ImGui.GetContentRegionAvail().X;
                Marquee.DrawLeftAuto("venuesync.settings.no-character",
                    "No character detected — enter the game world first", noCharOrigin.X, noCharOrigin.Y,
                    noCharWidth, TextStyles.Body, theme.TextMuted);
                return;
            }

            SettingsSection.Header("CHARACTER", theme);

            var characterCard = GroupCard.Begin(theme, 2, Metrics.Size.Row);
            SettingsRow.Info(characterCard.NextRow(), "Character", $"{localName} @ {localWorld}", theme);
            var linkLabel = settingsCharacterLinkStatus ?? "Link this character";
            if (AppSkin.PillButton(characterCard.NextRow(), linkLabel, filled: true, theme))
            {
                _ = LinkCharacterAsync(localName, localWorld);
            }

            characterCard.End();
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
