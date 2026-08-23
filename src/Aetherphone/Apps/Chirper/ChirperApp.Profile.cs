using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private void DrawProfile(Rect area, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        var user = store.ProfileUser;
        var title = user is null
            ? Loc.T(L.Apps.Chirper)
            : SocialIdentity.Name(user.DisplayName, user.Handle);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (store.ProfileFailed)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Chirper.ProfileError), AppPalettes.Chirper.MutedInk);
            return;
        }

        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Chirper.MutedInk);
            return;
        }

        using (AppSurface.Begin(body))
        {
            profile.DrawProfileHeader(user, theme);
            var posts = store.ProfilePosts;
            ui.SectionHeading(Loc.T(L.Chirper.ChirpsTitle));
            if (posts.Length == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, ImGui.GetCursorScreenPos().Y + 40f * scale),
                    Loc.T(L.Chirper.Empty), AppPalettes.Chirper.MutedInk);
            }
            else
            {
                profileVirtualizer.BeginFrame();
                renderedUnderlyingIds.Clear();
                for (var index = 0; index < posts.Length; index++)
                {
                    var post = posts[index];
                    if (HiddenByMediaPreference(post))
                    {
                        continue;
                    }

                    if (!renderedUnderlyingIds.Add(post.RepostOfId ?? post.Id))
                    {
                        continue;
                    }

                    if (profileVirtualizer.Skip(post.Id))
                    {
                        continue;
                    }

                    DrawPost(post);
                    profileVirtualizer.Record(post.Id);
                }

                if (store.ProfileLoadingMore)
                {
                    InfiniteScroll.DrawLoadingRow(body.Center.X, AppPalettes.Chirper.MutedInk);
                }

                ImGui.Dummy(new Vector2(0f, 24f * scale));
                if (InfiniteScroll.ReachedBottom() && store.HasMoreProfilePosts && !store.ProfileLoadingMore)
                {
                    store.LoadMoreProfilePosts();
                }
            }
        }
    }

    private void OpenUserList(string sourceId, UserListKind kind)
    {
        actions.Reset();
        store.OpenUserList(sourceId, kind);
        router.Push(ChirperRoute.UserList(sourceId, kind));
    }

    private void OpenAvatarComposer()
    {
        avatar.Open();
        router.Push(ChirperRoute.Avatar);
    }

    private void DrawAvatarCompose(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        if (avatar.Draw(area, context, Accent))
        {
            store.ReloadProfile();
            router.Pop();
        }
    }

    private void OpenHashtag(string tag)
    {
        actions.Reset();
        store.OpenHashtagPosts(tag);
        router.Push(ChirperRoute.Hashtag(tag));
    }

    private string HashtagTitle(string tag)
    {
        if (!string.Equals(hashtagTitleTag, tag, StringComparison.Ordinal))
        {
            hashtagTitleTag = tag;
            hashtagTitle = "#" + tag;
        }

        return hashtagTitle;
    }

    private void DrawHashtag(Rect area, string tag)
    {
        store.EnsureHashtagPosts(tag);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, HashtagTitle(tag), back);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var posts = store.HashtagPosts;
            if (posts.Length == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, top + 60f * scale),
                    store.HashtagLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Social.HashtagEmpty),
                    AppPalettes.Chirper.MutedInk);
                return;
            }

            ImGui.Dummy(new Vector2(0f, FeedTopPadding * scale));
            hashtagVirtualizer.BeginFrame();
            renderedUnderlyingIds.Clear();
            for (var index = 0; index < posts.Length; index++)
            {
                var post = posts[index];
                if (HiddenByMediaPreference(post))
                {
                    continue;
                }

                if (!renderedUnderlyingIds.Add(post.RepostOfId ?? post.Id))
                {
                    continue;
                }

                if (hashtagVirtualizer.Skip(post.Id))
                {
                    continue;
                }

                DrawPost(post);
                hashtagVirtualizer.Record(post.Id);
            }

            if (store.HashtagLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, AppPalettes.Chirper.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
            if (InfiniteScroll.ReachedBottom() && store.HasMoreHashtagPosts && !store.HashtagLoadingMore)
            {
                store.LoadMoreHashtagPosts();
            }
        }
    }

    private void DrawDiscover(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Chirper.FindPeople), back);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchHeight = 52f * scale;
        profile.DrawSearchBar(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)));
        profile.DrawSearchResults(new Rect(new Vector2(area.Min.X, top + searchHeight), area.Max), theme, true);
    }

    private void DrawHomeTopBar(Rect area)
    {
        var scale = UiScale.Current;
        var actions = new HeaderActions(area, scale, HomeActionSlots);
        var titleLeft = area.Min.X + 16f * scale;
        if (store.Me is { } me)
        {
            var radius = 16f * scale;
            var center = new Vector2(titleLeft + radius, actions.RowCenterY);
            DrawAvatar(ImGui.GetWindowDrawList(), center, radius, me.Name, me.World, me.AvatarUrl, 0.9f, 28,
                Frames.Of(me.FrameId));
            if (UiInteract.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
            {
                OpenProfile(me.Id);
            }

            titleLeft = center.X + radius + 14f * scale;
        }

        if (HeaderTitle.Draw("chirper.home.title", DisplayName, titleLeft, actions, AppPalettes.Chirper.TitleInk,
                scale))
        {
            RefreshActiveFeed();
        }

        var searchCenter = actions.Slot(2);
        UiAnchors.Report("chirper.search", actions.Bounds(2));
        if (ui.IconButton(searchCenter, actions.Radius, FontAwesomeIcon.Search.ToIconString(),
                AppPalettes.Chirper.BodyInk, AppSkin.Transparent, 1.2f, Loc.T(L.Chirper.FindPeople),
                HoverLabelSide.Below) && store.IsSignedIn)
        {
            store.ClearDiscover();
            profile.SearchDraft = string.Empty;
            router.Push(ChirperRoute.Discover);
        }

        var bellCenter = actions.Slot(1);
        UiAnchors.Report("chirper.activity", actions.Bounds(1));
        if (ui.IconButton(bellCenter, actions.Radius, FontAwesomeIcon.Bell.ToIconString(), AppPalettes.Chirper.BodyInk,
                AppSkin.Transparent, 1.2f, Loc.T(L.Social.ActivityTitle), HoverLabelSide.Below) && store.IsSignedIn)
        {
            OpenActivity();
        }

        ActivityBadge.Draw(bellCenter + new Vector2(10f * scale, -10f * scale), social.UnseenCount(Id), theme, scale);
        if (ui.IconButton(actions.Slot(0), actions.Radius, FontAwesomeIcon.EllipsisH.ToIconString(),
                AppPalettes.Chirper.BodyInk, AppSkin.Transparent, 1.2f, Loc.T(L.Chirper.More), HoverLabelSide.Below))
        {
            overflowMenu.Toggle(OverflowMenuId, actions.Bounds(0));
        }
    }
}
