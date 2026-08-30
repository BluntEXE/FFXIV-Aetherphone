using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private void OpenSaved()
    {
        store.RefreshSaved();
        router.Push(AethergramRoute.Saved);
    }

    private void OpenHashtag(string tag)
    {
        store.OpenHashtagPosts(tag);
        router.Push(AethergramRoute.Hashtag(tag));
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
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(listRect))
        {
            var posts = store.HashtagPosts;
            if (posts.Length == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    store.HashtagLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Social.HashtagEmpty),
                    AppPalettes.Aethergram.MutedInk);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            DrawPostGrid(posts, L.Social.HashtagEmpty, store.HasMoreHashtagPosts, store.HashtagLoadingMore,
                store.LoadMoreHashtagPosts, SquareGrid);
        }
    }

    private void DrawSaved(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.SavedTitle), back);
        var scale = UiScale.Current;
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(listRect))
        {
            var posts = store.SavedPosts;
            if (posts.Length == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    store.SavedLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Aethergram.SavedEmpty),
                    AppPalettes.Aethergram.MutedInk);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            DrawPostGrid(posts, L.Aethergram.SavedEmpty, store.HasMoreSaved, store.SavedLoadingMore,
                store.LoadMoreSaved, SquareGrid);
        }
    }
    private void ResetExplore()
    {
        store.ClearDiscover();
        profile.SearchDraft = string.Empty;
    }

    private void DrawSearchTab(Rect area)
    {
        var scale = UiScale.Current;
        DrawTabTitle(area, Loc.T(L.Aethergram.FindPeople));
        area = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var searchHeight = 52f * scale;
        profile.DrawSearchBar(new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + searchHeight)));
        profile.DrawSearchResults(new Rect(new Vector2(area.Min.X, area.Min.Y + searchHeight), area.Max), theme,
            false);
    }

}
