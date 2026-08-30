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
    private void DrawActivity(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Social.ActivityTitle), back);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        activityFeed.EnsureFresh(social.Latest);
        store.EnsureMe();
        store.EnsureFollowRequests();
        var listArea = body;
        var requestCount = store.PendingFollowRequestCount;
        if (requestCount > 0)
        {
            var pad = 12f * scale;
            var rowRect = new Rect(new Vector2(body.Min.X + pad, body.Min.Y + 6f * scale),
                new Vector2(body.Max.X - pad, body.Min.Y + 6f * scale + 54f * scale));
            DrawFollowRequestsRow(rowRect, requestCount);
            listArea = new Rect(new Vector2(body.Min.X, rowRect.Max.Y + 6f * scale), body.Max);
        }

        SocialActivityList.Draw(listArea, ui, AppPalettes.Aethergram, theme, activityFeed.Items, Id, images, lodestone,
            openActivityActor, openActivityPost, loadOlderActivity);
    }

    private void OpenActivity()
    {
        social.MarkSeen(Id);
        social.RefreshNow();
        activityFeed.Invalidate();
        store.RefreshFollowRequests();
        router.Push(AethergramRoute.Activity);
    }

    private void DrawFollowRequestsRow(Rect row, int count)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 16f * scale;
        ui.Card(drawList, row.Min, row.Max, rounding);
        UiInteract.HoverHighlight(drawList, row.Min, row.Max, rounding);
        var chipRadius = 15f * scale;
        var chipCenter = new Vector2(row.Min.X + 12f * scale + chipRadius, row.Center.Y);
        drawList.AddCircleFilled(chipCenter, chipRadius, ImGui.GetColorU32(Accent), 32);
        AppSkin.Icon(drawList, chipCenter, IconGlyph.Of(FontAwesomeIcon.UserClock), new Vector4(1f, 1f, 1f, 1f),
            0.85f);
        var label = Loc.T(L.Social.FollowRequestsCount, count);
        var labelSize = Typography.Measure(label, 1f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(chipCenter.X + chipRadius + 12f * scale, row.Center.Y - labelSize.Y * 0.5f),
            label, AppPalettes.Aethergram.TitleInk, 1f, FontWeight.SemiBold);
        AppSkin.Icon(drawList, new Vector2(row.Max.X - 18f * scale, row.Center.Y),
            IconGlyph.Of(FontAwesomeIcon.ChevronRight), AppPalettes.Aethergram.MutedInk, 0.8f);
        if (UiInteract.HoverClick(row.Min, row.Max))
        {
            OpenFollowRequests();
        }
    }

    private void OpenFollowRequests()
    {
        store.RefreshFollowRequests();
        router.Push(AethergramRoute.FollowRequests);
    }

    private void DrawFollowRequests(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Social.FollowRequests), back);
        var scale = UiScale.Current;
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var snapshot = store.FollowRequests;
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                var message = store.FollowRequestsLoading ? Loc.T(L.Common.Loading) : Loc.T(L.Social.ListEmpty);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), message,
                    AppPalettes.Aethergram.MutedInk);
                return;
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < snapshot.Length; index++)
            {
                DrawFollowRequestRow(snapshot[index]);
            }

            if (store.FollowRequestsLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, AppPalettes.Aethergram.MutedInk);
            }
            else if (store.HasMoreFollowRequests && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreFollowRequests();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawFollowRequestRow(UserDto user)
    {
        var scale = UiScale.Current;
        var rowHeight = 58f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        DrawAvatar(avatarCenter, radius, displayName, string.Empty, user.AvatarUrl, 0.95f, 32,
            Frames.Of(user.FrameId));
        var textLeft = avatarCenter.X + radius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + 9f * scale), displayName, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        var regionCode = SocialRegion.Resolve(user.Region, user.World, gameData);
        Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale),
            SocialIdentity.ProfileMeta(user.Handle, regionCode), AppPalettes.Aethergram.MutedInk, 0.85f);
        var buttonHeight = 30f * scale;
        var buttonWidth = 76f * scale;
        var buttonGap = 8f * scale;
        var buttonTop = origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f;
        var deleteRect = new Rect(new Vector2(origin.X + width - buttonWidth, buttonTop),
            new Vector2(origin.X + width, buttonTop + buttonHeight));
        var confirmRect = new Rect(new Vector2(deleteRect.Min.X - buttonGap - buttonWidth, buttonTop),
            new Vector2(deleteRect.Min.X - buttonGap, buttonTop + buttonHeight));
        if (ui.PillButton(confirmRect, Loc.T(L.Social.Confirm), true))
        {
            store.AcceptFollowRequest(user);
        }

        if (ui.PillButton(deleteRect, Loc.T(L.Social.Delete), false))
        {
            store.DeclineFollowRequest(user);
        }

        var rowMax = new Vector2(confirmRect.Min.X - 6f * scale, origin.Y + rowHeight);
        if (UiInteract.HoverClick(origin, rowMax))
        {
            OpenProfile(user.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }
}
