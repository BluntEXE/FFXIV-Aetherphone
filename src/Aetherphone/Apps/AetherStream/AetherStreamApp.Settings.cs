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
        ui.Body(area);
        var accentedContext = new PhoneContext(area, accentedTheme, context.Navigation);

        AppHeader.Draw(accentedContext, Loc.T(L.AetherStream.SettingsTitle), () => router.Pop());

        var margin = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top), new Vector2(area.Max.X - margin, area.Max.Y));

        var dependencies = screen.Engine.Dependencies;

        using (AppSurface.Begin(content))
        {
            SettingsSection.Header(Loc.T(L.AetherStream.SettingsSectionStatus), accentedTheme);
            var statusCard = GroupCard.Begin(accentedTheme, 5);
            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyStatus),
                DependencyStatusText(dependencies, dependencies.VideoLibrary), accentedTheme);
            DrawDependencyAction(statusCard.NextRow(), dependencies, dependencies.VideoLibrary,
                L.AetherStream.SettingsDownloadMpv, L.AetherStream.SettingsUpdateMpv);

            SettingsRow.Info(statusCard.NextRow(), Loc.T(L.AetherStream.SettingsDependencyYtdlp),
                DependencyStatusText(dependencies, dependencies.LinkResolver), accentedTheme);
            DrawDependencyAction(statusCard.NextRow(), dependencies, dependencies.LinkResolver,
                L.AetherStream.SettingsDownloadYtdlp, L.AetherStream.SettingsUpdateYtdlp);

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
            var watchingCard = GroupCard.Begin(accentedTheme, 3);
            var sharePresence = SettingsRow.Bool(watchingCard.NextRow(),
                Loc.T(L.AetherStream.SettingsShareWatchPresence), configuration.VideoShareWatchPresence,
                accentedTheme);
            var discoverable = SettingsRow.Bool(watchingCard.NextRow(),
                Loc.T(L.AetherStream.SettingsDiscoverable), configuration.VideoStreamDiscoverable,
                accentedTheme);
            var approvalRequired = SettingsRow.Bool(watchingCard.NextRow(),
                Loc.T(L.AetherStream.SettingsApprovalRequired), configuration.VideoStreamApprovalRequired,
                accentedTheme);
            watchingCard.End();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsShareWatchPresenceHint), accentedTheme);
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsDiscoverableHint), accentedTheme);
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            SettingsSection.Hint(Loc.T(L.AetherStream.SettingsApprovalRequiredHint), accentedTheme);
            if (discoverable != configuration.VideoStreamDiscoverable)
            {
                configuration.VideoStreamDiscoverable = discoverable;
                configuration.Save();
            }

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

    private void DrawDependencyAction(Rect row, MediaDependencies dependencies, MediaDependency dependency,
        LocString installLabel, LocString updateLabel)
    {
        var snapshot = dependency.Snapshot();
        var busy = snapshot.State is DependencyState.Checking or DependencyState.Downloading
            or DependencyState.Installing;
        var label = busy
            ? Loc.T(L.AetherStream.SettingsDownloading)
            : snapshot.State == DependencyState.Ready ? Loc.T(updateLabel) : Loc.T(installLabel);

        if (!SettingsRow.Action(row, label, busy ? accentedTheme.TextMuted : accentedTheme.Accent, accentedTheme)
            || busy)
        {
            return;
        }

        dependencyWork.Run("install " + dependency.Id,
            async token => await dependencies.ReinstallAsync(dependency, token).ConfigureAwait(false));
    }

    private static string DependencyStatusText(MediaDependencies dependencies, MediaDependency dependency)
    {
        var snapshot = dependency.Snapshot();
        switch (snapshot.State)
        {
            case DependencyState.Checking:
                return Loc.T(L.AetherStream.SetupChecking);
            case DependencyState.Downloading:
                return DependencySizeText(snapshot);
            case DependencyState.Installing:
                return Loc.T(L.AetherStream.SetupInstalling);
            case DependencyState.Failed:
                return snapshot.FailureReason ?? Loc.T(L.AetherStream.SetupFailed);
        }

        if (snapshot.State != DependencyState.Ready)
        {
            return Loc.T(L.AetherStream.SettingsDependencyNotInstalled);
        }

        return dependencies.HasUpdate(dependency)
            ? Loc.T(L.AetherStream.SettingsDependencyUpdateAvailable)
            : Loc.T(L.AetherStream.SettingsDependencyOk);
    }

    private static string DependencySizeText(DependencyProgress snapshot)
    {
        if (snapshot.TotalBytes <= 0)
        {
            return Loc.T(L.AetherStream.SetupDownloading);
        }

        return string.Format(Loc.T(L.AetherStream.SetupProgress), FormatMegabytes(snapshot.ReceivedBytes),
            FormatMegabytes(snapshot.TotalBytes));
    }

    private static string FormatMegabytes(long bytes) =>
        (bytes / (1024d * 1024d)).ToString("0.#", Loc.Culture);

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
