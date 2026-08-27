using Aetherphone.Apps.Music.Rolladeck;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Utility;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int LiveDjHomeRows = 3;
    private const float LiveDjHomeRowH = 68f;
    private const float LiveDjCardH = 72f;
    private const float LiveDjCardGap = 8f;
    private const float LiveDjHPad = 12f;
    private const float LiveDjVPad = 8f;
    private const float LiveDjAvatarR = 18f;
    private const float LiveDjDetailAvatarR = 40f;
    private const float LiveDjPillH = 36f;
    private const float LiveDjPillGap = 8f;

    private readonly List<LiveDjEntry> sortedDjs = new();
    private readonly List<LiveDjEntry> sortedDjsAtVenue = new();
    private readonly ChipRail liveDjChipRail = new();
    private readonly List<(string Label, FontAwesomeIcon Icon, string Url)> liveDjSocialLinks = new(8);
    private readonly List<string> sortedDjHomeKeys = new();
    private readonly List<string> sortedDjHomeTitleKeys = new();
    private readonly List<string> sortedDjViewerCounts = new();
    private readonly List<string> sortedDjCardKeys = new();
    private readonly List<string> sortedDjCardTitleKeys = new();
    private readonly List<string> sortedDjsAtVenueCardKeys = new();
    private readonly List<string> sortedDjsAtVenueCardTitleKeys = new();
    private readonly List<string> sortedDjsAtVenueViewerCounts = new();
    private static readonly string[] LiveDjSocialButtonIds =
    [
        "music.liveDj.soc.0", "music.liveDj.soc.1", "music.liveDj.soc.2", "music.liveDj.soc.3",
        "music.liveDj.soc.4", "music.liveDj.soc.5", "music.liveDj.soc.6", "music.liveDj.soc.7",
    ];
    private IReadOnlyList<LiveDjEntry> cachedDjSource = [];
    private LiveDjEntry? selectedDj;
    private string? lastDjDetailKey;
    private float djScrollY;
    private OpenVenueEntry? selectedVenue;
    private string? lastVenueDetailKey;
    private float venueScrollY;
    private bool liveDjLifestreamAvailable;
    private string cachedDjEventLabel = string.Empty;
    private string cachedDjViewerCount = string.Empty;
    private int liveDjFilter;

    private void OnLiveDjsOpened()
    {
        liveDjLifestreamAvailable = LifestreamBridge.IsAvailable();
    }

    private void EnsureDjList()
    {
        var source = rolladeck.LiveDJs;
        if (ReferenceEquals(source, cachedDjSource))
        {
            return;
        }

        cachedDjSource = source;
        sortedDjs.Clear();
        sortedDjsAtVenue.Clear();
        for (var djIndex = 0; djIndex < source.Count; djIndex++)
        {
            var dj = source[djIndex];
            sortedDjs.Add(dj);
            if (dj.VenueName != null)
            {
                sortedDjsAtVenue.Add(dj);
            }
        }

        sortedDjs.Sort(CompareDjs);
        sortedDjsAtVenue.Sort(CompareDjs);

        sortedDjHomeKeys.Clear();
        sortedDjHomeTitleKeys.Clear();
        sortedDjViewerCounts.Clear();
        sortedDjCardKeys.Clear();
        sortedDjCardTitleKeys.Clear();
        for (var keyIndex = 0; keyIndex < sortedDjs.Count; keyIndex++)
        {
            var slug = sortedDjs[keyIndex].DjSlug ?? sortedDjs[keyIndex].DjName;
            sortedDjHomeKeys.Add("music.liveDj.home." + slug);
            sortedDjHomeTitleKeys.Add("music.liveDj.home.title." + slug);
            sortedDjViewerCounts.Add(sortedDjs[keyIndex].ViewerCount.ToString("N0"));
            sortedDjCardKeys.Add("music.djCard." + slug);
            sortedDjCardTitleKeys.Add("music.djCard.title." + slug);
        }

        sortedDjsAtVenueCardKeys.Clear();
        sortedDjsAtVenueCardTitleKeys.Clear();
        sortedDjsAtVenueViewerCounts.Clear();
        for (var keyIndex = 0; keyIndex < sortedDjsAtVenue.Count; keyIndex++)
        {
            var slug = sortedDjsAtVenue[keyIndex].DjSlug ?? sortedDjsAtVenue[keyIndex].DjName;
            sortedDjsAtVenueCardKeys.Add("music.djCard." + slug);
            sortedDjsAtVenueCardTitleKeys.Add("music.djCard.title." + slug);
            sortedDjsAtVenueViewerCounts.Add(sortedDjsAtVenue[keyIndex].ViewerCount.ToString("N0"));
        }
    }

    private int CompareDjs(LiveDjEntry first, LiveDjEntry second)
    {
        var tierDiff = ProximityTier(first.Server, first.Datacenter)
                      .CompareTo(ProximityTier(second.Server, second.Datacenter));
        if (tierDiff != 0)
        {
            return tierDiff;
        }

        return second.ViewerCount.CompareTo(first.ViewerCount);
    }

    private int ProximityTier(string? server, string? datacenter)
    {
        var localId = gameData.LocalCurrentWorldId;
        var playerWorld = localId > 0 ? gameData.WorldName(localId) : null;
        var playerDc = localId > 0 ? gameData.DataCenterName(localId) : null;

        if (!string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(playerWorld) &&
            string.Equals(server, playerWorld, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (!string.IsNullOrEmpty(datacenter) && !string.IsNullOrEmpty(playerDc) &&
            string.Equals(datacenter, playerDc, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (!string.IsNullOrEmpty(datacenter) && !string.IsNullOrEmpty(playerDc))
        {
            var djRegion = HousingRegions.For(datacenter);
            if (djRegion != HousingRegions.Unknown &&
                string.Equals(djRegion, HousingRegions.For(playerDc), StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
        }

        return 3;
    }

    private void OpenLiveDjs()
    {
        Router.Push(View.LiveDjs);
    }

    private void OpenLiveDjDetail(LiveDjEntry dj)
    {
        selectedDj = dj;
        Router.Push(View.LiveDjDetail);
    }

    private void OpenVenueDetail(OpenVenueEntry venue)
    {
        selectedVenue = venue;
        Router.Push(View.VenueDetail);
    }

    private OpenVenueEntry? FindVenueForDj(LiveDjEntry dj)
    {
        if (dj.VenueName == null)
        {
            return null;
        }

        var venues = rolladeck.OpenVenues;
        for (var venueIndex = 0; venueIndex < venues.Count; venueIndex++)
        {
            if (string.Equals(venues[venueIndex].DisplayName, dj.VenueName, StringComparison.OrdinalIgnoreCase))
            {
                return venues[venueIndex];
            }
        }

        return null;
    }

    private void DrawLiveDjsSkeleton(float scale)
    {
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var titleSize = Typography.Measure("Ag", TextStyles.Title3);
        var fill = Skeleton.Fill();
        var drawList = ImGui.GetWindowDrawList();

        Squircle.Fill(drawList, origin, new Vector2(origin.X + width * 0.38f, origin.Y + titleSize.Y), 4f * scale, fill);
        ImGui.Dummy(new Vector2(0f, titleSize.Y + 8f * scale));

        var rowHeight = LiveDjHomeRowH * scale;
        var avatarRadius = 22f * scale;
        for (var rowIndex = 0; rowIndex < LiveDjHomeRows; rowIndex++)
        {
            var rowOrigin = ImGui.GetCursorScreenPos();
            var avatarCenter = new Vector2(rowOrigin.X + 6f * scale + avatarRadius, rowOrigin.Y + rowHeight * 0.5f);
            drawList.AddCircleFilled(avatarCenter, avatarRadius, fill, 32);

            var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
            var nameY = rowOrigin.Y + 9f * scale;
            var nameH = Typography.Measure("Ag", TextStyles.FootnoteEmphasized).Y;
            var captionH = Typography.Measure("Ag", TextStyles.Caption1).Y;

            Squircle.Fill(drawList, new Vector2(textLeft, nameY),
                new Vector2(textLeft + width * 0.42f, nameY + nameH), 4f * scale, fill);
            Squircle.Fill(drawList, new Vector2(width * 0.62f, nameY),
                new Vector2(width * 0.88f, nameY + captionH), 3f * scale, fill);

            var line2Y = nameY + nameH + 3f * scale;
            Squircle.Fill(drawList, new Vector2(textLeft, line2Y),
                new Vector2(textLeft + width * 0.30f, line2Y + captionH), 3f * scale, fill);

            ImGui.Dummy(new Vector2(width, rowHeight));
        }
    }

    private void DrawLiveDjsEmpty(float scale)
    {
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var width = ImGui.GetContentRegionAvail().X;
        var title = Typography.FitText(Loc.T(L.Music.LiveDjs), width, TextStyles.Title3);
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(origin, title, ui.Palette.HeadingInk, TextStyles.Title3);
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        var textOrigin = ImGui.GetCursorScreenPos();
        var textHeight = Typography.DrawWrappedLeft(textOrigin, Loc.T(L.Music.LiveDjsEmpty),
            ui.MutedInk, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, textHeight + 8f * scale));
    }

    private void DrawLiveDjsSection(float scale)
    {
        EnsureDjList();
        if (sortedDjs.Count == 0)
        {
            if (rolladeck.Loading)
            {
                DrawLiveDjsSkeleton(scale);
            }
            else if (rolladeck.HasData)
            {
                DrawLiveDjsEmpty(scale);
            }

            return;
        }

        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var iconBox = 30f * scale;
        var title = Typography.FitText(Loc.T(L.Music.LiveDjs), width - iconBox - 8f * scale, TextStyles.Title3);
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
            OpenLiveDjs();
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));

        var rowWidth = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();

        var withVenueCount = 0;
        for (var djIndex = 0; djIndex < sortedDjs.Count; djIndex++)
        {
            var dj = sortedDjs[djIndex];
            if (dj.VenueName != null || dj.FormattedAddress.Length > 0)
            {
                withVenueCount++;
            }
        }

        var drawn = 0;
        for (var djIndex = 0; djIndex < sortedDjs.Count; djIndex++)
        {
            if (drawn >= LiveDjHomeRows)
            {
                break;
            }

            var dj = sortedDjs[djIndex];
            if (dj.VenueName != null || dj.FormattedAddress.Length > 0)
            {
                DrawLiveDjHomeRow(drawList, scale, rowWidth, dj, sortedDjHomeKeys[djIndex], sortedDjHomeTitleKeys[djIndex], sortedDjViewerCounts[djIndex]);
                drawn++;
            }
        }

        if (withVenueCount < 3)
        {
            for (var djIndex = 0; djIndex < sortedDjs.Count; djIndex++)
            {
                if (drawn >= LiveDjHomeRows)
                {
                    break;
                }

                var dj = sortedDjs[djIndex];
                if (dj.VenueName == null && dj.FormattedAddress.Length == 0)
                {
                    DrawLiveDjHomeRow(drawList, scale, rowWidth, dj, sortedDjHomeKeys[djIndex], sortedDjHomeTitleKeys[djIndex], sortedDjViewerCounts[djIndex]);
                    drawn++;
                }
            }
        }
    }

    private void DrawLiveDjHomeRow(ImDrawListPtr drawList, float scale, float width, LiveDjEntry dj, string rowKey, string titleKey, string viewerCount)
    {
        var rowHeight = LiveDjHomeRowH * scale;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarRadius = 22f * scale;
        var avatarCenter = new Vector2(min.X + 6f * scale + avatarRadius, (min.Y + max.Y) * 0.5f);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme,
            dj.DjName, dj.Server ?? string.Empty, dj.AvatarUrl, images, lodestone, 0.85f, 44);

        var viewersText = string.Format(Loc.T(L.Rolladeck.Viewers), viewerCount);
        var viewerSize = Typography.Measure(viewersText, TextStyles.Caption1);
        var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
        var rightReserve = viewerSize.X + 14f * scale;
        var textWidth = max.X - rightReserve - textLeft;
        var nameY = min.Y + 9f * scale;
        var nameSize = Typography.Measure("Ag", TextStyles.FootnoteEmphasized);
        var captionSize = Typography.Measure("Ag", TextStyles.Caption1);

        Marquee.DrawLeft(rowKey,
            dj.NormalizedName, textLeft, nameY, textWidth, TextStyles.FootnoteEmphasized, ui.TitleInk, hovered);

        Typography.Draw(drawList,
            new Vector2(max.X - 12f * scale - viewerSize.X, nameY + (nameSize.Y - captionSize.Y) * 0.5f),
            viewersText, ui.Accent, TextStyles.Caption1);

        var line2Y = nameY + nameSize.Y + 3f * scale;
        var venueLabel = dj.VenueName ?? dj.FormattedAddress;
        if (!string.IsNullOrEmpty(venueLabel))
        {
            var fitted = Typography.FitText(venueLabel, textWidth, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(textLeft, line2Y), fitted, ui.MutedInk, TextStyles.Caption1);
        }

        if (!string.IsNullOrEmpty(dj.ServerLabel))
        {
            var serverSize = Typography.Measure(dj.ServerLabel, TextStyles.Caption1);
            Typography.Draw(drawList,
                new Vector2(max.X - 12f * scale - serverSize.X, line2Y),
                dj.ServerLabel, ui.MutedInk, TextStyles.Caption1);
        }

        if (!string.IsNullOrEmpty(dj.NormalizedTitle))
        {
            var line3Y = line2Y + captionSize.Y + 2f * scale;
            Marquee.DrawLeft(titleKey,
                dj.NormalizedTitle, textLeft, line3Y, textWidth,
                TextStyles.Caption1, ui.MutedInk, hovered);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));

        if (UiInteract.Click(min, max, hovered))
        {
            OpenLiveDjDetail(dj);
        }
    }

    private void DrawLiveDjs(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        DrawTopBar(context, Loc.T(L.Music.LiveDjs), () => Router.Pop());

        var chipH = (ChipRail.RowHeight + 8f) * scale;
        var chipRect = new Rect(
            new Vector2(content.Min.X, content.Min.Y + TopBarHeight * scale),
            new Vector2(content.Max.X, content.Min.Y + TopBarHeight * scale + chipH));

        ReadOnlySpan<string> filterLabels = [Loc.T(L.Rolladeck.FilterAtVenue), Loc.T(L.Rolladeck.FilterAllDjs)];
        ReadOnlySpan<bool> filterActive = [liveDjFilter == 0, liveDjFilter == 1];
        var filterTapped = liveDjChipRail.Draw(chipRect, ui, filterLabels, filterActive, overlay: false);
        if (filterTapped >= 0)
        {
            liveDjFilter = filterTapped;
        }

        var body = new Rect(
            new Vector2(content.Min.X, chipRect.Max.Y),
            new Vector2(content.Max.X, BodyBottom(content, scale)));

        EnsureDjList();

        var djs = liveDjFilter == 0 ? sortedDjsAtVenue : sortedDjs;
        var djCardKeys = liveDjFilter == 0 ? sortedDjsAtVenueCardKeys : sortedDjCardKeys;
        var djCardTitleKeys = liveDjFilter == 0 ? sortedDjsAtVenueCardTitleKeys : sortedDjCardTitleKeys;
        var djViewerCounts = liveDjFilter == 0 ? sortedDjsAtVenueViewerCounts : sortedDjViewerCounts;

        if (rolladeck.Loading && !rolladeck.HasData)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Center.Y - 14f * scale), 13f * scale, ui.Accent,
                ui.MutedInk, Loc.T(L.Common.Loading));
            return;
        }

        if (djs.Count == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Rolladeck.EmptyDjsHeading), ui.MutedInk, TextStyles.Callout);
            return;
        }

        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            ImGui.Dummy(new Vector2(1f, LiveDjVPad * scale));

            for (var djIndex = 0; djIndex < djs.Count; djIndex++)
            {
                var origin = ImGui.GetCursorScreenPos();
                var fullWidth = ImGui.GetContentRegionAvail().X;
                var cardMin = new Vector2(origin.X + LiveDjHPad * scale, origin.Y);
                var cardMax = new Vector2(origin.X + fullWidth - LiveDjHPad * scale, origin.Y + LiveDjCardH * scale);
                var card = new Rect(cardMin, cardMax);

                if (ImGui.IsRectVisible(card.Min, card.Max))
                {
                    DrawLiveDjCard(drawList, card, djs[djIndex], djCardKeys[djIndex], djCardTitleKeys[djIndex], djViewerCounts[djIndex], scale);
                }

                var gap = djIndex < djs.Count - 1 ? LiveDjCardGap : LiveDjVPad;
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(fullWidth, (LiveDjCardH + gap) * scale));
            }

            ImGui.Dummy(new Vector2(1f, 10f * scale));
            var footerCursor = ImGui.GetCursorScreenPos();
            var footerWidth = ImGui.GetContentRegionAvail().X;
            DrawRolladeckFooter(drawList, scale, footerWidth, footerCursor);
            ImGui.Dummy(new Vector2(footerWidth, 22f * scale + 14f * scale));
        }
    }

    private void DrawLiveDjCard(ImDrawListPtr drawList, Rect card, LiveDjEntry dj, string cardKey, string titleKey, string viewerCount, float scale)
    {
        var hovered = UiInteract.Hover(card.Min, card.Max);
        var fill = hovered
            ? Palette.Mix(ui.Palette.CardFill, Vector4.One, 0.09f)
            : ui.Palette.CardFill;

        Squircle.Fill(drawList, card.Min, card.Max, 10f * scale, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, card.Min, card.Max, 10f * scale, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarRadius = LiveDjAvatarR * scale;
        var avatarCenter = new Vector2(card.Min.X + LiveDjHPad * scale + avatarRadius,
                                       card.Min.Y + card.Height * 0.5f);

        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme,
            dj.DjName, dj.Server ?? string.Empty, dj.AvatarUrl, images, lodestone, 0.85f, 44);

        var textLeft = avatarCenter.X + avatarRadius + 8f * scale;
        var topY = card.Min.Y + 9f * scale;
        var maxTextWidth = card.Max.X - textLeft - 80f * scale;

        Marquee.DrawLeft(cardKey,
            dj.NormalizedName, textLeft, topY, maxTextWidth,
            TextStyles.SubheadlineEmphasized, ui.Palette.TitleInk, hovered);

        var nameHeight = Typography.Measure("Ag", TextStyles.SubheadlineEmphasized).Y;
        var currentY = topY + nameHeight + 3f * scale;

        var subLabel = dj.VenueName ?? dj.FormattedAddress;
        if (!string.IsNullOrEmpty(subLabel))
        {
            var subHeight = Typography.Measure("Ag", TextStyles.Footnote).Y;
            var sub = Typography.FitText(subLabel, maxTextWidth, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(textLeft, currentY), sub, ui.Palette.BodyInk, TextStyles.Footnote);
            currentY += subHeight + 2f * scale;
        }

        if (!string.IsNullOrEmpty(dj.NormalizedTitle))
        {
            Marquee.DrawLeft(titleKey,
                dj.NormalizedTitle, textLeft, currentY, maxTextWidth,
                TextStyles.Caption1, ui.Palette.MutedInk, hovered);
        }

        var viewersText = string.Format(Loc.T(L.Rolladeck.Viewers), viewerCount);
        var viewerSize = Typography.Measure(viewersText, TextStyles.Caption1);
        var viewersRightX = card.Max.X - 12f * scale - viewerSize.X;
        Typography.Draw(drawList,
            new Vector2(viewersRightX, card.Min.Y + 9f * scale),
            viewersText, ui.Palette.Accent, TextStyles.Caption1);

        if (!string.IsNullOrEmpty(dj.ServerLabel))
        {
            var serverSize = Typography.Measure(dj.ServerLabel, TextStyles.Caption1);
            Typography.Draw(drawList,
                new Vector2(card.Max.X - 12f * scale - serverSize.X, card.Min.Y + 9f * scale + viewerSize.Y + 3f * scale),
                dj.ServerLabel, ui.Palette.MutedInk, TextStyles.Caption1);
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            OpenLiveDjDetail(dj);
        }
    }

    private void DrawLiveDjDetail(in PhoneContext context)
    {
        var dj = selectedDj;
        if (dj == null)
        {
            Router.Pop();
            return;
        }

        var area = context.Content;
        var scale = UiScale.Current;
        DrawTopBar(context, string.Empty, () => Router.Pop());

        var body = new Rect(
            new Vector2(area.Min.X, area.Min.Y + TopBarHeight * scale),
            area.Max);

        var drawList = ImGui.GetWindowDrawList();
        var venueEntry = FindVenueForDj(dj);
        var venueLogoUrl = venueEntry?.LogoUrl;
        var djKey = dj.DjSlug ?? dj.DjName;
        var isNewDj = djKey != lastDjDetailKey;
        if (isNewDj)
        {
            lastDjDetailKey = djKey;
            cachedDjEventLabel = string.IsNullOrEmpty(dj.EventName) ? string.Empty : "♦ " + dj.EventName;
            cachedDjViewerCount = dj.ViewerCount.ToString("N0");
        }

        using (AppSurface.Begin(body))
        {
            if (isNewDj)
            {
                ImGui.SetScrollY(0f);
            }

            var scrollY = ImGui.GetScrollY();
            djScrollY = isNewDj ? 0f : scrollY;

            var rawOrigin = ImGui.GetCursorScreenPos();
            var origin = isNewDj
                ? new Vector2(rawOrigin.X, rawOrigin.Y + scrollY)
                : rawOrigin;
            var width = ImGui.GetContentRegionAvail().X;
            var padX = 16f * scale;
            var contentWidth = width - padX * 2f;

            var avatarRadius = LiveDjDetailAvatarR * scale;
            var avatarCenter = new Vector2(origin.X + width * 0.5f, origin.Y + 20f * scale + avatarRadius);

            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme,
                dj.DjName, dj.Server ?? string.Empty, dj.AvatarUrl, images, lodestone, 1.0f, 80);

            var djHeaderName = Typography.FitText(dj.NormalizedName, contentWidth * 0.75f, TextStyles.Headline);
            var djHeaderSize = Typography.Measure(djHeaderName, TextStyles.Headline);
            Typography.Draw(drawList,
                new Vector2(origin.X + (width - djHeaderSize.X) * 0.5f, avatarCenter.Y + avatarRadius + 8f * scale),
                djHeaderName, ui.Palette.TitleInk, TextStyles.Headline);

            var djHeaderH = 20f * scale + avatarRadius * 2f + 8f * scale + djHeaderSize.Y + 16f * scale;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, djHeaderH));

            var actionRadius = StationPlayRadius * scale;
            var actionRowH = actionRadius * 2f;
            var actionRowY = ImGui.GetCursorScreenPos().Y;

            if (dj.CanTeleport)
            {
                var teleportLabel = Loc.T(L.Rolladeck.Teleport);
                var teleportWidth = MathF.Min(
                    Typography.Measure(teleportLabel, TextStyles.Callout).X + 34f * scale,
                    contentWidth - actionRadius * 2f - padX);
                var teleportMin = new Vector2(origin.X + padX, actionRowY + actionRadius - 18f * scale);
                var teleportRect = new Rect(teleportMin, teleportMin + new Vector2(teleportWidth, 36f * scale));
                if (ui.GhostButton(teleportRect, teleportLabel))
                {
                    if (liveDjLifestreamAvailable)
                    {
                        LifestreamBridge.Travel(dj.LifestreamArg!);
                    }
                    else
                    {
                        ImGui.SetClipboardText(LifestreamBridge.TravelCommand(dj.LifestreamArg!));
                        ShellToast.Show();
                    }
                }

                if (!liveDjLifestreamAvailable)
                {
                    HoverTooltip.Show(teleportRect, Loc.T(L.Rolladeck.LifestreamNotInstalled), HoverLabelSide.Above);
                }
            }

            if (dj.TwitchUrl != null)
            {
                var watchCenter = new Vector2(origin.X + width - actionRadius - padX, actionRowY + actionRadius);
                var watchMin = watchCenter - new Vector2(actionRadius, actionRadius);
                var watchMax = watchCenter + new Vector2(actionRadius, actionRadius);
                var watchHovered = UiInteract.Hover(watchMin, watchMax);
                var watchFill = watchHovered ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.92f);
                drawList.AddCircleFilled(watchCenter, actionRadius, ImGui.GetColorU32(watchFill), 32);
                AppSkin.Icon(drawList, watchCenter, FontAwesomeIcon.Play.ToIconString(), ui.Palette.BackdropBottom, 1f);
                if (watchHovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (watchHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    Util.OpenLink(dj.RolladeckUrl ?? dj.TwitchUrl ?? "https://xivrolladeck.com");
                }
            }

            ImGui.Dummy(new Vector2(width, actionRowH + padX));

            var cursorY = ImGui.GetCursorScreenPos().Y;
            var infoCardH = OnAirCardHeight * scale;
            var infoMin = new Vector2(origin.X + padX, cursorY);
            var infoMax = new Vector2(origin.X + padX + contentWidth, cursorY + infoCardH);
            var infoHovered = venueEntry != null && UiInteract.Hover(infoMin, infoMax);
            ui.Card(drawList, infoMin, infoMax, 10f * scale, elevated: false);
            if (infoHovered)
            {
                Squircle.Fill(drawList, infoMin, infoMax, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var logoRadius = 14f * scale;
            var logoCenterX = infoMin.X + 14f * scale + logoRadius;
            var logoCenterY = infoMin.Y + infoCardH * 0.5f;
            var logoCenter = new Vector2(logoCenterX, logoCenterY);
            if (venueLogoUrl != null)
            {
                AvatarView.DrawRemote(drawList, logoCenter, logoRadius, theme,
                    dj.VenueName ?? string.Empty, dj.Server ?? string.Empty, venueLogoUrl,
                    images, lodestone, 0.75f, 36);
            }
            else
            {
                AvatarView.Draw(drawList, logoCenter, logoRadius, theme.Accent,
                    Initials.Of(dj.VenueName ?? string.Empty), 0.75f, AvatarHandle.Disabled, 36);
            }

            var infoTextX = logoCenterX + logoRadius + 8f * scale;
            var textMaxWidth = infoMax.X - infoTextX - 80f * scale;

            var detailAddress = dj.District != null && dj.Ward.HasValue && dj.Plot.HasValue
                ? $"{dj.District}-W{dj.Ward}-P{dj.Plot}"
                : dj.District ?? string.Empty;

            var venueStr = dj.VenueName ?? Loc.T(L.Rolladeck.VenueUnknown);
            var venueFit = Typography.FitText(venueStr, textMaxWidth, TextStyles.BodyEmphasized);

            if (detailAddress.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(infoTextX, infoMin.Y + 10f * scale), detailAddress, ui.MutedInk, TextStyles.Footnote);
                Typography.Draw(drawList, new Vector2(infoTextX, infoMin.Y + 26f * scale), venueFit, ui.Palette.TitleInk, TextStyles.BodyEmphasized);
            }
            else
            {
                Typography.Draw(drawList, new Vector2(infoTextX, infoMin.Y + 16f * scale), venueFit, ui.Palette.TitleInk, TextStyles.BodyEmphasized);
            }

            var serverStr = dj.ServerLabel;
            var serverSize = Typography.Measure(serverStr, TextStyles.Footnote);
            Typography.Draw(drawList,
                new Vector2(infoMax.X - 14f * scale - serverSize.X, infoMin.Y + 10f * scale),
                serverStr, ui.Palette.MutedInk, TextStyles.Footnote);

            if (infoHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                OpenVenueDetail(venueEntry!);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, djHeaderH + actionRowH + padX + infoCardH + 12f * scale));

            if (!string.IsNullOrEmpty(dj.EventName))
            {
                var eventOrigin = ImGui.GetCursorScreenPos();
                var eventLabel = cachedDjEventLabel;
                var eventSize = Typography.Measure(eventLabel, TextStyles.Footnote);
                Typography.Draw(drawList, new Vector2(eventOrigin.X + padX, eventOrigin.Y), eventLabel, ui.Palette.HeaderInk, TextStyles.Footnote);
                ImGui.SetCursorScreenPos(eventOrigin);
                ImGui.Dummy(new Vector2(width, eventSize.Y + 12f * scale));
            }


            var statsOrigin = ImGui.GetCursorScreenPos();
            var viewers = string.Format(Loc.T(L.Rolladeck.Viewers), cachedDjViewerCount);
            var viewerSize = Typography.Measure(viewers, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(statsOrigin.X + padX, statsOrigin.Y), viewers, ui.Palette.Accent, TextStyles.Subheadline);
            ImGui.SetCursorScreenPos(statsOrigin);
            ImGui.Dummy(new Vector2(width, viewerSize.Y + 8f * scale));

            if (!string.IsNullOrEmpty(dj.NormalizedTitle))
            {
                var titleOrigin = ImGui.GetCursorScreenPos();
                var lines = Typography.WrapText(dj.NormalizedTitle, TextStyles.Subheadline, contentWidth - 4f * scale);
                var lineHeight = Typography.Measure("A", TextStyles.Subheadline).Y;
                var lineY = titleOrigin.Y;
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    Typography.Draw(drawList, new Vector2(titleOrigin.X + padX, lineY), lines[lineIndex], ui.Palette.MutedInk, TextStyles.Subheadline);
                    lineY += lineHeight + 2f * scale;
                }

                ImGui.SetCursorScreenPos(titleOrigin);
                ImGui.Dummy(new Vector2(width, lineY - titleOrigin.Y + 16f * scale));
            }

            if (dj.Genres.Count > 0)
            {
                var genreOrigin = ImGui.GetCursorScreenPos();
                var genreLabel = Loc.T(L.Rolladeck.SectionGenres);
                var genreLabelH = Typography.Measure(genreLabel, TextStyles.Caption1).Y;
                Typography.Draw(drawList, new Vector2(genreOrigin.X + padX, genreOrigin.Y), genreLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(genreOrigin);
                ImGui.Dummy(new Vector2(width, genreLabelH + 6f * scale));

                var chipX = origin.X + padX;
                var chipY = ImGui.GetCursorScreenPos().Y;
                var chipH = 24f * scale;
                var chipPad = 10f * scale;
                var chipGapX = 6f * scale;
                var rowMaxX = origin.X + padX + contentWidth;
                var currentChipX = chipX;
                var currentChipY = chipY;

                for (var genreIndex = 0; genreIndex < dj.Genres.Count; genreIndex++)
                {
                    var genre = dj.Genres[genreIndex];
                    var genreSize = Typography.Measure(genre, TextStyles.Footnote);
                    var chipWidth = genreSize.X + chipPad * 2f;
                    if (currentChipX + chipWidth > rowMaxX && currentChipX > chipX)
                    {
                        currentChipX = chipX;
                        currentChipY += chipH + 6f * scale;
                    }

                    var chipMin = new Vector2(currentChipX, currentChipY);
                    var chipMax = new Vector2(currentChipX + chipWidth, currentChipY + chipH);
                    Squircle.Stroke(drawList, chipMin, chipMax, 6f * scale, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);
                    Typography.Draw(drawList, new Vector2(chipMin.X + chipPad, chipMin.Y + (chipH - genreSize.Y) * 0.5f),
                        genre, ui.Palette.BodyInk, TextStyles.Footnote);
                    currentChipX += chipWidth + chipGapX;
                }

                ImGui.SetCursorScreenPos(new Vector2(origin.X, chipY));
                ImGui.Dummy(new Vector2(width, (currentChipY - chipY) + chipH + 14f * scale));
            }

            liveDjSocialLinks.Clear();
            if (!string.IsNullOrEmpty(dj.TwitchUrl))     liveDjSocialLinks.Add(("Twitch",    FontAwesomeIcon.Tv,        dj.TwitchUrl!));
            if (!string.IsNullOrEmpty(dj.Twitter))       liveDjSocialLinks.Add(("Twitter",   FontAwesomeIcon.Feather,   dj.Twitter!));
            if (!string.IsNullOrEmpty(dj.Bluesky))       liveDjSocialLinks.Add(("Bluesky",   FontAwesomeIcon.Cloud,     dj.Bluesky!));
            if (!string.IsNullOrEmpty(dj.Instagram))     liveDjSocialLinks.Add(("Instagram", FontAwesomeIcon.Camera,    dj.Instagram!));
            if (!string.IsNullOrEmpty(dj.Youtube))       liveDjSocialLinks.Add(("YouTube",   FontAwesomeIcon.Play,      dj.Youtube!));
            if (!string.IsNullOrEmpty(dj.Tiktok))        liveDjSocialLinks.Add(("TikTok",    FontAwesomeIcon.Music,     dj.Tiktok!));
            if (!string.IsNullOrEmpty(dj.Website))       liveDjSocialLinks.Add(("Website",   FontAwesomeIcon.Globe,     dj.Website!));
            if (!string.IsNullOrEmpty(dj.MusicPlatform)) liveDjSocialLinks.Add(("Music",     FontAwesomeIcon.Headphones, dj.MusicPlatform!));

            if (liveDjSocialLinks.Count > 0)
            {
                var socialOrigin = ImGui.GetCursorScreenPos();
                var socialLabel = Loc.T(L.Rolladeck.SectionLinks);
                var socialLabelH = Typography.Measure(socialLabel, TextStyles.Caption1).Y;
                Typography.Draw(drawList, new Vector2(socialOrigin.X + padX, socialOrigin.Y), socialLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(socialOrigin);
                ImGui.Dummy(new Vector2(width, socialLabelH + 6f * scale));

                var iconButtonSize = 36f * scale;
                var iconGapX = 8f * scale;
                var socialRowMaxX = origin.X + padX + contentWidth;
                var socialX = origin.X + padX;
                var socialY = ImGui.GetCursorScreenPos().Y;
                var socialStartY = socialY;

                for (var socialIndex = 0; socialIndex < liveDjSocialLinks.Count; socialIndex++)
                {
                    var (label, icon, url) = liveDjSocialLinks[socialIndex];
                    if (socialX + iconButtonSize > socialRowMaxX && socialX > origin.X + padX)
                    {
                        socialX = origin.X + padX;
                        socialY += iconButtonSize + 8f * scale;
                    }

                    var buttonMin = new Vector2(socialX, socialY);
                    var buttonMax = new Vector2(socialX + iconButtonSize, socialY + iconButtonSize);

                    ImGui.SetCursorScreenPos(buttonMin);
                    var clicked = ImGui.InvisibleButton(LiveDjSocialButtonIds[socialIndex], new Vector2(iconButtonSize, iconButtonSize));
                    var buttonHovered = ImGui.IsItemHovered();

                    Squircle.Stroke(drawList, buttonMin, buttonMax, 8f * scale,
                        ImGui.GetColorU32(buttonHovered ? ui.Palette.Accent : ui.Palette.CardStroke), 1f);
                    AppSkin.Icon(drawList, new Vector2(buttonMin.X + iconButtonSize * 0.5f, buttonMin.Y + iconButtonSize * 0.5f),
                        icon.ToIconString(), buttonHovered ? ui.Palette.Accent : ui.Palette.MutedInk, 0.88f);

                    if (buttonHovered)
                    {
                        ImGui.SetTooltip(label);
                    }

                    if (clicked)
                    {
                        Util.OpenLink(url);
                    }

                    socialX += iconButtonSize + iconGapX;
                }

                ImGui.SetCursorScreenPos(new Vector2(origin.X, socialStartY));
                ImGui.Dummy(new Vector2(width, (socialY - socialStartY) + iconButtonSize + 14f * scale));
            }

            if (!string.IsNullOrEmpty(dj.Bio))
            {
                var bioOrigin = ImGui.GetCursorScreenPos();
                var bioLabel = Loc.T(L.Rolladeck.SectionAbout);
                var bioLabelH = Typography.Measure(bioLabel, TextStyles.Caption1).Y;
                Typography.Draw(drawList, new Vector2(bioOrigin.X + padX, bioOrigin.Y), bioLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(bioOrigin);
                ImGui.Dummy(new Vector2(width, bioLabelH + 6f * scale));

                var textOrigin = ImGui.GetCursorScreenPos();
                var lines = Typography.WrapText(dj.Bio, TextStyles.Subheadline, contentWidth);
                var lineHeight = Typography.Measure("A", TextStyles.Subheadline).Y;
                var lineY = textOrigin.Y;
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    Typography.Draw(drawList, new Vector2(textOrigin.X + padX, lineY), lines[lineIndex], ui.Palette.BodyInk, TextStyles.Subheadline);
                    lineY += lineHeight + 2f * scale;
                }

                ImGui.SetCursorScreenPos(textOrigin);
                ImGui.Dummy(new Vector2(width, lineY - textOrigin.Y + 16f * scale));
            }
        }

        var titleAlpha = Math.Clamp(djScrollY / (48f * scale), 0f, 1f);
        if (titleAlpha > 0f)
        {
            var foregroundList = ImGui.GetForegroundDrawList();
            var titleStr = Typography.FitText(dj.NormalizedName, area.Width * 0.6f, TextStyles.Headline);
            var titleSize = Typography.Measure(titleStr, TextStyles.Headline);
            var headerCenterY = area.Min.Y + TopBarHeight * scale * 0.5f;
            Typography.Draw(foregroundList,
                new Vector2(area.Center.X - titleSize.X * 0.5f, headerCenterY - titleSize.Y * 0.5f),
                titleStr, Palette.WithAlpha(ui.Palette.TitleInk, titleAlpha), TextStyles.Headline);
        }
    }

    private void DrawRolladeckFooter(ImDrawListPtr drawList, float scale, float rowWidth, Vector2 rowOrigin)
    {
        var radius = 11f * scale;
        var gap = 7f * scale;
        var label = Loc.T(L.Music.PoweredByRolladeck);
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var externalIconW = 14f * scale;
        var totalWidth = radius * 2f + gap + labelSize.X + gap + externalIconW;
        var rowLeft = rowOrigin.X + (rowWidth - totalWidth) * 0.5f;
        var rowCenterY = rowOrigin.Y + radius;
        var iconCenter = new Vector2(rowLeft + radius, rowCenterY);
        var accent = AppAccents.For("rolladeck");

        if (!AppIconTextures.TryDraw(drawList, "rolladeck", iconCenter, radius * 2f, Vector4.One))
        {
            drawList.AddCircleFilled(iconCenter, radius, ImGui.GetColorU32(accent), 24);
        }

        var textX = rowLeft + radius * 2f + gap;
        var textY = rowCenterY - labelSize.Y * 0.5f;
        Typography.Draw(drawList, new Vector2(textX, textY), label, ui.MutedInk, TextStyles.Caption1);

        AppSkin.Icon(drawList,
            new Vector2(textX + labelSize.X + gap + externalIconW * 0.5f, rowCenterY),
            FontAwesomeIcon.Globe.ToIconString(), accent, 0.65f);

        var rowMin = new Vector2(rowLeft, rowOrigin.Y);
        var rowMax = new Vector2(rowLeft + totalWidth, rowOrigin.Y + radius * 2f);
        var hovered = UiInteract.Hover(rowMin, rowMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rowMin, rowMax, hovered))
        {
            Util.OpenLink("https://xivrolladeck.com");
        }
    }

    private void DrawVenueDetail(in PhoneContext context)
    {
        var venue = selectedVenue;
        if (venue == null)
        {
            Router.Pop();
            return;
        }

        var scale = UiScale.Current;
        var area = context.Content;
        DrawTopBar(context, string.Empty, () => Router.Pop());

        var body = new Rect(
            new Vector2(area.Min.X, area.Min.Y + TopBarHeight * scale),
            area.Max);

        var venueKey = venue.Slug ?? venue.DisplayName;
        var isNewVenue = venueKey != lastVenueDetailKey;
        if (isNewVenue)
        {
            lastVenueDetailKey = venueKey;
        }

        using (AppSurface.Begin(body))
        {
            if (isNewVenue)
            {
                ImGui.SetScrollY(0f);
            }

            venueScrollY = ImGui.GetScrollY();
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var padX = 16f * scale;
            var contentWidth = width - padX * 2f;

            var logoRadius = 32f * scale;
            var logoCenter = new Vector2(origin.X + padX + logoRadius, origin.Y + 20f * scale + logoRadius);

            AvatarView.DrawRemote(drawList, logoCenter, logoRadius, theme,
                venue.DisplayName, venue.Server ?? string.Empty, venue.LogoUrl,
                images, lodestone, 0.75f, 64);

            var nameX = logoCenter.X + logoRadius + 12f * scale;
            var nameMaxWidth = origin.X + padX + contentWidth - nameX;
            var nameStr = Typography.FitText(venue.DisplayName, nameMaxWidth, TextStyles.Headline);
            var nameSize = Typography.Measure(nameStr, TextStyles.Headline);
            var nameY = logoCenter.Y - nameSize.Y * 0.5f;

            if (!string.IsNullOrEmpty(venue.ActiveLabel))
            {
                nameY -= nameSize.Y * 0.5f + 2f * scale;
                var activeStr = Typography.FitText(venue.ActiveLabel, nameMaxWidth, TextStyles.Footnote);
                Typography.Draw(drawList, new Vector2(nameX, nameY + nameSize.Y + 4f * scale),
                    activeStr, ui.Palette.Accent, TextStyles.Footnote);
            }

            Typography.Draw(drawList, new Vector2(nameX, nameY), nameStr, ui.Palette.TitleInk, TextStyles.Headline);

            var headerBlockH = 20f * scale + logoRadius * 2f + 16f * scale;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, headerBlockH));

            var addr = venue.FormattedAddress;
            if (!string.IsNullOrEmpty(addr) || !string.IsNullOrEmpty(venue.ServerLabel))
            {
                var cardMin = new Vector2(origin.X + padX, ImGui.GetCursorScreenPos().Y);
                var cardMax = new Vector2(origin.X + padX + contentWidth, cardMin.Y + 52f * scale);
                ui.Card(drawList, cardMin, cardMax, 10f * scale, elevated: false);

                if (!string.IsNullOrEmpty(addr))
                {
                    Typography.Draw(drawList, new Vector2(cardMin.X + 14f * scale, cardMin.Y + 12f * scale),
                        addr, ui.Palette.TitleInk, TextStyles.SubheadlineEmphasized);
                }

                if (!string.IsNullOrEmpty(venue.ServerLabel))
                {
                    var serverSize = Typography.Measure(venue.ServerLabel, TextStyles.Footnote);
                    Typography.Draw(drawList,
                        new Vector2(cardMax.X - 14f * scale - serverSize.X, cardMin.Y + 12f * scale),
                        venue.ServerLabel, ui.Palette.MutedInk, TextStyles.Footnote);
                }

                if (!string.IsNullOrEmpty(venue.FirstOpenedYear))
                {
                    var foSize = Typography.Measure(venue.FirstOpenedYear, TextStyles.Caption1);
                    Typography.Draw(drawList,
                        new Vector2(cardMax.X - 14f * scale - foSize.X, cardMax.Y - 12f * scale - foSize.Y),
                        venue.FirstOpenedYear, ui.Palette.MutedInk, TextStyles.Caption1);
                }

                ImGui.Dummy(new Vector2(width, 52f * scale + 12f * scale));
            }

            if (!string.IsNullOrEmpty(venue.DjName))
            {
                var npCursor = ImGui.GetCursorScreenPos();
                var npCardH = 64f * scale;
                var npMin = new Vector2(origin.X + padX, npCursor.Y);
                var npMax = new Vector2(origin.X + padX + contentWidth, npCursor.Y + npCardH);
                ui.Card(drawList, npMin, npMax, 10f * scale, elevated: false);

                var npLabel = Loc.T(L.Rolladeck.LiveNow);
                var npLabelSize = Typography.Measure(npLabel, TextStyles.Caption1);
                Typography.Draw(drawList, new Vector2(npMin.X + 14f * scale, npMin.Y + 10f * scale),
                    npLabel, ui.Palette.Accent, TextStyles.Caption1);

                var watchRadius = venue.DjTwitch != null ? 20f * scale : 0f;
                var djMaxWidth = npMax.X - (npMin.X + 14f * scale) - (watchRadius > 0f ? watchRadius * 2f + 24f * scale : 4f * scale);
                var djNameY = npMin.Y + 10f * scale + npLabelSize.Y + 3f * scale;
                var djFit = Typography.FitText(venue.DjName, djMaxWidth, TextStyles.SubheadlineEmphasized);
                Typography.Draw(drawList, new Vector2(npMin.X + 14f * scale, djNameY),
                    djFit, ui.Palette.TitleInk, TextStyles.SubheadlineEmphasized);

                if (venue.DjTwitch != null)
                {
                    var watchCenter = new Vector2(npMax.X - 14f * scale - watchRadius, npMin.Y + npCardH * 0.5f);
                    drawList.AddCircleFilled(watchCenter, watchRadius, ImGui.GetColorU32(ui.Palette.Accent), 32);
                    AppSkin.Icon(drawList, watchCenter, FontAwesomeIcon.Play.ToIconString(), ui.Palette.BackdropBottom, 1f);
                    ImGui.SetCursorScreenPos(new Vector2(watchCenter.X - watchRadius, watchCenter.Y - watchRadius));
                    if (ImGui.InvisibleButton("##venueWatch", new Vector2(watchRadius * 2f, watchRadius * 2f)))
                    {
                        Util.OpenLink(venue.DjRolladeckUrl ?? venue.DjTwitch!);
                    }
                }

                ImGui.SetCursorScreenPos(npCursor);
                ImGui.Dummy(new Vector2(width, npCardH + 10f * scale));
            }

            var eventName = venue.EventName ?? venue.DiscordEventName;
            var eventLabel = venue.EventName != null
                ? Loc.T(L.Rolladeck.EventLabel)
                : Loc.T(L.Rolladeck.DiscordEventLabel);
            if (!string.IsNullOrEmpty(eventName))
            {
                var evCursor = ImGui.GetCursorScreenPos();
                var evCardH = 64f * scale;
                var evMin = new Vector2(origin.X + padX, evCursor.Y);
                var evMax = new Vector2(origin.X + padX + contentWidth, evCursor.Y + evCardH);
                ui.Card(drawList, evMin, evMax, 10f * scale, elevated: false);

                var evLabelSize = Typography.Measure(eventLabel, TextStyles.Caption1);
                Typography.Draw(drawList, new Vector2(evMin.X + 14f * scale, evMin.Y + 10f * scale),
                    eventLabel, ui.Palette.Accent, TextStyles.Caption1);

                var evFit = Typography.FitText(eventName, contentWidth - 28f * scale, TextStyles.SubheadlineEmphasized);
                var evY = evMin.Y + 10f * scale + evLabelSize.Y + 3f * scale;
                Typography.Draw(drawList, new Vector2(evMin.X + 14f * scale, evY),
                    evFit, ui.Palette.TitleInk, TextStyles.SubheadlineEmphasized);

                ImGui.SetCursorScreenPos(evCursor);
                ImGui.Dummy(new Vector2(width, evCardH + 10f * scale));
            }

            var pillOrigin = ImGui.GetCursorScreenPos();
            var buttonHeight = 36f * scale;
            var buttonGap = 8f * scale;
            var primaryUrl = venue.WebsiteUrl ?? venue.RolladeckUrl;
            var primaryLabel = venue.WebsiteUrl != null ? Loc.T(L.Rolladeck.Website) : Loc.T(L.Rolladeck.Visit);

            var buttonCount = (venue.CanTeleport ? 1 : 0) + (primaryUrl != null ? 1 : 0) + (venue.DiscordUrl != null ? 1 : 0);
            if (buttonCount > 0)
            {
                var buttonWidth = (contentWidth - buttonGap * (buttonCount - 1)) / buttonCount;
                var buttonX = pillOrigin.X + padX;

                if (venue.CanTeleport)
                {
                    var teleRect = new Rect(new Vector2(buttonX, pillOrigin.Y), new Vector2(buttonX + buttonWidth, pillOrigin.Y + buttonHeight));
                    if (ui.GhostButton(teleRect, Loc.T(L.Rolladeck.Teleport)))
                    {
                        if (liveDjLifestreamAvailable)
                        {
                            LifestreamBridge.Travel(venue.Lifestream!);
                        }
                        else
                        {
                            ImGui.SetClipboardText(LifestreamBridge.TravelCommand(venue.Lifestream!));
                            ShellToast.Show();
                        }
                    }

                    if (!liveDjLifestreamAvailable)
                    {
                        HoverTooltip.Show(teleRect, Loc.T(L.Rolladeck.LifestreamNotInstalled), HoverLabelSide.Above);
                    }

                    buttonX += buttonWidth + buttonGap;
                }

                if (primaryUrl != null)
                {
                    var visitRect = new Rect(new Vector2(buttonX, pillOrigin.Y), new Vector2(buttonX + buttonWidth, pillOrigin.Y + buttonHeight));
                    if (ui.GhostButton(visitRect, primaryLabel))
                    {
                        Util.OpenLink(primaryUrl);
                    }

                    buttonX += buttonWidth + buttonGap;
                }

                if (venue.DiscordUrl != null)
                {
                    var discordRect = new Rect(new Vector2(buttonX, pillOrigin.Y), new Vector2(buttonX + buttonWidth, pillOrigin.Y + buttonHeight));
                    if (ui.GhostButton(discordRect, Loc.T(L.Rolladeck.Discord)))
                    {
                        Util.OpenLink(venue.DiscordUrl);
                    }
                }
            }

            ImGui.SetCursorScreenPos(pillOrigin);
            ImGui.Dummy(new Vector2(width, buttonHeight + 16f * scale));

            if (venue.Amenities.Count > 0)
            {
                var amOrigin = ImGui.GetCursorScreenPos();
                var amLabel = Loc.T(L.Rolladeck.SectionAmenities);
                var amLabelSize = Typography.Measure(amLabel, TextStyles.Caption1);
                Typography.Draw(drawList, new Vector2(amOrigin.X + padX, amOrigin.Y),
                    amLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(amOrigin);
                ImGui.Dummy(new Vector2(width, amLabelSize.Y + 6f * scale));

                var chipX = origin.X + padX;
                var chipY = ImGui.GetCursorScreenPos().Y;
                var chipH = 24f * scale;
                var chipPad = 10f * scale;
                var chipGapX = 6f * scale;
                var chipGapY = 6f * scale;
                var rowMaxX = origin.X + padX + contentWidth;
                var currentChipX = chipX;
                var currentChipY = chipY;

                for (var amenityIndex = 0; amenityIndex < venue.Amenities.Count; amenityIndex++)
                {
                    var amenity = venue.Amenities[amenityIndex];
                    var amenitySize = Typography.Measure(amenity, TextStyles.Footnote);
                    var chipWidth = amenitySize.X + chipPad * 2f;
                    if (currentChipX + chipWidth > rowMaxX && currentChipX > chipX)
                    {
                        currentChipX = chipX;
                        currentChipY += chipH + chipGapY;
                    }

                    var chipMin = new Vector2(currentChipX, currentChipY);
                    var chipMax = new Vector2(currentChipX + chipWidth, currentChipY + chipH);
                    Squircle.Stroke(drawList, chipMin, chipMax, 6f * scale, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);
                    Typography.Draw(drawList,
                        new Vector2(chipMin.X + chipPad, chipMin.Y + (chipH - amenitySize.Y) * 0.5f),
                        amenity, ui.Palette.BodyInk, TextStyles.Footnote);
                    currentChipX += chipWidth + chipGapX;
                }

                ImGui.SetCursorScreenPos(new Vector2(origin.X, chipY));
                ImGui.Dummy(new Vector2(width, (currentChipY - chipY) + chipH + 14f * scale));
            }

            if (!string.IsNullOrEmpty(venue.Description))
            {
                var descOrigin = ImGui.GetCursorScreenPos();
                var descLabel = Loc.T(L.Rolladeck.SectionAbout);
                var descLabelSize = Typography.Measure(descLabel, TextStyles.Caption1);
                Typography.Draw(drawList, new Vector2(descOrigin.X + padX, descOrigin.Y),
                    descLabel, ui.Palette.MutedInk, TextStyles.Caption1);
                ImGui.SetCursorScreenPos(descOrigin);
                ImGui.Dummy(new Vector2(width, descLabelSize.Y + 6f * scale));

                var textOrigin = ImGui.GetCursorScreenPos();
                var lines = Typography.WrapText(venue.Description, TextStyles.Subheadline, contentWidth);
                var lineHeight = Typography.Measure("A", TextStyles.Subheadline).Y;
                var lineY = textOrigin.Y;
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    Typography.Draw(drawList, new Vector2(textOrigin.X + padX, lineY),
                        lines[lineIndex], ui.Palette.BodyInk, TextStyles.Subheadline);
                    lineY += lineHeight + 2f * scale;
                }

                ImGui.SetCursorScreenPos(textOrigin);
                ImGui.Dummy(new Vector2(width, lineY - textOrigin.Y + 16f * scale));
            }
        }

        var venueTitleAlpha = Math.Clamp(venueScrollY / (48f * scale), 0f, 1f);
        if (venueTitleAlpha > 0f)
        {
            var foregroundList = ImGui.GetForegroundDrawList();
            var titleStr = Typography.FitText(venue.DisplayName, area.Width * 0.6f, TextStyles.Headline);
            var titleSize = Typography.Measure(titleStr, TextStyles.Headline);
            var headerCenterY = area.Min.Y + TopBarHeight * scale * 0.5f;
            Typography.Draw(foregroundList,
                new Vector2(area.Center.X - titleSize.X * 0.5f, headerCenterY - titleSize.Y * 0.5f),
                titleStr, Palette.WithAlpha(ui.Palette.TitleInk, venueTitleAlpha), TextStyles.Headline);
        }
    }
}
