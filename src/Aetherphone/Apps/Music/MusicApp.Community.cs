using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float CommunityRowHeight = 68f;
    private const float StationCoverSize = 132f;
    private const float LinkPillHeight = 30f;
    private const int CommunityHomeRows = 3;

    private static readonly string[] LinkLabels =
    {
        "Twitch", "YouTube", "Discord", "Bluesky", "X", "Ko-fi", "Patreon",
    };

    private const int StationKindTwitch = 1;

    private string viewedStationId = string.Empty;

    private static bool IsTwitch(CommunityStationDto station) => station.Kind == StationKindTwitch;

    private void OpenCommunity()
    {
        community.Refresh();
        router.Push(View.Community);
    }

    private void OpenStationPage(CommunityStationDto station)
    {
        viewedStationId = station.Id;
        router.Push(View.Station);
    }

    private void PopToCommunity()
    {
        router.Pop();
    }

    private CommunityStationDto? ViewedStation()
    {
        return community.TryFind(viewedStationId, out var station) ? station : null;
    }

    private bool IsCurrentCommunityStation(CommunityStationDto station)
    {
        return playback.RadioActive && playback.Radio.CurrentStationInfo.CommunityId == station.Id;
    }

    private void PlayCommunityStation(CommunityStationDto station)
    {
        var snapshot = community.Stations;
        var start = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (string.Equals(snapshot[index].Id, station.Id, StringComparison.Ordinal))
            {
                start = index;
                break;
            }
        }

        playSource = Loc.T(L.Music.CommunityRadio);
        playback.PlayStations(CommunityRadioService.ToQueue(snapshot), start);
    }

    private void DrawCommunitySection(float scale)
    {
        var stations = community.Stations;
        if (stations.Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var iconBox = 30f * scale;
        var title = Typography.FitText(Loc.T(L.Music.CommunityRadio), width - iconBox - 8f * scale, TextStyles.Title3);
        var titleSize = Typography.Measure(title, TextStyles.Title3);
        var headingMin = origin;
        var headingMax = new Vector2(origin.X + width, origin.Y + titleSize.Y);
        var hovered = UiInteract.Hover(headingMin, headingMax);
        Typography.Draw(origin, title, ui.Palette.HeadingInk, TextStyles.Title3);
        var iconCenter = new Vector2(origin.X + width - iconBox * 0.5f, origin.Y + titleSize.Y * 0.5f);
        AppSkin.Icon(iconCenter, FontAwesomeIcon.ChevronRight.ToIconString(), ui.MutedInk, 0.8f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(headingMin, headingMax, hovered))
        {
            OpenCommunity();
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var shown = Math.Min(stations.Length, CommunityHomeRows);
        for (var index = 0; index < shown; index++)
        {
            DrawCommunityRow(scale, stations[index]);
        }
    }

    private void DrawCommunity(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        community.EnsureFresh(true);
        community.EnsureMine();
        DrawTopBar(context, Loc.T(L.Music.CommunityRadio), GoToHome);
        DrawMyStationEntry(content, scale);
        var body = ScrollBody(content, scale);
        var stations = community.Stations;
        if (stations.Length == 0)
        {
            DrawCommunityEmpty(body, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            for (var index = 0; index < stations.Length; index++)
            {
                DrawCommunityRow(scale, stations[index]);
            }

            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawMyStationEntry(Rect content, float scale)
    {
        if (!community.OwnsStation)
        {
            return;
        }

        var center = new Vector2(content.Max.X - 26f * scale, content.Min.Y + TopBarHeight * scale * 0.5f);
        if (ui.IconButton(center, 16f * scale, FontAwesomeIcon.BroadcastTower.ToIconString(), ui.TitleInk,
                AppSkin.Transparent, 0.8f, Loc.T(L.Music.MyStation)))
        {
            OpenMyStation();
        }
    }

    private void DrawCommunityEmpty(Rect body, float scale)
    {
        if (community.Loading)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), ui.MutedInk, TextStyles.Callout);
            return;
        }

        var center = new Vector2(body.Center.X, body.Center.Y - 20f * scale);
        Typography.DrawCentered(center, Loc.T(L.Music.CommunityEmpty), ui.TitleInk, TextStyles.Title3);
        Typography.DrawWrappedCentered(new Vector2(center.X, center.Y + 20f * scale),
            Loc.T(L.Music.CommunityEmptySub), ui.MutedInk, TextStyles.Subheadline, body.Width - 48f * scale);
    }

    private void DrawCommunityRow(float scale, CommunityStationDto station)
    {
        var rowHeight = CommunityRowHeight * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var artSize = 50f * scale;
        var artMin = new Vector2(min.X + 6f * scale, min.Y + (rowHeight - artSize) * 0.5f);
        var artMax = artMin + new Vector2(artSize, artSize);
        DrawStationArt(drawList, artMin, artMax, station, 10f * scale);

        var current = IsCurrentCommunityStation(station);
        var textLeft = artMax.X + 12f * scale;
        var textWidth = max.X - (current ? 40f * scale : 14f * scale) - textLeft;
        var nameY = min.Y + 12f * scale;
        var fittedName = Typography.FitText(station.Name, textWidth, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, nameY), fittedName, current ? ui.Accent : ui.TitleInk,
            TextStyles.BodyEmphasized);

        var statusY = min.Y + 33f * scale;
        DrawLiveMark(drawList, new Vector2(textLeft, statusY), scale, station, textWidth);

        var subtitle = station.IsLive && station.NowPlaying.Length > 0 ? station.NowPlaying : station.Description;
        if (subtitle.Length > 0)
        {
            var fittedSubtitle = Typography.FitText(subtitle, textWidth, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + 47f * scale), fittedSubtitle, ui.MutedInk,
                TextStyles.Caption1);
        }

        if (current)
        {
            Equalizer.Draw(drawList, new Vector2(max.X - 20f * scale, min.Y + rowHeight * 0.5f), scale, 17f * scale,
                clock, ui.Accent, 1f, playback.IsPlaying);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (UiInteract.Click(min, max, hovered))
        {
            OpenStationPage(station);
        }
    }

    private void DrawStationArt(ImDrawListPtr drawList, Vector2 min, Vector2 max, CommunityStationDto station,
        float rounding)
    {
        if (station.ArtworkUrl.Length > 0 && Thumb(station.ArtworkUrl).Texture is { } texture)
        {
            drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
            return;
        }

        drawList.AddImageRounded(artwork.HandleForName(station.Name), min, max, Vector2.Zero, Vector2.One,
            0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
    }

    private void DrawLiveMark(ImDrawListPtr drawList, Vector2 origin, float scale, CommunityStationDto station,
        float available)
    {
        if (!station.IsLive)
        {
            var offAir = Typography.FitText(Loc.T(L.Music.OffAir), available, TextStyles.Caption1);
            Typography.Draw(drawList, origin, offAir, ui.MutedInk, TextStyles.Caption1);
            return;
        }

        var dotRadius = 3.5f * scale;
        var dotCenter = new Vector2(origin.X + dotRadius, origin.Y + 7f * scale);
        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(ui.Accent));
        var label = string.Format(Loc.T(IsTwitch(station) ? L.Music.WatchingCount : L.Music.ListeningCount),
            station.Listeners);
        var textLeft = dotCenter.X + dotRadius + 6f * scale;
        var fitted = Typography.FitText(label, available - (textLeft - origin.X), TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y), fitted, ui.Accent, TextStyles.Caption1);
    }

    private void DrawStationPage(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        community.EnsureFresh(true);
        var station = ViewedStation();
        if (station is null)
        {
            DrawTopBar(context, Loc.T(L.Music.CommunityRadio), PopToCommunity);
            return;
        }

        DrawTopBar(context, station.Name, PopToCommunity);
        var body = ScrollBody(content, scale);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            ImGui.Dummy(new Vector2(0f, 8f * scale));

            var coverSize = StationCoverSize * scale;
            var coverOrigin = ImGui.GetCursorScreenPos();
            var coverMin = new Vector2(coverOrigin.X + (ImGui.GetContentRegionAvail().X - coverSize) * 0.5f,
                coverOrigin.Y);
            DrawStationArt(drawList, coverMin, coverMin + new Vector2(coverSize, coverSize), station, 18f * scale);
            ImGui.SetCursorScreenPos(coverOrigin);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, coverSize + 14f * scale));

            DrawStationHeadline(scale, station);
            DrawStationActions(scale, station);
            DrawStationBody(scale, station);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawStationHeadline(float scale, CommunityStationDto station)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var name = Typography.FitText(station.Name, width, TextStyles.Title2);
        var nameSize = Typography.Measure(name, TextStyles.Title2);
        Typography.Draw(drawList, new Vector2(origin.X + (width - nameSize.X) * 0.5f, origin.Y), name, ui.TitleInk,
            TextStyles.Title2);

        var hostLabel = station.OwnerHandle.Length > 0
            ? string.Format(Loc.T(L.Music.HostedBy), "@" + station.OwnerHandle)
            : string.Empty;
        var hostY = origin.Y + nameSize.Y + 4f * scale;
        if (hostLabel.Length > 0)
        {
            var host = Typography.FitText(hostLabel, width, TextStyles.Caption1);
            var hostSize = Typography.Measure(host, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(origin.X + (width - hostSize.X) * 0.5f, hostY), host, ui.MutedInk,
                TextStyles.Caption1);
        }

        var statusY = hostY + 20f * scale;
        var statusText = station.IsLive
            ? string.Format(Loc.T(IsTwitch(station) ? L.Music.WatchingCount : L.Music.ListeningCount),
                station.Listeners)
            : Loc.T(L.Music.OffAir);
        var status = Typography.FitText(statusText, width, TextStyles.Subheadline);
        var statusSize = Typography.Measure(status, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(origin.X + (width - statusSize.X) * 0.5f, statusY), status,
            station.IsLive ? ui.Accent : ui.MutedInk, TextStyles.Subheadline);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, statusY - origin.Y + statusSize.Y + 12f * scale));
    }

    private void DrawStationActions(float scale, CommunityStationDto station)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var buttonHeight = 42f * scale;
        var buttonWidth = MathF.Min(width - 32f * scale, 220f * scale);
        var buttonMin = new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y);
        var buttonRect = new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, buttonHeight));
        if (IsTwitch(station))
        {
            // Twitch audio cannot be decoded by the radio player yet, so the station page sends the
            // listener to the channel rather than pretending to play something.
            if (ui.PillButton(buttonRect, Loc.T(L.Music.WatchOnTwitch), station.IsLive, "music.station.watch")
                && station.WatchUrl.Length > 0)
            {
                Dalamud.Utility.Util.OpenLink(station.WatchUrl);
            }
        }
        else
        {
            var current = IsCurrentCommunityStation(station);
            var label = current ? Loc.T(L.Music.StopListening) : Loc.T(L.Music.ListenLive);
            var enabled = station.IsLive || current;
            if (enabled && ui.PillButton(buttonRect, label, !current, "music.station.play"))
            {
                if (current)
                {
                    playback.Stop();
                }
                else
                {
                    PlayCommunityStation(station);
                }
            }

            if (!enabled)
            {
                ui.PillButton(buttonRect, label, false, "music.station.playDisabled");
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, buttonHeight + 16f * scale));
    }

    private void DrawStationBody(float scale, CommunityStationDto station)
    {
        var width = ImGui.GetContentRegionAvail().X;
        if (station.IsLive && station.NowPlaying.Length > 0)
        {
            DrawStationParagraph(scale, station.NowPlaying, ui.Accent, TextStyles.Callout, width);
        }

        if (station.Description.Length > 0)
        {
            DrawStationParagraph(scale, station.Description, ui.BodyInk, TextStyles.Subheadline, width);
        }

        DrawStationLinks(scale, station);

        var reportOrigin = ImGui.GetCursorScreenPos();
        var reportWidth = MathF.Min(width - 32f * scale, 200f * scale);
        var reportMin = new Vector2(reportOrigin.X + (width - reportWidth) * 0.5f, reportOrigin.Y + 8f * scale);
        var reportRect = new Rect(reportMin, reportMin + new Vector2(reportWidth, 34f * scale));
        if (ui.GhostButton(reportRect, Loc.T(L.Music.ReportStation)))
        {
            ReportStation(station);
        }

        ImGui.SetCursorScreenPos(reportOrigin);
        ImGui.Dummy(new Vector2(width, 50f * scale));
    }

    private void DrawStationParagraph(float scale, string text, Vector4 color, TextStyle style, float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var wrapWidth = width - 32f * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + 16f * scale, origin.Y), text, color, style,
            wrapWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 12f * scale));
    }

    private void DrawStationLinks(float scale, CommunityStationDto station)
    {
        if (station.Links.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pillHeight = LinkPillHeight * scale;
        var gap = 8f * scale;
        var cursorX = origin.X + 16f * scale;
        var cursorY = origin.Y;
        var rowCount = 1;
        for (var index = 0; index < station.Links.Length; index++)
        {
            var link = station.Links[index];
            if (link.Kind < 0 || link.Kind >= LinkLabels.Length)
            {
                continue;
            }

            var label = LinkLabels[link.Kind];
            var pillWidth = Typography.Measure(label, TextStyles.Caption1).X + 26f * scale;
            if (cursorX + pillWidth > origin.X + width - 16f * scale && cursorX > origin.X + 16f * scale)
            {
                cursorX = origin.X + 16f * scale;
                cursorY += pillHeight + gap;
                rowCount++;
            }

            var pillMin = new Vector2(cursorX, cursorY);
            var pillRect = new Rect(pillMin, pillMin + new Vector2(pillWidth, pillHeight));
            if (ui.Chip(pillRect, label, false))
            {
                Dalamud.Utility.Util.OpenLink(link.Url);
            }

            cursorX += pillWidth + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowCount * (pillHeight + gap) + 6f * scale));
    }

    private void ReportStation(CommunityStationDto station)
    {
        var stationId = station.Id;
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Music.ReportStationTitle),
            Submit = (reason, done) => SubmitStationReport(stationId, reason, done),
        });
    }

    private void SubmitStationReport(string stationId, string? reason, Action<bool> done)
    {
        _ = Task.Run(async () =>
        {
            var ok = false;
            try
            {
                ok = await aethernet.Safety.ReportAsync("radio_station", stationId, reason, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Radio] station report failed: {exception.Message}");
            }

            done(ok);
        });
    }
}
