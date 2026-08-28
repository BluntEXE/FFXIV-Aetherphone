using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private void DrawProfileGrid() => DrawProfileGrid(store.ProfilePosts, L.Aethergram.Empty,
        store.HasMoreProfilePosts, store.ProfileLoadingMore, store.LoadMoreProfilePosts);

    private void DrawProfileGrid(PostDto[] posts, LocString emptyMessage, bool hasMore, bool loadingMore,
        Action loadMore)
    {
        var scale = UiScale.Current;
        if (posts.Length == 0)
        {
            Typography.DrawCentered(
                new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X * 0.5f,
                    ImGui.GetCursorScreenPos().Y + 40f * scale), Loc.T(emptyMessage), AppPalettes.Aethergram.MutedInk);
            return;
        }

        var gridCenterX = ImGui.GetCursorScreenPos().X + ScrollLayout.StableContentWidth() * 0.5f;
        var gap = 3f * scale;
        var cell = (ScrollLayout.StableContentWidth() - gap * (GridColumns - 1)) / GridColumns;
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        {
            for (var index = 0; index < posts.Length; index++)
            {
                ImGui.Dummy(new Vector2(cell, cell));
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                DrawGridThumbnail(posts[index], min, max);
                if (UiInteract.Click(min, max, UiInteract.Hover(min, max)))
                {
                    OpenDetail(posts[index]);
                }

                if (index % GridColumns != GridColumns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }

        ImGui.NewLine();
        if (loadingMore)
        {
            InfiniteScroll.DrawLoadingRow(gridCenterX, AppPalettes.Aethergram.MutedInk);
        }
        else if (hasMore && InfiniteScroll.ReachedBottom())
        {
            loadMore();
        }

        ImGui.Dummy(new Vector2(0f, 24f * scale));
    }

    private void DrawGridThumbnail(PostDto post, Vector2 min, Vector2 max)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 8f * scale;
        var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
        if (SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, configuration.ShowSensitiveContent))
        {
            SensitiveVeil.Draw(drawList, min, max, rounding);
        }
        else
        {
            var texture = images.Get(photos.Length > 0 ? photos[0] : null);
            if (texture is null)
            {
                Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(AppPalettes.Aethergram.FieldSurface));
                return;
            }

            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
            if (photos.Length > 1)
            {
                MultiPhotoBadge.Draw(drawList, new Vector2(max.X - 8f * scale, min.Y + 8f * scale), scale);
            }
        }

        if (ImGui.IsItemHovered())
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void DrawSearchTab(Rect area)
    {
        var scale = UiScale.Current;
        var searchHeight = 52f * scale;
        profile.DrawSearchBar(new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + searchHeight)));
        profile.DrawSearchResults(new Rect(new Vector2(area.Min.X, area.Min.Y + searchHeight), area.Max), theme,
            false);
    }

    private void DrawHomeTopBar(Rect area)
    {
        var scale = UiScale.Current;
        var actions = new HeaderActions(area, scale, store.IsSignedIn ? HomeActionSlots : 0);
        var logoLeft = area.Min.X + 16f * scale;
        if (HeaderTitle.Draw("aethergram.home.logo", DisplayName, logoLeft, actions,
                AppPalettes.Aethergram.TitleInk, scale) && store.IsSignedIn)
        {
            RefreshActiveFeed();
        }

        if (!store.IsSignedIn)
        {
            return;
        }

        var bellCenter = actions.Slot(1);
        UiAnchors.Report("aethergram.activity", actions.Bounds(1));
        if (ui.IconButton(bellCenter, actions.Radius, IconGlyph.Of(FontAwesomeIcon.Bell),
                AppPalettes.Aethergram.BodyInk, AppSkin.Transparent, 1.2f, Loc.T(L.Social.ActivityTitle),
                HoverLabelSide.Below))
        {
            OpenActivity();
        }

        ActivityBadge.Draw(bellCenter + new Vector2(10f * scale, -10f * scale), social.UnseenCount(Id), theme, scale);
        if (ui.IconButton(actions.Slot(0), actions.Radius, IconGlyph.Of(FontAwesomeIcon.EllipsisH),
                AppPalettes.Aethergram.BodyInk, AppSkin.Transparent, 1.2f, Loc.T(L.Aethergram.More),
                HoverLabelSide.Below))
        {
            overflowMenu.Toggle(OverflowMenuId, actions.Bounds(0));
        }
    }
}
