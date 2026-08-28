using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float BackChipRadius = 17f;
    private const float UserRowHeight = 62f;
    private const float FollowPillHeight = 31f;
    private const float ActivityIconRadius = 19f;
    private const float ActivityBadgeRadius = 9f;
    private const float ActivityBadgeRimFraction = 0.70711f;
    private const float SearchDebounceSeconds = 0.35f;
    private const float TagRowHeight = 60f;
    private const float TagGlyphSize = 38f;
    private const float EditAvatarRadius = 46f;
    private const float EditRowHeight = 46f;
    private const float EditBioMinHeight = 64f;

    private static readonly TextStyle ScreenTitleStyle = new(1.13f, FontWeight.Bold);
    private static readonly TextStyle ScreenSubtitleStyle = new(0.8f, FontWeight.Regular);
    private static readonly TextStyle UserNameStyle = new(0.97f, FontWeight.SemiBold);
    private static readonly TextStyle UserSubStyle = new(0.87f, FontWeight.Regular);
    private static readonly TextStyle SmallPillStyle = new(0.83f, FontWeight.Bold);
    private static readonly TextStyle SectionLabelStyle = new(0.87f, FontWeight.Bold);
    private static readonly TextStyle TagNameStyle = new(1f, FontWeight.SemiBold);
    private static readonly TextStyle TagCountStyle = new(0.87f, FontWeight.Regular);
    private static readonly TextStyle TagGlyphStyle = new(1.13f, FontWeight.Bold);
    private static readonly TextStyle ActivityActorStyle = new(0.95f, FontWeight.Bold);
    private static readonly TextStyle ActivityBodyStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle ActivityTimeStyle = new(0.8f, FontWeight.Regular);
    private static readonly TextStyle EditLabelStyle = new(0.93f, FontWeight.Regular);
    private static readonly TextStyle EditValueStyle = new(1f, FontWeight.Regular);
    private static readonly TextStyle EditHintStyle = new(0.8f, FontWeight.SemiBold);
    private static readonly TextStyle EditFootStyle = new(0.83f, FontWeight.Regular);
    private static readonly TextStyle SaveWordStyle = new(1.03f, FontWeight.Bold);
    private static readonly Vector4 BackChipFill = new(1f, 1f, 1f, 0.07f);
    private static readonly Vector4 BackChipHover = new(1f, 1f, 1f, 0.13f);
    private static readonly Vector4 SolidPillFill = new(0.949f, 0.961f, 0.980f, 1f);
    private static readonly Vector4 SolidPillInk = new(0.043f, 0.078f, 0.125f, 1f);
    private static readonly Vector4 GlassPillInk = new(0.875f, 0.902f, 0.941f, 1f);
    private static readonly Vector4 GlassPillStroke = new(1f, 1f, 1f, 0.12f);
    private static readonly Vector4 RowHover = new(1f, 1f, 1f, 0.03f);
    private static readonly Vector4 MentionInk = new(0.718f, 0.612f, 1f, 1f);
    private static readonly Vector4 ActivityBadgeRing = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 UnreadTint = Palette.WithAlpha(ChirperInk.Accent, 0.045f);
    private static readonly Vector4 EditCardFill = new(1f, 1f, 1f, 0.045f);
    private static readonly Vector4 EditCardStroke = new(1f, 1f, 1f, 0.07f);
    private static readonly Vector4 EditRowHairline = new(1f, 1f, 1f, 0.06f);
    private static readonly Vector4 SearchFieldFill = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 SearchFieldStroke = new(1f, 1f, 1f, 0.07f);

    private string searchDraft = string.Empty;
    private double searchDirtyAt = -1d;
    private bool trendingRequested;
    private bool mentionsOnly;
    private Spring activitySegment;
    private string editDisplay = string.Empty;
    private string editHandle = string.Empty;
    private string editBio = string.Empty;
    private string editStatus = string.Empty;
    private string? editLoadedFor;
    private volatile bool editBusy;
    private volatile int editOutcome;

    private float DrawScreenHeader(Rect area, string title, float trailingReserve = 0f, string subtitle = "",
        bool showBack = true)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var titleLeft = area.Min.X + CellPadX * scale;
        if (showBack)
        {
            var chipRadius = BackChipRadius * scale;
            var chipCenter = new Vector2(area.Min.X + 12f * scale + chipRadius, rowCenterY);
            var hitHalf = 22f * scale;
            var hitMin = chipCenter - new Vector2(hitHalf, hitHalf);
            var hitMax = chipCenter + new Vector2(hitHalf, hitHalf);
            var hovered = UiInteract.Hover(hitMin, hitMax);
            drawList.AddCircleFilled(chipCenter, chipRadius, ImGui.GetColorU32(hovered ? BackChipHover : BackChipFill), 32);
            ChirperIcons.ChevronLeft.Stroke(drawList, chipCenter, 17f * scale, ImGui.GetColorU32(ChirperInk.TitleInk), 2.4f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(hitMin, hitMax, hovered))
            {
                back();
            }

            titleLeft = chipCenter.X + chipRadius + 10f * scale;
        }

        var titleRight = area.Max.X - CellPadX * scale - trailingReserve;
        var titleHeight = Typography.LineHeight(ScreenTitleStyle);
        var fitted = Typography.FitText(title, MathF.Max(1f, titleRight - titleLeft), ScreenTitleStyle);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(titleLeft, rowCenterY - titleHeight * 0.5f), fitted,
                ChirperInk.TitleInk, ScreenTitleStyle);
        }
        else
        {
            var subtitleHeight = Typography.LineHeight(ScreenSubtitleStyle);
            var blockTop = rowCenterY - (titleHeight + subtitleHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(titleLeft, blockTop), fitted, ChirperInk.TitleInk, ScreenTitleStyle);
            Typography.Draw(drawList, new Vector2(titleLeft, blockTop + titleHeight),
                Typography.FitText(subtitle, MathF.Max(1f, titleRight - titleLeft), ScreenSubtitleStyle),
                ChirperInk.MutedInk, ScreenSubtitleStyle);
        }

        return titleLeft;
    }

    private void DrawDiscover(Rect area, bool root = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var fieldLeft = area.Min.X + 14f * scale;
        if (!root)
        {
            var chipRadius = BackChipRadius * scale;
            var chipCenter = new Vector2(area.Min.X + 14f * scale + chipRadius, rowCenterY);
            var hitHalf = 22f * scale;
            var hovered = UiInteract.Hover(chipCenter - new Vector2(hitHalf, hitHalf), chipCenter + new Vector2(hitHalf, hitHalf));
            drawList.AddCircleFilled(chipCenter, chipRadius, ImGui.GetColorU32(hovered ? BackChipHover : BackChipFill), 32);
            ChirperIcons.ChevronLeft.Stroke(drawList, chipCenter, 17f * scale, ImGui.GetColorU32(ChirperInk.TitleInk), 2.4f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(chipCenter - new Vector2(hitHalf, hitHalf), chipCenter + new Vector2(hitHalf, hitHalf), hovered))
            {
                back();
            }

            fieldLeft = chipCenter.X + chipRadius + 10f * scale;
        }

        var fieldHeight = 36f * scale;
        var fieldMin = new Vector2(fieldLeft, rowCenterY - fieldHeight * 0.5f);
        var fieldMax = new Vector2(area.Max.X - 14f * scale, rowCenterY + fieldHeight * 0.5f);
        Squircle.Fill(drawList, fieldMin, fieldMax, 12f * scale, ImGui.GetColorU32(SearchFieldFill));
        Squircle.Stroke(drawList, fieldMin, fieldMax, 12f * scale, ImGui.GetColorU32(SearchFieldStroke), 1f);
        ChirperIcons.Search(drawList, new Vector2(fieldMin.X + 19f * scale, rowCenterY), 15f * scale, ChirperInk.MutedInk, 2.2f);
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 32f * scale, rowCenterY - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldMax.X - fieldMin.X - 40f * scale);
        var hint = Loc.T(L.Chirper.SearchHint);
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(searchDraft);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.TitleInk))
        {
            if (ImGui.InputTextWithHint("##chirperSearch", hint, ref searchDraft, 64))
            {
                searchDirtyAt = ImGui.GetTime();
            }
        }

        var searchingTags = searchDraft.TrimStart().StartsWith('#');
        if (!trendingRequested && string.IsNullOrWhiteSpace(searchDraft))
        {
            trendingRequested = true;
            store.SearchTags(string.Empty);
        }

        if (searchDirtyAt >= 0d && ImGui.GetTime() - searchDirtyAt >= SearchDebounceSeconds)
        {
            searchDirtyAt = -1d;
            if (string.IsNullOrWhiteSpace(searchDraft))
            {
                store.ClearDiscover();
                trendingRequested = true;
                store.SearchTags(string.Empty);
            }
            else
            {
                store.SearchTags(searchDraft);
                if (!searchingTags)
                {
                    store.Search(searchDraft);
                }
            }
        }

        var top = area.Min.Y + AppHeader.Height * scale + 6f * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        var results = searchingTags ? Array.Empty<UserDto>() : store.DiscoverResults;
        var tags = store.DiscoverTags;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (results.Length == 0 && tags.Length == 0)
            {
                var message = store.Searching || store.TagsLoading
                    ? Loc.T(L.Common.Searching)
                    : Loc.T(L.Chirper.SearchByName);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), message,
                    ChirperInk.MutedInk, MetaStyle);
                return;
            }

            if (tags.Length > 0)
            {
                DrawSectionLabel(string.IsNullOrWhiteSpace(searchDraft)
                    ? Loc.T(L.Chirper.Trending)
                    : Loc.T(L.Chirper.HashtagsTitle));
                for (var index = 0; index < tags.Length; index++)
                {
                    DrawTagRow(tags[index]);
                }
            }

            if (results.Length > 0)
            {
                DrawSectionLabel(Loc.T(L.Chirper.SuggestedPeople));
                for (var index = 0; index < results.Length; index++)
                {
                    DrawUserRow(results[index]);
                }
            }

            ImGui.Dummy(new Vector2(0f, 40f * scale));
        }
    }

    private void DrawTagRow(TagSummaryDto summary)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = TagRowHeight * scale;
        var padX = CellPadX * scale;
        var rowMax = new Vector2(origin.X + width, origin.Y + height);
        var hovered = UiInteract.Hover(origin, rowMax);
        if (hovered)
        {
            drawList.AddRectFilled(origin, rowMax, ImGui.GetColorU32(RowHover));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var glyphSize = TagGlyphSize * scale;
        var glyphMin = new Vector2(origin.X + padX, origin.Y + (height - glyphSize) * 0.5f);
        var glyphMax = glyphMin + new Vector2(glyphSize, glyphSize);
        Squircle.Fill(drawList, glyphMin, glyphMax, 12f * scale, ImGui.GetColorU32(ChirperInk.AccentWash));
        Typography.DrawCentered(drawList, (glyphMin + glyphMax) * 0.5f, "#", ChirperInk.AccentLink, TagGlyphStyle);
        var textLeft = glyphMax.X + 12f * scale;
        var chevronLeft = rowMax.X - padX - 14f * scale;
        var textWidth = MathF.Max(1f, chevronLeft - textLeft - 8f * scale);
        var nameHeight = Typography.LineHeight(TagNameStyle);
        var countHeight = Typography.LineHeight(TagCountStyle);
        var textTop = origin.Y + (height - nameHeight - countHeight - 2f * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText("#" + summary.Tag, textWidth, TagNameStyle), ChirperInk.TitleInk, TagNameStyle);
        var count = summary.PostsToday > 0
            ? Loc.Plural(L.Chirper.ChirpsToday, summary.PostsToday)
            : Loc.Plural(L.Chirper.Posts, summary.Posts);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + 2f * scale),
            Typography.FitText(count, textWidth, TagCountStyle), ChirperInk.MutedInk, TagCountStyle);
        ChirperIcons.ChevronRight.Stroke(drawList, new Vector2(chevronLeft, origin.Y + height * 0.5f), 14f * scale,
            ImGui.GetColorU32(ChirperInk.FaintInk), 2.4f);
        if (UiInteract.Click(origin, rowMax, hovered))
        {
            OpenHashtag(summary.Tag, summary.PostsToday);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawSectionLabel(string label)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Typography.LineHeight(SectionLabelStyle) + 12f * scale;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + CellPadX * scale, origin.Y + 8f * scale),
            Loc.Culture.TextInfo.ToUpper(label), ChirperInk.FaintInk, SectionLabelStyle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawUserRow(UserDto user)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = UserRowHeight * scale;
        var padX = CellPadX * scale;
        var rowMax = new Vector2(origin.X + width, origin.Y + height);
        var pillHeight = FollowPillHeight * scale;
        var state = SocialFeedStore.FollowStateOf(user);
        var pillLabel = user.IsMe ? string.Empty : state switch
        {
            FollowState.Following => Loc.T(L.Chirper.Following),
            FollowState.Requested => Loc.T(L.Social.Requested),
            _ => Loc.T(L.Chirper.Follow),
        };
        var pillWidth = pillLabel.Length > 0 ? Typography.Measure(pillLabel, SmallPillStyle).X + 30f * scale : 0f;
        var pillMin = new Vector2(rowMax.X - padX - pillWidth, origin.Y + (height - pillHeight) * 0.5f);
        var pillMax = new Vector2(rowMax.X - padX, pillMin.Y + pillHeight);
        var tapMax = new Vector2(pillLabel.Length > 0 ? pillMin.X - 6f * scale : rowMax.X, rowMax.Y);
        var rowHovered = UiInteract.Hover(origin, tapMax);
        if (rowHovered)
        {
            drawList.AddRectFilled(origin, rowMax, ImGui.GetColorU32(RowHover));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var radius = FeedAvatarRadius * scale;
        var avatarCenter = new Vector2(origin.X + padX + radius, origin.Y + height * 0.5f);
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        DrawAvatar(drawList, avatarCenter, radius, user.IsMe ? user.Name : displayName,
            user.IsMe ? user.World : string.Empty, user.AvatarUrl, 0.95f, 32, Frames.Of(user.FrameId));
        var textLeft = avatarCenter.X + radius + AvatarGap * scale;
        var textRight = pillLabel.Length > 0 ? pillMin.X - 10f * scale : rowMax.X - padX;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var nameHeight = Typography.LineHeight(UserNameStyle);
        var subHeight = Typography.LineHeight(UserSubStyle);
        var textTop = origin.Y + (height - nameHeight - subHeight - 2f * scale) * 0.5f;
        UserName.DrawAuto(drawList, "chirper.row.name." + user.Id, displayName, user.Badges, user.ProfileBadges,
            textLeft, textTop, textWidth, UserNameStyle, ChirperInk.TitleInk, theme);
        var regionCode = user.IsMe
            ? SocialRegion.EffectiveCode(configuration, gameData)
            : SocialRegion.Resolve(user.Region, user.World, gameData);
        var sub = user.Bio.Length > 0 && user.Handle.Length > 0
            ? $"@{user.Handle} · {user.Bio}"
            : SocialIdentity.ProfileMeta(user.Handle, regionCode);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + 2f * scale),
            Typography.FitText(sub, textWidth, UserSubStyle), ChirperInk.MutedInk, UserSubStyle);
        if (pillLabel.Length > 0)
        {
            var pillHovered = UiInteract.Hover(pillMin, pillMax);
            var solid = state == FollowState.None;
            var fill = solid ? (pillHovered ? ChirperInk.White : SolidPillFill) : pillHovered ? ChirperInk.ChipHover : GlassPillFill;
            Squircle.Fill(drawList, pillMin, pillMax, pillHeight * 0.5f, ImGui.GetColorU32(fill));
            if (!solid)
            {
                Squircle.Stroke(drawList, pillMin, pillMax, pillHeight * 0.5f, ImGui.GetColorU32(GlassPillStroke), 1f);
            }

            Typography.DrawCentered(drawList, (pillMin + pillMax) * 0.5f, pillLabel, solid ? SolidPillInk : GlassPillInk,
                SmallPillStyle);
            if (pillHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(pillMin, pillMax, pillHovered))
            {
                store.ToggleFollow(user);
            }
        }

        if (UiInteract.Click(origin, tapMax, rowHovered))
        {
            OpenProfile(user.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawUserList(Rect area, string sourceId, UserListKind kind)
    {
        store.EnsureUserList(sourceId, kind);
        DrawScreenHeader(area, SocialProfilePages.UserListTitle(kind));
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        var snapshot = store.UserListResults;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.UserListLoading ? Loc.T(L.Common.Loading)
                    : store.UserListFailed ? Loc.T(L.Chirper.ProfileError)
                    : Loc.T(L.Social.ListEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), message,
                    ChirperInk.MutedInk, MetaStyle);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < snapshot.Length; index++)
            {
                DrawUserRow(snapshot[index]);
            }

            if (store.UserListLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, ChirperInk.MutedInk);
            }
            else if (store.HasMoreUserList && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreUserList();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawActivity(Rect area, bool root = false)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Social.ActivityTitle), 0f, string.Empty, !root);
        var rowTop = area.Min.Y + AppHeader.Height * scale;
        var row = new Rect(new Vector2(area.Min.X, rowTop), new Vector2(area.Max.X, rowTop + FeedTabRowHeight * scale));
        var picked = DrawUnderlineTabs(row, Loc.T(L.Chirper.ActivityAll), Loc.T(L.Chirper.ActivityMentions),
            mentionsOnly, ref activitySegment);
        if (picked >= 0)
        {
            mentionsOnly = picked == 1;
        }

        var body = new Rect(new Vector2(area.Min.X, row.Max.Y), area.Max);
        activityFeed.EnsureFresh(social.Latest);
        var items = activityFeed.Items;
        var shown = 0;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            for (var index = 0; index < items.Length; index++)
            {
                if (!ShowsActivity(items[index]))
                {
                    continue;
                }

                DrawActivityRow(items[index]);
                shown++;
            }

            if (shown == 0)
            {
                Typography.DrawWrappedCentered(new Vector2(body.Center.X, body.Min.Y + 90f * scale),
                    Loc.T(L.Social.ActivityEmpty), ChirperInk.MutedInk, MetaStyle, body.Width - 64f * scale);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 300f * scale)
            {
                activityFeed.LoadOlder();
            }
        }
    }

    private static int DrawUnderlineTabs(Rect row, string leftLabel, string rightLabel, bool rightActive,
        ref Spring slide)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var half = row.Width * 0.5f;
        var leftRect = new Rect(row.Min, new Vector2(row.Min.X + half, row.Max.Y));
        var rightRect = new Rect(new Vector2(row.Min.X + half, row.Min.Y), row.Max);
        DrawFeedTabLabel(drawList, leftRect, leftLabel, !rightActive);
        DrawFeedTabLabel(drawList, rightRect, rightLabel, rightActive);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        slide.Step(rightActive ? 1f : 0f, SegmentSmoothTime, delta);
        var inset = CellPadX * scale;
        var underlineWidth = half - inset;
        var underlineLeft = row.Min.X + inset + slide.Value * (half - inset);
        var underlineTop = row.Max.Y - FeedTabUnderline * scale;
        Squircle.Fill(drawList, new Vector2(underlineLeft, underlineTop),
            new Vector2(underlineLeft + underlineWidth, row.Max.Y), FeedTabUnderline * scale * 0.5f,
            ImGui.GetColorU32(ChirperInk.Accent));
        DrawHairline(drawList, row.Min.X, row.Max.X, row.Max.Y);
        if (UiInteract.HoverClick(leftRect.Min, leftRect.Max))
        {
            return 0;
        }

        return UiInteract.HoverClick(rightRect.Min, rightRect.Max) ? 1 : -1;
    }

    private bool ShowsActivity(NotificationDto item)
    {
        if (item.App != Id || SocialActivity.IsModerationNotice(item.Type))
        {
            return false;
        }

        return !mentionsOnly
            || item.Type == SocialActivity.TypeMention
            || item.Type == SocialActivity.TypeCommentMention;
    }

    private void DrawActivityRow(NotificationDto item)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = CellPadX * scale;
        var padY = 12f * scale;
        var iconRadius = ActivityIconRadius * scale;
        var timeText = TimeText.Short(item.CreatedAtUnix);
        var timeSize = Typography.Measure(timeText, ActivityTimeStyle);
        var textLeft = origin.X + padX + iconRadius * 2f + 12f * scale;
        var textRight = origin.X + width - padX - timeSize.X - 12f * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var actor = SocialActivity.ActorLabel(item);
        var body = SocialActivity.Body(item);
        var actorHeight = Typography.LineHeight(ActivityActorStyle);
        var bodyHeight = body.Length > 0 ? Typography.MeasureWrappedBlock(body, ActivityBodyStyle, textWidth).Y : 0f;
        var contentHeight = actorHeight + (bodyHeight > 0f ? 2f * scale + bodyHeight : 0f);
        var rowHeight = MathF.Max(iconRadius * 2f, contentHeight) + padY * 2f;
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        var hovered = UiInteract.Hover(origin, rowMax);
        if (!item.Read)
        {
            drawList.AddRectFilled(origin, rowMax, ImGui.GetColorU32(UnreadTint));
        }

        if (hovered)
        {
            drawList.AddRectFilled(origin, rowMax, ImGui.GetColorU32(RowHover));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarCenter = new Vector2(origin.X + padX + iconRadius, origin.Y + padY + iconRadius);
        DrawAvatar(drawList, avatarCenter, iconRadius, actor, string.Empty, item.ActorAvatarUrl, 0.95f, 32,
            Frames.Of(item.ActorFrameId));
        var badgeOffset = iconRadius * ActivityBadgeRimFraction;
        DrawActivityBadge(drawList, avatarCenter + new Vector2(badgeOffset, badgeOffset), item.Type, scale);
        var textTop = origin.Y + padY;
        UserName.DrawAuto(drawList, "chirper.activity.actor." + item.Id, actor, item.ActorBadges, item.ActorBadgeIds,
            textLeft, textTop, textWidth, ActivityActorStyle, ChirperInk.TitleInk, theme);
        if (bodyHeight > 0f)
        {
            Typography.DrawWrappedLeft(new Vector2(textLeft, textTop + actorHeight + 2f * scale), body, ChirperInk.BodyInk,
                ActivityBodyStyle, textWidth);
        }

        Typography.Draw(drawList, new Vector2(origin.X + width - padX - timeSize.X, textTop + 1f * scale), timeText,
            ChirperInk.FaintInk, ActivityTimeStyle);
        if (!item.Read)
        {
            drawList.AddCircleFilled(new Vector2(origin.X + width - padX - 3.5f * scale, textTop + timeSize.Y + 10f * scale),
                3.5f * scale, ImGui.GetColorU32(ChirperInk.Accent), 12);
        }

        DrawHairline(drawList, origin.X, rowMax.X, rowMax.Y);
        var avatarExtent = new Vector2(iconRadius, iconRadius);
        if (UiInteract.HoverClick(avatarCenter - avatarExtent, avatarCenter + avatarExtent))
        {
            OpenProfile(item.ActorId);
        }
        else if (UiInteract.Click(origin, rowMax, hovered))
        {
            if (SocialActivity.OpensPost(item))
            {
                OpenThreadFromLink(item.PostId!);
            }
            else
            {
                OpenProfile(item.ActorId);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private static void DrawActivityBadge(ImDrawListPtr drawList, Vector2 center, int type, float scale)
    {
        var radius = ActivityBadgeRadius * scale;
        var iconSize = radius * 1.15f;
        var ink = ImGui.GetColorU32(ChirperInk.White);
        drawList.AddCircleFilled(center, radius + 1.6f * scale, ImGui.GetColorU32(ActivityBadgeRing), 20);
        switch (type)
        {
            case SocialActivity.TypeLike:
            case SocialActivity.TypeCommentLike:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.LikeRed), 20);
                ChirperIcons.HeartFilled(drawList, center, iconSize, ChirperInk.White);
                break;
            case SocialActivity.TypeRepost:
            case SocialActivity.TypeQuote:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.RechirpGreen), 20);
                ChirperIcons.Rechirp.Stroke(drawList, center, iconSize, ink, 2.8f);
                break;
            case SocialActivity.TypeComment:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.AccentLink), 20);
                ChirperIcons.Reply.Stroke(drawList, center, iconSize, ink, 2.6f);
                break;
            case SocialActivity.TypeMention:
            case SocialActivity.TypeCommentMention:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(MentionInk), 20);
                Typography.DrawCentered(drawList, center, "@", ChirperInk.White, TextStyles.Caption2);
                break;
            case SocialActivity.TypeFollow:
            case SocialActivity.TypeFollowRequest:
            case SocialActivity.TypeFollowAccept:
            case SocialActivity.TypeConnectRequest:
            case SocialActivity.TypeConnectAccept:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.AccentLink), 20);
                ChirperIcons.Plus.Stroke(drawList, center, iconSize, ink, 3.2f);
                break;
            default:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ChirperInk.MutedInk), 20);
                ChirperIcons.Bell.Stroke(drawList, center, iconSize, ink, 2.6f);
                break;
        }
    }

    private void DrawEditProfile(Rect area)
    {
        var me = store.Me ?? (store.ProfileUser is { IsMe: true } self ? self : null);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var cancelLabel = Loc.T(L.Common.Cancel);
        var cancelSize = Typography.Measure(cancelLabel, ComposeCancelStyle);
        var cancelMin = area.Min;
        var cancelMax = new Vector2(area.Min.X + CellPadX * scale + cancelSize.X + 12f * scale, area.Min.Y + AppHeader.Height * scale);
        var cancelHovered = UiInteract.Hover(cancelMin, cancelMax);
        Typography.Draw(drawList, new Vector2(area.Min.X + CellPadX * scale, rowCenterY - cancelSize.Y * 0.5f), cancelLabel,
            cancelHovered ? ChirperInk.TitleInk : ChirperInk.BodyInk, ComposeCancelStyle);
        if (cancelHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(cancelMin, cancelMax, cancelHovered))
        {
            back();
        }

        if (me is null)
        {
            store.EnsureMe();
            AppHeader.DrawTitleWithReserve(area, "chirper.edit.title", Loc.T(L.Chirper.EditProfile), 0f, ChirperInk.TitleInk,
                scale, ComposeTitleStyle);
            Typography.DrawCentered(new Vector2(area.Center.X, area.Min.Y + 120f * scale), Loc.T(L.Common.Loading),
                ChirperInk.MutedInk);
            return;
        }

        if (editOutcome == 1)
        {
            editOutcome = 0;
            store.ReloadProfile();
            toast.Show(Loc.T(L.Chirper.Save));
            back();
            return;
        }

        if (editOutcome == 2)
        {
            editOutcome = 0;
            editStatus = Loc.T(L.Chirper.HandleTaken);
        }

        if (editLoadedFor != me.Id)
        {
            editLoadedFor = me.Id;
            editDisplay = me.DisplayName;
            editHandle = me.Handle;
            editBio = me.Bio;
            editStatus = string.Empty;
        }

        var handleValid = SocialProfilePages.IsHandleValid(editHandle);
        var canSave = !editBusy && !string.IsNullOrWhiteSpace(editDisplay) && handleValid;
        var saveLabel = editBusy ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.Save);
        var saveSize = Typography.Measure(saveLabel, SaveWordStyle);
        var saveMax = new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var saveMin = new Vector2(area.Max.X - CellPadX * scale - saveSize.X - 12f * scale, area.Min.Y);
        var saveHovered = canSave && UiInteract.Hover(saveMin, saveMax);
        Typography.Draw(drawList, new Vector2(area.Max.X - CellPadX * scale - saveSize.X, rowCenterY - saveSize.Y * 0.5f),
            saveLabel, !canSave ? ChirperInk.FaintInk : saveHovered ? ChirperInk.MineInk : ChirperInk.AccentLink, SaveWordStyle);
        if (saveHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(saveMin, saveMax, saveHovered))
        {
            SaveProfile();
        }

        AppHeader.DrawTitleWithReserve(area, "chirper.edit.title", Loc.T(L.Chirper.EditProfile), saveSize.X + 28f * scale,
            ChirperInk.TitleInk, scale, ComposeTitleStyle, (cancelMax.X - area.Min.X) / scale + 8f);

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var listDrawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var padX = CellPadX * scale;
            var avatarRadius = EditAvatarRadius * scale;
            var avatarCenter = new Vector2(origin.X + width * 0.5f, origin.Y + 14f * scale + avatarRadius);
            DrawAvatar(listDrawList, avatarCenter, avatarRadius, me.Name, me.World, me.AvatarUrl, 1.4f, 64, Frames.Of(me.FrameId));
            var badgeRadius = 15f * scale;
            var badgeCenter = avatarCenter + new Vector2(avatarRadius - badgeRadius + 2f * scale, avatarRadius - badgeRadius + 2f * scale);
            listDrawList.AddCircleFilled(badgeCenter, badgeRadius + 3f * scale, ImGui.GetColorU32(ChirperInk.BackdropTop), 32);
            Squircle.FillCircleVerticalGradient(listDrawList, badgeCenter, badgeRadius, ImGui.GetColorU32(ChirperInk.Accent),
                ImGui.GetColorU32(ChirperInk.AccentDeep));
            ChirperIcons.CameraBadge(listDrawList, badgeCenter, 13f * scale, ChirperInk.White);
            var avatarExtent = new Vector2(avatarRadius + 4f * scale, avatarRadius + 4f * scale);
            if (UiInteract.HoverClick(avatarCenter - avatarExtent, avatarCenter + avatarExtent))
            {
                OpenAvatarComposer();
            }

            var cardTop = avatarCenter.Y + avatarRadius + 18f * scale;
            var cardMin = new Vector2(origin.X + padX, cardTop);
            var cardRight = origin.X + width - padX;
            var rowHeight = EditRowHeight * scale;
            var labelWidth = 104f * scale;
            var innerPad = 14f * scale;
            var bioLabelHeight = Typography.LineHeight(EditLabelStyle);
            var bioFieldHeight = EditBioMinHeight * scale;
            var bioRowHeight = 12f * scale + bioLabelHeight + 5f * scale + bioFieldHeight + 12f * scale;
            var cardMax = new Vector2(cardRight, cardTop + rowHeight * 2f + bioRowHeight);
            Squircle.Fill(listDrawList, cardMin, cardMax, 16f * scale, ImGui.GetColorU32(EditCardFill));
            Squircle.Stroke(listDrawList, cardMin, cardMax, 16f * scale, ImGui.GetColorU32(EditCardStroke), 1f);

            var nameRowTop = cardTop;
            DrawEditLabel(listDrawList, cardMin.X + innerPad, nameRowTop, rowHeight, Loc.T(L.Chirper.NameLabel));
            DrawEditInput("##chirperEditName", cardMin.X + innerPad + labelWidth, cardRight - innerPad, nameRowTop, rowHeight,
                ref editDisplay, SocialProfilePages.DisplayNameMax, ImGuiInputTextFlags.None, ChirperInk.TitleInk);
            listDrawList.AddLine(new Vector2(cardMin.X, nameRowTop + rowHeight), new Vector2(cardRight, nameRowTop + rowHeight),
                ImGui.GetColorU32(EditRowHairline), 1f);

            var handleRowTop = nameRowTop + rowHeight;
            DrawEditLabel(listDrawList, cardMin.X + innerPad, handleRowTop, rowHeight, Loc.T(L.Chirper.HandleShort));
            var atSize = Typography.Measure("@", EditValueStyle);
            Typography.Draw(listDrawList, new Vector2(cardMin.X + innerPad + labelWidth, handleRowTop + (rowHeight - atSize.Y) * 0.5f),
                "@", ChirperInk.FaintInk, EditValueStyle);
            var availableLabel = Loc.T(L.Chirper.HandleAvailable);
            var availableSize = Typography.Measure(availableLabel, EditHintStyle);
            var showAvailable = handleValid && string.Equals(editHandle, me.Handle, StringComparison.Ordinal);
            var handleRight = cardRight - innerPad - (showAvailable ? availableSize.X + 22f * scale : 0f);
            if (DrawEditInput("##chirperEditHandle", cardMin.X + innerPad + labelWidth + atSize.X + 2f * scale, handleRight,
                    handleRowTop, rowHeight, ref editHandle, SocialProfilePages.HandleMax, ImGuiInputTextFlags.CharsNoBlank,
                    handleValid ? ChirperInk.TitleInk : ChirperInk.Danger))
            {
                editHandle = editHandle.ToLowerInvariant();
            }

            if (showAvailable)
            {
                var checkCenter = new Vector2(cardRight - innerPad - availableSize.X - 9f * scale, handleRowTop + rowHeight * 0.5f);
                ChirperIcons.Check.Stroke(listDrawList, checkCenter, 13f * scale, ImGui.GetColorU32(ChirperInk.RechirpGreen), 2.6f);
                Typography.Draw(listDrawList, new Vector2(cardRight - innerPad - availableSize.X, handleRowTop + (rowHeight - availableSize.Y) * 0.5f),
                    availableLabel, ChirperInk.RechirpGreen, EditHintStyle);
            }

            listDrawList.AddLine(new Vector2(cardMin.X, handleRowTop + rowHeight), new Vector2(cardRight, handleRowTop + rowHeight),
                ImGui.GetColorU32(EditRowHairline), 1f);

            var bioRowTop = handleRowTop + rowHeight;
            var bioLabelTop = bioRowTop + 12f * scale;
            Typography.Draw(listDrawList, new Vector2(cardMin.X + innerPad, bioLabelTop), Loc.T(L.Chirper.BioLabel), ChirperInk.MutedInk,
                EditLabelStyle);
            var counter = $"{editBio.Length.ToString(Loc.Culture)}/{SocialProfilePages.BioMax.ToString(Loc.Culture)}";
            var counterSize = Typography.Measure(counter, EditFootStyle);
            Typography.Draw(listDrawList, new Vector2(cardRight - innerPad - counterSize.X, bioLabelTop + (bioLabelHeight - counterSize.Y) * 0.5f),
                counter, ChirperInk.FaintInk, EditFootStyle);
            var bioFieldTop = bioLabelTop + bioLabelHeight + 5f * scale;
            var bioFieldWidth = cardRight - innerPad - (cardMin.X + innerPad);
            ImGui.SetCursorScreenPos(new Vector2(cardMin.X + innerPad, bioFieldTop));
            using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.TitleInk))
            {
                var wrapWidth = bioFieldWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
                SoftWrapField.Multiline("##chirperEditBio", ref editBio, SocialProfilePages.BioMax,
                    new Vector2(bioFieldWidth, bioFieldHeight), wrapWidth);
            }

            var footTop = cardMax.Y + 12f * scale;
            var footText = editStatus.Length > 0 ? editStatus : Loc.T(L.Chirper.HandleRules);
            Typography.DrawWrappedLeft(new Vector2(cardMin.X + 4f * scale, footTop), footText,
                editStatus.Length > 0 ? ChirperInk.Danger : ChirperInk.FaintInk, EditFootStyle, cardRight - cardMin.X - 8f * scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, footTop + 60f * scale - origin.Y));
        }
    }

    private static void DrawEditLabel(ImDrawListPtr drawList, float left, float rowTop, float rowHeight, string label)
    {
        var size = Typography.Measure(label, EditLabelStyle);
        Typography.Draw(drawList, new Vector2(left, rowTop + (rowHeight - size.Y) * 0.5f), label, ChirperInk.MutedInk,
            EditLabelStyle);
    }

    private static bool DrawEditInput(string id, float left, float right, float rowTop, float rowHeight, ref string value,
        int maxLength, ImGuiInputTextFlags flags, Vector4 ink)
    {
        ImGui.SetCursorScreenPos(new Vector2(left, rowTop + rowHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, right - left));
        Plugin.Fonts.NoticeText(value);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ink))
        {
            return ImGui.InputText(id, ref value, maxLength, flags);
        }
    }

    private void SaveProfile()
    {
        if (!store.IsSignedIn || editBusy)
        {
            return;
        }

        if (!SocialProfilePages.IsHandleValid(editHandle) || string.IsNullOrWhiteSpace(editDisplay))
        {
            editStatus = Loc.T(L.Chirper.HandleRules);
            return;
        }

        editBusy = true;
        editStatus = string.Empty;
        store.UpdateProfile(editDisplay.Trim(), editHandle.Trim(), editBio.Trim(), (ok, _) =>
        {
            editBusy = false;
            editOutcome = ok ? 1 : 2;
        });
    }
}
