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
    private const float StationHeaderHeight = 190f;
    private const float StationHeaderPillOffset = 88f;
    private const float StationHeaderNameOffset = 62f;
    private const float StationHeaderHostOffset = 30f;
    private const float StationPlayRadius = 26f;
    private const int CommunityHomeRows = 3;
    private const float CommunityShelfStatusHeight = 34f;

    private static readonly string[] LinkLabels =
    {
        "Twitch", "YouTube", "Discord", "Bluesky", "X", "Ko-fi", "Patreon",
    };

    private const int MaxStationTags = 5;
    private const int RecentTrackRows = 12;
    private const int RecentTrackPreviewRows = 5;
    private const float TrackRowHeight = 44f;
    private const float TrackSquareSize = 26f;
    private const float OnAirCardHeight = 52f;

    private readonly ChipRail tagRail = new();
    private readonly ChipRail stationTagRail = new();
    private readonly ChipRail linkRail = new();
    private readonly string[] linkLabels = new string[7];
    private readonly bool[] linkActive = new bool[7];
    private readonly string[] linkTargets = new string[7];
    private readonly string[] tagFilterLabels = new string[MaxStationTags * 8 + 1];
    private readonly bool[] tagFilterActive = new bool[MaxStationTags * 8 + 1];
    private readonly List<string> knownTags = new();
    private readonly List<CommunityStationDto> filteredStations = new();
    private string viewedStationId = string.Empty;
    private string tagFilter = string.Empty;
    private RadioTrackDto[] splitTrackSource = Array.Empty<RadioTrackDto>();
    private string[] trackTitles = Array.Empty<string>();
    private string[] trackArtists = Array.Empty<string>();
    private bool showAllTracks;

    private void OpenCommunity()
    {
        community.Refresh();
        SelectTab(MusicTab.Live);
    }

    private void OpenCommunityWithTag(string tag)
    {
        tagFilter = tag;
        tagRail.Reset();
        routers[(int)MusicTab.Live].Reset();
        tab = MusicTab.Live;
    }

    private void OpenStationPage(CommunityStationDto station)
    {
        viewedStationId = station.Id;
        showAllTracks = false;
        community.OpenStation(station.Id, station);
        Router.Push(View.Station);
    }

    private void PopStationPage()
    {
        Router.Pop();
    }

    private CommunityStationDto? ViewedStation()
    {
        return community.TryResolve(viewedStationId, out var station) ? station : null;
    }

    private bool IsCurrentCommunityStation(CommunityStationDto station)
    {
        return playback.RadioActive && playback.Radio.CurrentStationInfo.CommunityId == station.Id;
    }

    private void PlayCommunityStation(CommunityStationDto station)
    {
        playSource = Loc.T(L.Music.CommunityRadio);
        var snapshot = community.Stations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (string.Equals(snapshot[index].Id, station.Id, StringComparison.Ordinal))
            {
                playback.PlayStations(CommunityRadioService.ToQueue(snapshot), index);
                return;
            }
        }

        playback.PlayStations(new[] { CommunityRadioService.ToStation(station) }, 0);
    }

    private void DrawCommunitySection(float scale)
    {
        var stations = community.Stations;
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
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
        if (stations.Length == 0)
        {
            DrawCommunityShelfStatus(scale);
            return;
        }

        var shown = Math.Min(stations.Length, CommunityHomeRows);
        for (var index = 0; index < shown; index++)
        {
            DrawCommunityRow(scale, stations[index]);
        }
    }

    private void DrawCommunityShelfStatus(float scale)
    {
        var retryable = !community.Loading && !community.Loaded && community.IsSignedIn;
        var label = community.Loading
            ? Loc.T(L.Common.Loading)
            : community.Loaded
                ? Loc.T(L.Music.CommunityEmpty)
                : retryable
                    ? Loc.T(L.Music.CommunityOffline)
                    : Loc.T(L.Music.StationSignedOut);

        var width = ScrollLayout.StableContentWidth();
        var height = CommunityShelfStatusHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = retryable && UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var drawList = ImGui.GetWindowDrawList();
        var retry = retryable ? Loc.T(L.Common.Retry) : string.Empty;
        var retryWidth = retry.Length == 0 ? 0f : Typography.Measure(retry, TextStyles.Subheadline).X + 12f * scale;
        var fitted = Typography.FitText(label, width - retryWidth, TextStyles.Subheadline);
        var size = Typography.Measure(fitted, TextStyles.Subheadline);
        var textY = origin.Y + (height - size.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(origin.X, textY), fitted, ui.MutedInk, TextStyles.Subheadline);
        if (retry.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(max.X - retryWidth + 12f * scale, textY), retry, ui.Accent,
                TextStyles.Subheadline);
        }

        ImGui.Dummy(new Vector2(width, height));
        if (retryable && UiInteract.Click(min, max, hovered))
        {
            community.RetryDirectory();
        }
    }

    private void DrawLive(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        community.EnsureFresh(true);
        community.EnsureMine();
        DrawTopBar(context, Loc.T(L.Music.CommunityRadio), null);
        DrawMyStationEntry(content, scale);
        var body = ScrollBody(content, scale);
        var stations = community.Stations;
        if (stations.Length == 0)
        {
            DrawCommunityEmpty(body, scale);
            return;
        }

        ApplyTagFilter(stations);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawTagFilterRail(scale, stations);
            DrawLiveGroup(scale, LiveGroup.OnAir, Loc.T(L.Music.OnAirSection));
            DrawLiveGroup(scale, LiveGroup.Upcoming, Loc.T(L.Music.UpNextSection));
            DrawLiveGroup(scale, LiveGroup.Followed, Loc.T(L.Music.FollowingSection));
            DrawLiveGroup(scale, LiveGroup.Resting, Loc.T(L.Music.AllStationsSection));
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private enum LiveGroup : byte
    {
        OnAir,
        Upcoming,
        Followed,
        Resting,
    }

    private static LiveGroup GroupOf(CommunityStationDto station)
    {
        if (station.IsLive)
        {
            return LiveGroup.OnAir;
        }

        if (station.NextBroadcastAtUnix > 0)
        {
            return LiveGroup.Upcoming;
        }

        return station.IsFollowing ? LiveGroup.Followed : LiveGroup.Resting;
    }

    private void DrawLiveGroup(float scale, LiveGroup group, string heading)
    {
        var any = false;
        for (var index = 0; index < filteredStations.Count; index++)
        {
            if (GroupOf(filteredStations[index]) != group)
            {
                continue;
            }

            if (!any)
            {
                any = true;
                DrawShelfHeading(heading, scale);
            }

            DrawCommunityRow(scale, filteredStations[index]);
        }
    }

    private void ApplyTagFilter(CommunityStationDto[] stations)
    {
        filteredStations.Clear();
        for (var index = 0; index < stations.Length; index++)
        {
            if (tagFilter.Length == 0 || HasTag(stations[index], tagFilter))
            {
                filteredStations.Add(stations[index]);
            }
        }
    }

    private static bool HasTag(CommunityStationDto station, string tag)
    {
        for (var index = 0; index < station.Tags.Length; index++)
        {
            if (string.Equals(station.Tags[index], tag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawTagFilterRail(float scale, CommunityStationDto[] stations)
    {
        knownTags.Clear();
        for (var index = 0; index < stations.Length && knownTags.Count < tagFilterLabels.Length - 1; index++)
        {
            var tags = stations[index].Tags;
            for (var tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                if (tags[tagIndex].Length > 0 && !knownTags.Contains(tags[tagIndex]))
                {
                    knownTags.Add(tags[tagIndex]);
                }
            }
        }

        if (knownTags.Count == 0)
        {
            return;
        }

        tagFilterLabels[0] = Loc.T(L.Music.AllTags);
        tagFilterActive[0] = tagFilter.Length == 0;
        for (var index = 0; index < knownTags.Count; index++)
        {
            tagFilterLabels[index + 1] = knownTags[index];
            tagFilterActive[index + 1] = string.Equals(knownTags[index], tagFilter, StringComparison.OrdinalIgnoreCase);
        }

        var count = knownTags.Count + 1;
        var tapped = tagRail.Draw(ui, tagFilterLabels.AsSpan(0, count), tagFilterActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        if (tapped < 0)
        {
            return;
        }

        tagFilter = tapped == 0 || tagFilterActive[tapped] ? string.Empty : knownTags[tapped - 1];
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
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        if (!community.Loaded)
        {
            DrawCommunityFailure(body);
            return;
        }

        EmptyState.Draw(body, ui, FontAwesomeIcon.BroadcastTower, Loc.T(L.Music.CommunityEmpty),
            Loc.T(L.Music.CommunityEmptySub));
    }

    private void DrawCommunityFailure(Rect body)
    {
        if (!community.IsSignedIn)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.UserSlash, Loc.T(L.Music.StationSignedOut),
                Loc.T(L.Music.StationSignedOutSub));
            return;
        }

        if (EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Music.CommunityOffline),
                Loc.T(L.Music.StationOfflineSub), Loc.T(L.Common.Retry)))
        {
            community.RetryDirectory();
        }
    }

    private void DrawCommunityRow(float scale, CommunityStationDto station)
    {
        var rowHeight = CommunityRowHeight * scale;
        var width = ScrollLayout.StableContentWidth();
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

        var nowPlaying = NowPlayingFor(station);
        var subtitle = nowPlaying.Length > 0 ? nowPlaying : ScheduleLine(station);
        if (subtitle.Length == 0)
        {
            subtitle = station.Description;
        }

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
        float rounding, ImDrawFlags corners = ImDrawFlags.RoundCornersAll)
    {
        if (station.ArtworkUrl.Length > 0 && Thumb(station.ArtworkUrl).Texture is { } texture)
        {
            drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                corners);
            return;
        }

        drawList.AddImageRounded(artwork.HandleForName(station.Name), min, max, Vector2.Zero, Vector2.One,
            0xFFFFFFFFu, rounding, corners);
    }

    private void DrawLiveMark(ImDrawListPtr drawList, Vector2 origin, float scale, CommunityStationDto station,
        float available)
    {
        if (!station.IsLive)
        {
            var offAir = Typography.FitText(OffAirMark(station), available, TextStyles.Caption1);
            Typography.Draw(drawList, origin, offAir, ui.MutedInk, TextStyles.Caption1);
            return;
        }

        var label = string.Format(Loc.T(L.Music.ListeningCount), station.Listeners);
        LivePill.Draw(drawList, origin, LiveLabel(label), ui.Theme.Danger, clock, scale);
    }

    private static string LiveLabel(string detail) => Loc.T(L.Music.LiveBadge) + " · " + detail;

    private static string OffAirMark(CommunityStationDto station)
    {
        var offAir = Loc.T(L.Music.OffAir);
        if (station.NextBroadcastAtUnix > 0)
        {
            return offAir + " · " + TimeText.FutureMoment(station.NextBroadcastAtUnix);
        }

        if (station.LastLiveAtUnix > 0)
        {
            return string.Format(Loc.T(L.Music.LastLive), TimeText.Ago(station.LastLiveAtUnix));
        }

        return offAir;
    }

    private void DrawStationPage(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        community.EnsureFresh(true);
        var station = ViewedStation();
        if (station is null)
        {
            DrawTopBar(context, Loc.T(L.Music.CommunityRadio), PopStationPage);
            DrawStationPlaceholder(ScrollBody(content, scale), scale);
            return;
        }

        community.EnsureTracks(station.Id);
        DrawTopBar(context, string.Empty, PopStationPage);
        var body = ScrollBody(content, scale);
        using (AppSurface.Begin(body))
        {
            DrawStationHeader(scale, station);
            DrawStationActions(scale, station);
            DrawStationBody(scale, station);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawStationHeader(float scale, CommunityStationDto station)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = StationHeaderHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        DrawStationArt(drawList, min, max, station, Metrics.Radius.Card * scale, ImDrawFlags.RoundCornersTop);

        var scrimTop = new Vector2(min.X, min.Y + height * 0.32f);
        var clear = ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropTop, 0f));
        var solid = ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropTop, 0.94f));
        drawList.AddRectFilledMultiColor(scrimTop, max, clear, clear, solid, solid);

        var inset = min.X + Metrics.Space.Md * scale;
        var available = width - Metrics.Space.Md * 2f * scale;
        var pillY = max.Y - StationHeaderPillOffset * scale;
        if (station.IsLive)
        {
            var listening = string.Format(Loc.T(L.Music.ListeningCount), station.Listeners);
            LivePill.Draw(drawList, new Vector2(inset, pillY), LiveLabel(listening), ui.Theme.Danger, clock, scale);
        }
        else
        {
            var resting = Typography.FitText(StationHeaderStatus(station), available, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(inset, pillY), resting, ui.MutedInk, TextStyles.Caption1);
        }

        var nameStyle = TextStyles.Title1;
        var name = Typography.FitText(station.Name, available, nameStyle);
        Typography.Draw(drawList, new Vector2(inset, max.Y - StationHeaderNameOffset * scale), name, ui.TitleInk,
            nameStyle);
        DrawHost(drawList, station, inset, max.Y - StationHeaderHostOffset * scale, available, scale);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawStationPlaceholder(Rect body, float scale)
    {
        switch (community.StationState)
        {
            case CommunityStationLoad.NotFound:
                EmptyState.Draw(body, ui, FontAwesomeIcon.BroadcastTower, Loc.T(L.Music.StationGone),
                    Loc.T(L.Music.StationGoneSub));
                return;
            case CommunityStationLoad.SignedOut:
                EmptyState.Draw(body, ui, FontAwesomeIcon.UserSlash, Loc.T(L.Music.StationSignedOut),
                    Loc.T(L.Music.StationSignedOutSub));
                return;
            case CommunityStationLoad.Unavailable:
                if (EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Music.StationOffline),
                        Loc.T(L.Music.StationOfflineSub), Loc.T(L.Common.Retry)))
                {
                    community.RetryStation();
                }

                return;
            default:
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
                return;
        }
    }

    private void DrawHost(ImDrawListPtr drawList, CommunityStationDto station, float left, float top, float width,
        float scale)
    {
        var display = station.OwnerDisplayName.Length > 0
            ? station.OwnerDisplayName
            : station.OwnerHandle.Length > 0
                ? "@" + station.OwnerHandle
                : string.Empty;
        if (display.Length == 0)
        {
            return;
        }

        var label = string.Format(Loc.T(L.Music.HostedBy), display);
        var radius = 11f * scale;
        var gap = 7f * scale;
        var available = width - 32f * scale - radius * 2f - gap;
        var fitted = Typography.FitText(label, available, TextStyles.Caption1);
        var rowLeft = left;
        var center = new Vector2(rowLeft + radius, top + radius);

        if (station.OwnerAvatarUrl.Length > 0 && Thumb(station.OwnerAvatarUrl, radius * 2f).Texture is { } avatar)
        {
            drawList.AddImageRounded(avatar.Handle, center - new Vector2(radius, radius),
                center + new Vector2(radius, radius), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, radius,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.FieldSurface), 24);
            var initials = Initials.Of(display);
            var initialsSize = Typography.Measure(initials, TextStyles.Caption2);
            Typography.Draw(drawList, new Vector2(center.X - initialsSize.X * 0.5f, center.Y - initialsSize.Y * 0.5f),
                initials, ui.MutedInk, TextStyles.Caption2);
        }

        UserName.DrawAuto(drawList, "music.station.host", fitted, station.OwnerBadges, station.OwnerBadgeIds,
            rowLeft + radius * 2f + gap, top + radius - Typography.Measure(fitted, TextStyles.Caption1).Y * 0.5f,
            available, TextStyles.Caption1, ui.MutedInk, theme);
    }

    private static string StationHeaderStatus(CommunityStationDto station)
    {
        var resting = OffAirMark(station);
        if (station.Followers == 0)
        {
            return resting;
        }

        return resting + " · " + Loc.Plural(L.Music.StationFollowers, station.Followers);
    }

    private static string ScheduleLine(CommunityStationDto station)
    {
        if (station.NextBroadcastAtUnix <= 0)
        {
            return string.Empty;
        }

        return string.Format(Loc.T(L.Music.NextBroadcast), TimeText.FutureMoment(station.NextBroadcastAtUnix));
    }

    private void DrawStationActions(float scale, CommunityStationDto station)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var radius = StationPlayRadius * scale;
        var rowHeight = radius * 2f;
        var current = IsCurrentCommunityStation(station);
        var playable = station.IsLive || current;
        var owned = community.Mine is { } mine && string.Equals(mine.Station.Id, station.Id, StringComparison.Ordinal);
        var playCenter = new Vector2(origin.X + width - radius - Metrics.Space.Md * scale, origin.Y + radius);

        if (!owned)
        {
            var followLabel = station.IsFollowing ? Loc.T(L.Music.FollowingStation) : Loc.T(L.Music.FollowStation);
            if (!playable && !station.IsFollowing)
            {
                followLabel = Loc.T(L.Music.NotifyWhenLive);
            }

            var followWidth = MathF.Min(Typography.Measure(followLabel, TextStyles.Callout).X + 34f * scale,
                width - radius * 2f - Metrics.Space.Xl * scale);
            var followMin = new Vector2(origin.X + Metrics.Space.Md * scale, origin.Y + radius - 18f * scale);
            var followRect = new Rect(followMin, followMin + new Vector2(followWidth, 36f * scale));
            if (ui.GhostButton(followRect, followLabel))
            {
                community.ToggleFollow(station);
            }
        }

        if (playable)
        {
            if (MusicRenderer.PlayButton("music.station.play", playCenter, radius, ui.Accent, ui.Palette.BackdropBottom,
                    current && playback.IsPlaying))
            {
                if (current)
                {
                    playback.TogglePlayPause();
                }
                else
                {
                    PlayCommunityStation(station);
                }
            }
        }
        else
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddCircleFilled(playCenter, radius, ImGui.GetColorU32(ui.FieldSurface), 32);
            AppSkin.Icon(drawList, playCenter, FontAwesomeIcon.Bell.ToIconString(), ui.MutedInk, 1f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Md * scale));
    }

    private const int TwitchLinkKind = 0;

    private static string LinkUrl(CommunityStationDto station, int kind)
    {
        for (var index = 0; index < station.Links.Length; index++)
        {
            if (station.Links[index].Kind == kind)
            {
                return station.Links[index].Url;
            }
        }

        return string.Empty;
    }

    /// A broadcaster who streams on Twitch wants their audience there, not here, so their channel
    /// gets a real button rather than one pill among seven. It opens Twitch and plays nothing.
    private void DrawWatchOnTwitch(float scale, CommunityStationDto station)
    {
        var url = LinkUrl(station, TwitchLinkKind);
        if (url.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var buttonWidth = MathF.Min(width - 32f * scale, 220f * scale);
        var buttonMin = new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y);
        var buttonRect = new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, 36f * scale));
        if (ui.GhostButton(buttonRect, Loc.T(L.Music.WatchOnTwitch)))
        {
            Dalamud.Utility.Util.OpenLink(url);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 46f * scale));
    }

    private void DrawStationBody(float scale, CommunityStationDto station)
    {
        var width = ScrollLayout.StableContentWidth();
        DrawWatchOnTwitch(scale, station);
        DrawStationTagRail(scale, station);
        var track = NowPlayingFor(station);
        if (track.Length > 0)
        {
            DrawOnAirCard(scale, track);
        }

        if (!station.IsLive && ScheduleLine(station) is { Length: > 0 } schedule)
        {
            DrawStationParagraph(scale, schedule, ui.TitleInk, TextStyles.Callout, width);
        }

        if (station.Description.Length > 0)
        {
            DrawStationParagraph(scale, station.Description, ui.BodyInk, TextStyles.Subheadline, width);
        }

        DrawStationLinks(scale, station);
        DrawRecentTracks(scale, width);

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

    private void DrawStationTagRail(float scale, CommunityStationDto station)
    {
        if (station.Tags.Length == 0)
        {
            return;
        }

        var count = Math.Min(station.Tags.Length, MaxStationTags);
        for (var index = 0; index < count; index++)
        {
            tagFilterLabels[index] = station.Tags[index];
            tagFilterActive[index] = false;
        }

        var tapped = stationTagRail.Draw(ui, tagFilterLabels.AsSpan(0, count), tagFilterActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (tapped >= 0)
        {
            OpenCommunityWithTag(station.Tags[tapped]);
        }
    }

    private void DrawOnAirCard(float scale, string track)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = OnAirCardHeight * scale;
        var min = new Vector2(origin.X + Metrics.Space.Md * scale, origin.Y);
        var max = new Vector2(origin.X + width - Metrics.Space.Md * scale, origin.Y + height);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Md * scale, ImGui.GetColorU32(ui.Palette.CardFill));
        var lampCenter = new Vector2(min.X + 20f * scale, (min.Y + max.Y) * 0.5f);
        Equalizer.Draw(drawList, lampCenter, scale, 16f * scale, clock, ui.Accent, 1f, playback.IsPlaying);
        var textLeft = lampCenter.X + 18f * scale;
        var available = max.X - Metrics.Space.Md * scale - textLeft;
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + 10f * scale), Loc.T(L.Music.OnAirNow), ui.MutedInk,
            TextStyles.Caption2);
        var fitted = Typography.FitText(track, available, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + 26f * scale), fitted, ui.TitleInk,
            TextStyles.BodyEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void EnsureTrackSplits()
    {
        var source = community.Tracks;
        if (ReferenceEquals(source, splitTrackSource))
        {
            return;
        }

        splitTrackSource = source;
        trackTitles = new string[source.Length];
        trackArtists = new string[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var raw = source[index].Title;
            var cut = raw.IndexOf(" - ", StringComparison.Ordinal);
            if (cut <= 0)
            {
                trackTitles[index] = raw;
                trackArtists[index] = string.Empty;
                continue;
            }

            trackArtists[index] = raw[..cut];
            trackTitles[index] = raw[(cut + 3)..];
        }
    }

    private void DrawRecentTracks(float scale, float width)
    {
        if (community.TracksLoading)
        {
            DrawShelfHeading(Loc.T(L.Music.LastPlayed), scale);
            InfiniteScroll.DrawLoadingRow(ImGui.GetCursorScreenPos().X + width * 0.5f, ui.MutedInk);
            return;
        }

        var recent = community.Tracks;
        if (recent.Length == 0)
        {
            return;
        }

        EnsureTrackSplits();
        DrawShelfHeading(Loc.T(L.Music.LastPlayed), scale);
        var shown = Math.Min(recent.Length, showAllTracks ? RecentTrackRows : RecentTrackPreviewRows);
        for (var index = 0; index < shown; index++)
        {
            DrawTrackRow(scale, recent[index], index);
        }

        if (showAllTracks || recent.Length <= RecentTrackPreviewRows)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = TrackRowHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var label = Loc.T(L.Music.ShowAll);
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(min.X + Metrics.Space.Md * scale, min.Y + (rowHeight - size.Y) * 0.5f), label, ui.Accent,
            TextStyles.SubheadlineEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (UiInteract.Click(min, max, hovered))
        {
            showAllTracks = true;
        }
    }

    private void DrawTrackRow(float scale, RadioTrackDto track, int index)
    {
        var rowHeight = TrackRowHeight * scale;
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 8f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var squareSize = TrackSquareSize * scale;
        var squareMin = new Vector2(min.X + Metrics.Space.Md * scale, min.Y + (rowHeight - squareSize) * 0.5f);
        drawList.AddImageRounded(artwork.HandleForName(track.Title), squareMin,
            squareMin + new Vector2(squareSize, squareSize), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, 6f * scale,
            ImDrawFlags.RoundCornersAll);

        var stamp = TimeText.Ago(track.PlayedAtUnix);
        var stampWidth = Typography.Measure(stamp, TextStyles.Caption2).X;
        var textLeft = squareMin.X + squareSize + 10f * scale;
        var textWidth = max.X - Metrics.Space.Md * scale - stampWidth - 10f * scale - textLeft;
        var artist = index < trackArtists.Length ? trackArtists[index] : string.Empty;
        var title = index < trackTitles.Length ? trackTitles[index] : track.Title;
        if (artist.Length == 0)
        {
            var single = Typography.FitText(title, textWidth, TextStyles.Subheadline);
            var singleSize = Typography.Measure(single, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + (rowHeight - singleSize.Y) * 0.5f), single,
                ui.BodyInk, TextStyles.Subheadline);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + 7f * scale),
                Typography.FitText(title, textWidth, TextStyles.Subheadline), ui.BodyInk, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + 24f * scale),
                Typography.FitText(artist, textWidth, TextStyles.Caption2), ui.MutedInk, TextStyles.Caption2);
        }

        Typography.Draw(drawList, new Vector2(max.X - Metrics.Space.Md * scale - stampWidth,
            min.Y + (rowHeight - Typography.Measure(stamp, TextStyles.Caption2).Y) * 0.5f), stamp, ui.MutedInk,
            TextStyles.Caption2);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (UiInteract.Click(min, max, hovered))
        {
            SearchForTrack(track.Title);
        }
    }

    private void SearchForTrack(string title)
    {
        searchDraft = title;
        BeginSearch(title);
        Router.Reset();
        Router.Push(View.Search, false);
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

    /// While we are the one playing, the stream's own metadata beats the directory snapshot, which
    /// is up to ten seconds behind and blank for stations the server sees no title for.
    private string NowPlayingFor(CommunityStationDto station)
    {
        if (IsCurrentCommunityStation(station) && playback.RadioNowPlaying.Length > 0)
        {
            return playback.RadioNowPlaying;
        }

        return station.IsLive ? station.NowPlaying : string.Empty;
    }

    private void DrawStationLinks(float scale, CommunityStationDto station)
    {
        if (station.Links.Length == 0)
        {
            return;
        }

        var count = 0;
        for (var index = 0; index < station.Links.Length && count < linkLabels.Length; index++)
        {
            var link = station.Links[index];
            if (link.Kind < 0 || link.Kind >= LinkLabels.Length || link.Kind == TwitchLinkKind)
            {
                continue;
            }

            linkLabels[count] = LinkLabels[link.Kind];
            linkActive[count] = false;
            linkTargets[count] = link.Url;
            count++;
        }

        if (count == 0)
        {
            return;
        }

        var tapped = linkRail.Draw(ui, linkLabels.AsSpan(0, count), linkActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        if (tapped >= 0)
        {
            Dalamud.Utility.Util.OpenLink(linkTargets[tapped]);
        }
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
                AepLog.Warning(exception, "[Radio] station report failed");
            }

            done(ok);
        });
    }
}
