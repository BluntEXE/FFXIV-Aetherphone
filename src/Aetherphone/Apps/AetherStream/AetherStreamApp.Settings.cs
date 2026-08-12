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
    private static readonly int[] QualityOptions = { 144, 240, 360, 480, 720, 1080 };
    private readonly DropdownMenu qualityMenu = new();
    private Rect qualityRowRect;

    private void DrawSettings(PhoneContext context, Rect area, float scale)
    {
        var accentedTheme = AccentedTheme(context.Theme);
        var accentedContext = new PhoneContext(context.Content, accentedTheme, context.Navigation);

        AppHeader.Draw(accentedContext, Loc.T(L.AetherStream.SettingsTitle), () => router.Pop());

        var margin = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top), new Vector2(area.Max.X - margin, area.Max.Y));

        var resources = screen.Engine.Resources;

        using (AppSurface.Begin(content))
        {
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionStatus), accentedTheme);
            var statusCard = GroupCard.Begin(accentedTheme, 3);
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyStatus),
                MpvStatusText(resources), accentedTheme);
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyYtdlp),
                YtdlpStatusText(resources), accentedTheme);
            if (SettingsRow.Disclosure(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsScreen), ScreenStateText(),
                    accentedTheme))
            {
                activeTab = AetherStreamTab.Casting;
                router.Pop();
            }

            statusCard.End();

            {
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                var buttonHeight = 34f * scale;
                var buttonTop = ImGui.GetCursorScreenPos().Y;

                {
                    var mpvRect = new Rect(new Vector2(content.Min.X, buttonTop),
                        new Vector2(content.Max.X, buttonTop + buttonHeight));
                    var mpvLabel = resources.GetLocationMPV() is null
                        ? Loc.T(L.AetherStream.SettingsDownloadMpv)
                        : Loc.T(L.AetherStream.SettingsUpdateMpv);
                    if (SmallButton(mpvRect, mpvLabel, !mpvDownloading, scale))
                    {
                        mpvDownloading = true;
                        dependencyWork.Run("download mpv", async token =>
                        {
                            if (resources.MpvCheckResult[0].Length == 0)
                            {
                                await resources.CheckMPVAsync().ConfigureAwait(false);
                            }

                            if (resources.MpvCheckResult[0].Length > 0)
                            {
                                await resources.DownloadMPVAsync().ConfigureAwait(false);
                            }
                        }, () => mpvDownloading = false);
                    }

                    buttonTop += buttonHeight + 8f * scale;
                }

                {
                    var ytdlpRect = new Rect(new Vector2(content.Min.X, buttonTop),
                        new Vector2(content.Max.X, buttonTop + buttonHeight));
                    var ytdlpLabel = resources.GetLocationYTDLP() is null
                        ? Loc.T(L.AetherStream.SettingsDownloadYtdlp)
                        : Loc.T(L.AetherStream.SettingsUpdateYtdlp);
                    if (SmallButton(ytdlpRect, ytdlpLabel, !ytdlpDownloading, scale))
                    {
                        ytdlpDownloading = true;
                        dependencyWork.Run("download yt-dlp", async token =>
                        {
                            if (resources.YtdlpCheckResult[0].Length == 0)
                            {
                                await resources.CheckYTDLPAsync().ConfigureAwait(false);
                            }

                            if (resources.YtdlpCheckResult[0].Length > 0)
                            {
                                await resources.DownloadYTDLPAsync().ConfigureAwait(false);
                            }
                        }, () => ytdlpDownloading = false);
                    }
                }

                ImGui.SetCursorScreenPos(new Vector2(content.Min.X, buttonTop + buttonHeight));
            }

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
            var watchingCard = GroupCard.Begin(accentedTheme, 2);
            var sharePresence = SettingsRow.Bool(watchingCard.NextRow(),
                Loc.T(L.AetherStream.SettingsShareWatchPresence), configuration.VideoShareWatchPresence,
                accentedTheme);
            var approvalRequired = SettingsRow.Bool(watchingCard.NextRow(),
                Loc.T(L.AetherStream.SettingsApprovalRequired), configuration.VideoStreamApprovalRequired,
                accentedTheme);
            watchingCard.End();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsShareWatchPresenceHint), accentedTheme);
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsApprovalRequiredHint), accentedTheme);
            if (sharePresence != configuration.VideoShareWatchPresence)
            {
                configuration.VideoShareWatchPresence = sharePresence;
                configuration.Save();
            }

            if (approvalRequired != configuration.VideoStreamApprovalRequired)
            {
                configuration.VideoStreamApprovalRequired = approvalRequired;
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

    private static PhoneTheme AccentedTheme(PhoneTheme baseTheme)
    {
        var accent = AppAccents.For("aetherstream");
        return new PhoneTheme
        {
            Case = baseTheme.Case,
            CaseKind = baseTheme.CaseKind,
            CaseTextureId = baseTheme.CaseTextureId,
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
            RailWidth = baseTheme.RailWidth,
            MetalWidth = baseTheme.MetalWidth,
            GlassWidth = baseTheme.GlassWidth,
            DeviceRounding = baseTheme.DeviceRounding,
            TopZoneHeight = baseTheme.TopZoneHeight,
            BottomZoneHeight = baseTheme.BottomZoneHeight,
            SidePadding = baseTheme.SidePadding,
        };
    }

    private static string MpvStatusText(Resources resources)
    {
        if (resources.GetLocationMPV() is null)
        {
            return Loc.T(L.AetherStream.SettingsDependencyNotInstalled);
        }

        return resources.MpvCheckResult[0].Length > 0
            ? Loc.T(L.AetherStream.SettingsDependencyUpdateAvailable)
            : Loc.T(L.AetherStream.SettingsDependencyOk);
    }

    private static string YtdlpStatusText(Resources resources)
    {
        if (resources.GetLocationYTDLP() is null)
        {
            return Loc.T(L.AetherStream.SettingsDependencyNotInstalled);
        }

        return resources.YtdlpCheckResult[0].Length > 0
            ? Loc.T(L.AetherStream.SettingsDependencyUpdateAvailable)
            : Loc.T(L.AetherStream.SettingsDependencyOk);
    }

    private string ScreenStateText() => screen.Engine.IsActive
        ? Loc.T(L.AetherStream.CastingStateReady)
        : Loc.T(L.AetherStream.CastingStateNotReady);

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
