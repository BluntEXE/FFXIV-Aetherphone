using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    // Capped at 1080p, not YouTube's actual ceiling - the TV's own screen texture is a fixed
    // 1920x1080 (VideoRenderTarget.ScreenWidth/Height), so anything higher would just be
    // downscaled into the same buffer by mpv for no visible gain, at the cost of extra bandwidth
    // and decode work. 720p+ now resolves via adaptive video+audio streams, not muxed-only - see
    // VideoUrlResolver.
    private static readonly int[] QualityOptions = { 144, 240, 360, 480, 720, 1080 };
    private readonly DropdownMenu qualityMenu = new();
    private Rect qualityRowRect;

    private void DrawSettings(PhoneContext context, Rect area, float scale)
    {
        // Every other tab in this app draws through `ui` (AppSkin, accented pink via
        // AppAccents.For("aetherstream")). SettingsRow/Toggle/GroupCard/AppHeader all take a
        // plain PhoneTheme instead, so without this the whole Settings screen silently fell back
        // to the system theme's accent/toggle-green - a different colour from every other screen
        // in this same app. PhoneTheme is a sealed class, not a record, so it can't use `with`;
        // this copies it field-by-field with just Accent/ToggleOn swapped.
        var accentedTheme = AccentedTheme(context.Theme);
        var accentedContext = new PhoneContext(context.Content, accentedTheme, context.Navigation);

        AppHeader.Draw(accentedContext, Loc.T(L.AetherStream.SettingsTitle), () => router.Pop());

        var margin = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top), new Vector2(area.Max.X - margin, area.Max.Y));

        VideoNativeLibrary.EnsureYtdlpChecked();

        using (AppSurface.Begin(content))
        {
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionStatus), accentedTheme);
            var statusCard = GroupCard.Begin(accentedTheme, 3);
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyStatus),
                DependencyStatusText(VideoNativeLibrary.LoadError), accentedTheme);
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyYtdlp),
                DependencyStatusText(VideoNativeLibrary.YtdlpError), accentedTheme);
            if (SettingsRow.Disclosure(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsScreen), ScreenStateText(),
                    accentedTheme))
            {
                activeTab = AetherStreamTab.Casting;
                router.Pop();
            }

            statusCard.End();

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionPlayback), accentedTheme);
            var playbackCard = GroupCard.Begin(accentedTheme, 2);
            var hideNameplates = SettingsRow.Bool(playbackCard.NextRow(),
                Loc.T(L.AetherStream.SettingsHideNameplates), configuration.VideoHideNameplates, accentedTheme);
            DrawQualityRow(playbackCard.NextRow(), accentedTheme);
            playbackCard.End();
            if (hideNameplates != configuration.VideoHideNameplates)
            {
                configuration.VideoHideNameplates = hideNameplates;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionWatching), accentedTheme);
            var watchingCard = GroupCard.Begin(accentedTheme, 1);
            var sharePresence = SettingsRow.Bool(watchingCard.NextRow(),
                Loc.T(L.AetherStream.SettingsShareWatchPresence), configuration.VideoShareWatchPresence,
                accentedTheme);
            watchingCard.End();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsShareWatchPresenceHint), accentedTheme);
            if (sharePresence != configuration.VideoShareWatchPresence)
            {
                configuration.VideoShareWatchPresence = sharePresence;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionAdvanced), accentedTheme);
            var hardwareCard = GroupCard.Begin(accentedTheme, 1);
            var hardwareDecoding = SettingsRow.Bool(hardwareCard.NextRow(),
                Loc.T(L.AetherStream.SettingsHardwareDecoding), configuration.VideoHardwareDecoding, accentedTheme);
            hardwareCard.End();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsHardwareDecodingHint), accentedTheme);
            if (hardwareDecoding != configuration.VideoHardwareDecoding)
            {
                configuration.VideoHardwareDecoding = hardwareDecoding;
                configuration.Save();
                video.HardwareDecoding = hardwareDecoding;
            }

            var allowInsecure = configuration.VideoAllowInsecureDirectUrls;
            if (WineEnvironment.IsWine)
            {
                ImGui.Dummy(new Vector2(0f, 12f * scale));
                var tlsCard = GroupCard.Begin(accentedTheme, 1);
                allowInsecure = SettingsRow.Bool(tlsCard.NextRow(), Loc.T(L.AetherStream.SettingsTls), allowInsecure,
                    accentedTheme);
                tlsCard.End();
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                SettingsSection.Hint(Loc.T(L.AetherStream.SettingsTlsHint), accentedTheme);
            }

            if (allowInsecure != configuration.VideoAllowInsecureDirectUrls)
            {
                configuration.VideoAllowInsecureDirectUrls = allowInsecure;
                configuration.Save();
                video.AllowInsecureDirectUrls = allowInsecure;
            }
        }

        qualityMenu.Gate();
        if (qualityMenu.IsOpenFor("aetherstream.quality"))
        {
            var items = new DropdownMenu.Item[QualityOptions.Length];
            for (var index = 0; index < QualityOptions.Length; index++)
            {
                items[index] = new DropdownMenu.Item($"{QualityOptions[index]}p",
                    Selected: QualityOptions[index] == configuration.VideoMaxQualityHeight);
            }

            var picked = qualityMenu.Draw(context.Content, accentedTheme, items);
            if (picked >= 0)
            {
                configuration.VideoMaxQualityHeight = QualityOptions[picked];
                configuration.Save();
                video.MaxQualityHeight = QualityOptions[picked];
            }
        }
    }

    // PhoneTheme is a sealed class with required init-only properties, not a record, so there is
    // no `with` support - this is the only way to hand existing PhoneTheme-typed components
    // (SettingsRow, Toggle, GroupCard, AppHeader, ...) this app's own accent instead of the
    // system theme's, without touching those shared components' own colour logic.
    private static PhoneTheme AccentedTheme(PhoneTheme baseTheme)
    {
        var accent = AppAccents.For("aetherstream");
        return new PhoneTheme
        {
            BezelOuter = baseTheme.BezelOuter,
            FrameMetal = baseTheme.FrameMetal,
            RailMetal = baseTheme.RailMetal,
            ScreenBase = baseTheme.ScreenBase,
            LightWallpaperId = baseTheme.LightWallpaperId,
            DarkWallpaperId = baseTheme.DarkWallpaperId,
            AppBackground = baseTheme.AppBackground,
            GroupedCard = baseTheme.GroupedCard,
            Separator = baseTheme.Separator,
            ToggleOn = accent,
            ToggleOff = baseTheme.ToggleOff,
            Surface = baseTheme.Surface,
            SurfaceMuted = baseTheme.SurfaceMuted,
            TextStrong = baseTheme.TextStrong,
            TextMuted = baseTheme.TextMuted,
            Accent = accent,
            Danger = baseTheme.Danger,
            DeviceRounding = baseTheme.DeviceRounding,
            BezelThickness = baseTheme.BezelThickness,
            ScreenRounding = baseTheme.ScreenRounding,
            TopZoneHeight = baseTheme.TopZoneHeight,
            BottomZoneHeight = baseTheme.BottomZoneHeight,
            SidePadding = baseTheme.SidePadding,
        };
    }

    private static string DependencyStatusText(string? loadError) =>
        loadError ?? Loc.T(L.AetherStream.SettingsDependencyOk);

    private string ScreenStateText() => screen.State switch
    {
        ScreenState.Ready => Loc.T(L.AetherStream.CastingStateReady),
        _ => Loc.T(L.AetherStream.CastingStateNotReady),
    };

    private void DrawQualityRow(Rect row, PhoneTheme theme)
    {
        qualityRowRect = row;
        if (SettingsRow.Disclosure(row, Loc.T(L.AetherStream.SettingsMaxQuality),
                $"{configuration.VideoMaxQualityHeight}p", theme))
        {
            qualityMenu.Toggle("aetherstream.quality", qualityRowRect);
        }
    }

}
