using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{

    private void DrawProfile(Rect area, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        var user = store.ProfileUser;
        var title = user is null
            ? DisplayName
            : SocialIdentity.Name(user.DisplayName, user.Handle);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        DrawProfileBody(new Rect(new Vector2(area.Min.X, top), area.Max), userId);
    }

    private void DrawProfileBody(Rect body, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        if (store.ProfileFailed)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Aethergram.ProfileError), AppPalettes.Aethergram.MutedInk);
            return;
        }

        var user = store.ProfileUser;
        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
            return;
        }

        using (AppSurface.Begin(body))
        {
            profile.DrawProfileHeader(user, theme);
            if (user.IsPrivate && !user.IsFollowing && !user.IsMe)
            {
                DrawPrivateProfileNotice();
                return;
            }

            var scale = UiScale.Current;
            var tabRow = new Rect(
                new Vector2(ImGui.GetCursorScreenPos().X + 14f * scale, ImGui.GetCursorScreenPos().Y + 4f * scale),
                new Vector2(ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X - 14f * scale,
                    ImGui.GetCursorScreenPos().Y + 32f * scale));
            for (var index = 0; index < ProfileTabs.Length; index++)
            {
                profileTabLabels[index] = Loc.T(ProfileTabs[index]);
            }

            profileTab = SegmentStrip.Draw("aethergram.profileTabs", tabRow, profileTabLabels, profileTab,
                AppPalettes.Aethergram);
            ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, tabRow.Max.Y + 10f * scale));
            if (profileTab == 0)
            {
                DrawPostGrid(store.ProfilePosts, L.Aethergram.Empty, store.HasMoreProfilePosts,
                    store.ProfileLoadingMore, store.LoadMoreProfilePosts, SquareGrid);
            }
            else
            {
                store.EnsureTaggedPosts(userId);
                DrawPostGrid(store.TaggedPosts, L.PhotoTag.NoTagged,
                    store.HasMoreTagged, store.TaggedLoadingMore, store.LoadMoreTaggedPosts, SquareGrid);
            }
        }
    }

    private void DrawPrivateProfileNotice()
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var centerX = origin.X + width * 0.5f;
        var lockCenter = new Vector2(centerX, origin.Y + 52f * scale);
        drawList.AddCircle(lockCenter, 26f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.Aethergram.MutedInk, 0.5f)), 48, 1.6f * scale);
        AppSkin.Icon(drawList, lockCenter, IconGlyph.Of(FontAwesomeIcon.Lock), AppPalettes.Aethergram.TitleInk,
            1.3f);
        var titleTop = lockCenter.Y + 40f * scale;
        var titleHeight = Typography.DrawWrappedCentered(new Vector2(centerX, titleTop),
            Loc.T(L.Aethergram.PrivateTitle), AppPalettes.Aethergram.TitleInk, TextStyles.BodyEmphasized,
            width - 48f * scale);
        var subtitleTop = titleTop + titleHeight + 6f * scale;
        var subtitleHeight = Typography.DrawWrappedCentered(new Vector2(centerX, subtitleTop),
            Loc.T(L.Aethergram.PrivateSubtitle), AppPalettes.Aethergram.MutedInk, TextStyles.Subheadline,
            width - 48f * scale);
        ImGui.Dummy(new Vector2(width, subtitleTop + subtitleHeight + 24f * scale - origin.Y));
    }

    private void DrawProfileTab(Rect area)
    {
        var scale = UiScale.Current;
        DrawTabTitle(area, store.Me is { } title ? SocialIdentity.Name(title.DisplayName, title.Handle)
            : Loc.T(L.Aethergram.Profile));
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (store.Me is not { } me)
        {
            store.EnsureMe();
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AethergramInk.MutedInk);
            return;
        }

        DrawProfileBody(body, me.Id);
    }

}
