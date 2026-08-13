using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float JoinDebounceSeconds = 0.20f;
    private const float JoinRowHeight = 56f;
    private const float NearbyRefreshIntervalSeconds = 5f;

    private string joinQuery = string.Empty;
    private string joinApplied = string.Empty;
    private float joinDebounce;
    private UserDto[] joinResults = Array.Empty<UserDto>();
    private bool joinSearching;
    private bool joinSearchFailed;
    private float nearbyRefreshTimer = NearbyRefreshIntervalSeconds;

    private void DrawJoinScreen(PhoneContext context, Rect area, float scale)
    {
        ui.Body(area);
        var accentedContext = new PhoneContext(area, accentedTheme, context.Navigation);
        AppHeader.Draw(accentedContext, Loc.T(L.AetherStream.JoinStream), () => router.Pop());

        TickNearbyRefresh();

        var margin = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top), new Vector2(area.Max.X - margin, area.Max.Y));

        var fieldRect = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + 36f * scale));
        SearchField.Draw(fieldRect, "##aetherstreamJoinSearch", Loc.T(L.AetherStream.JoinSearchHint), ref joinQuery,
            accentedTheme);
        TickJoinSearch();

        var listRect = new Rect(new Vector2(content.Min.X, fieldRect.Max.Y + Metrics.Space.Md * scale),
            content.Max);
        using (AppSurface.Begin(listRect))
        {
            var width = ScrollLayout.StableContentWidth();
            if (joinQuery.Trim().Length == 0)
            {
                DrawNearbyList(width, scale, listRect);
            }
            else
            {
                DrawSearchResults(width, scale, listRect);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawNearbyList(float width, float scale, Rect listRect)
    {
        var nearby = watchAlong.Nearby;
        if (nearby.Count == 0)
        {
            DrawJoinEmptyState(width, scale, listRect, FontAwesomeIcon.Tv,
                Loc.T(L.AetherStream.JoinEmptyTitle), Loc.T(L.AetherStream.JoinEmptyHint));
            return;
        }

        var labelOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(ImGui.GetWindowDrawList(), labelOrigin,
            Loc.Culture.TextInfo.ToUpper(Loc.T(L.AetherStream.JoinNearbyHeader)), ui.Palette.HeaderInk,
            TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(labelOrigin);
        ImGui.Dummy(new Vector2(width,
            Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Xs * scale));

        for (var index = 0; index < nearby.Count; index++)
        {
            DrawNearbyResultRow(width, scale, nearby[index]);
        }
    }

    private void DrawSearchResults(float width, float scale, Rect listRect)
    {
        if (joinResults.Length == 0)
        {
            if (joinSearching)
            {
                var origin = ImGui.GetCursorScreenPos();
                var height = MathF.Max(160f * scale, listRect.Max.Y - origin.Y - Metrics.Space.Lg * scale);
                LoadingPulse.Draw(new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f), 16f * scale,
                    ui.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, height));
                return;
            }

            if (joinSearchFailed)
            {
                DrawJoinEmptyState(width, scale, listRect, FontAwesomeIcon.ExclamationTriangle,
                    Loc.T(L.AetherStream.JoinSearchFailedTitle), Loc.T(L.AetherStream.JoinSearchFailed));
                return;
            }

            DrawJoinEmptyState(width, scale, listRect, FontAwesomeIcon.Search, Loc.T(L.PhotoTag.NoPeople),
                string.Empty);
            return;
        }

        for (var index = 0; index < joinResults.Length; index++)
        {
            DrawJoinResultRow(width, scale, joinResults[index]);
        }
    }

    private void DrawJoinEmptyState(float width, float scale, Rect listRect, FontAwesomeIcon icon, string title,
        string hint)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = MathF.Max(220f * scale, listRect.Max.Y - origin.Y - Metrics.Space.Lg * scale);
        EmptyState.Draw(new Rect(origin, origin + new Vector2(width, height)), ui, icon, title, hint);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawNearbyResultRow(float width, float scale, NearbyStream row)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, origin + new Vector2(width, JoinRowHeight * scale));
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(rect.Min.X + Metrics.Space.Sm * scale + avatarRadius, rect.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, row.Name, row.World, null, remoteImages,
            lodestone, 0.8f, 28);

        var liveLabel = Loc.T(L.Common.Live);
        var pillWidth = LivePill.Width(liveLabel, scale);
        var pillOrigin = new Vector2(rect.Max.X - Metrics.Space.Sm * scale - pillWidth,
            rect.Center.Y - LivePill.Height(scale) * 0.5f);
        LivePill.Draw(drawList, pillOrigin, liveLabel, theme.Danger, (float)ImGui.GetTime(), scale);

        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textWidth = pillOrigin.X - Metrics.Space.Md * scale - textLeft;
        Marquee.DrawLeft(drawList, "aetherstream.nearby.name." + row.HostId, row.DisplayName, textLeft,
            rect.Center.Y - 16f * scale, textWidth, TextStyles.BodyEmphasized, ui.TitleInk, hovered);
        Marquee.DrawLeft(drawList, "aetherstream.nearby.world." + row.HostId, $"{row.Name}  ·  {row.World}",
            textLeft, rect.Center.Y + 2f * scale, textWidth, TextStyles.Caption1, ui.MutedInk, hovered);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, JoinRowHeight * scale));

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            watchAlong.Join(row.HostId);
            router.Pop();
        }
    }

    private void TickNearbyRefresh()
    {
        nearbyRefreshTimer += ImGui.GetIO().DeltaTime;
        if (nearbyRefreshTimer < NearbyRefreshIntervalSeconds)
        {
            return;
        }

        nearbyRefreshTimer = 0f;
        watchAlong.RequestNearbyStreams();
    }

    private void DrawJoinResultRow(float width, float scale, UserDto row)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, origin + new Vector2(width, JoinRowHeight * scale));
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(rect.Min.X + Metrics.Space.Sm * scale + avatarRadius, rect.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, row.Name, row.World, row.AvatarUrl,
            remoteImages, lodestone, 0.8f, 28);

        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textWidth = rect.Max.X - Metrics.Space.Sm * scale - textLeft;
        Marquee.DrawLeft(drawList, "aetherstream.result.name." + row.Id, row.DisplayName, textLeft,
            rect.Center.Y - 16f * scale, textWidth, TextStyles.BodyEmphasized, ui.TitleInk, hovered);
        Marquee.DrawLeft(drawList, "aetherstream.result.world." + row.Id, $"{row.Name}  ·  {row.World}", textLeft,
            rect.Center.Y + 2f * scale, textWidth, TextStyles.Caption1, ui.MutedInk, hovered);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, JoinRowHeight * scale));

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            watchAlong.Join(row.Id);
            router.Pop();
        }
    }

    private void TickJoinSearch()
    {
        var trimmed = joinQuery.Trim();
        if (trimmed.Length == 0)
        {
            joinApplied = string.Empty;
            joinResults = Array.Empty<UserDto>();
            joinSearching = false;
            joinSearchFailed = false;
            return;
        }

        if (string.Equals(trimmed, joinApplied, StringComparison.Ordinal))
        {
            return;
        }

        joinDebounce += ImGui.GetIO().DeltaTime;
        if (joinDebounce < JoinDebounceSeconds)
        {
            return;
        }

        joinDebounce = 0f;
        joinApplied = trimmed;
        joinSearching = true;
        joinSearchFailed = false;
        joinWork.Run("join search", async token =>
        {
            var result = await joinAccount.SearchAsync(trimmed, token).ConfigureAwait(false);
            joinResults = result?.Users ?? Array.Empty<UserDto>();
            joinSearchFailed = result is null;
        }, () => joinSearching = false);
    }
}
