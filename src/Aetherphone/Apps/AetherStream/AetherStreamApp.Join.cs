using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float JoinDebounceSeconds = 0.20f;
    private const float JoinRowHeight = 52f;
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
        var accentedTheme = AccentedTheme(context.Theme);
        var accentedContext = new PhoneContext(context.Content, accentedTheme, context.Navigation);
        AppHeader.Draw(accentedContext, Loc.T(L.AetherStream.JoinStream), () => router.Pop());

        TickNearbyRefresh();

        var margin = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top), new Vector2(area.Max.X - margin, area.Max.Y));

        var fieldRect = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + 36f * scale));
        SearchField.Draw(fieldRect, "##aetherstreamJoinSearch", Loc.T(L.AetherStream.JoinSearchHint), ref joinQuery,
            accentedTheme);
        TickJoinSearch();

        var listTop = fieldRect.Max.Y + 10f * scale;
        var nearby = watchAlong.Nearby;
        if (joinQuery.Trim().Length == 0 && nearby.Count > 0)
        {
            Typography.Draw(new Vector2(content.Min.X, listTop), Loc.T(L.AetherStream.JoinNearbyHeader),
                accentedTheme.TextMuted, TextStyles.Caption1);
            listTop += 20f * scale;
        }

        var listRect = new Rect(new Vector2(content.Min.X, listTop), content.Max);
        using (AppSurface.Begin(listRect))
        {
            var rowHeight = JoinRowHeight * scale;
            var cursorY = listRect.Min.Y;

            if (joinQuery.Trim().Length == 0)
            {
                for (var index = 0; index < nearby.Count; index++)
                {
                    var rowRect = new Rect(new Vector2(listRect.Min.X, cursorY),
                        new Vector2(listRect.Max.X, cursorY + rowHeight));
                    DrawNearbyResultRow(rowRect, nearby[index], accentedTheme, scale);
                    cursorY = rowRect.Max.Y;
                }
            }

            if (joinResults.Length == 0 && (joinQuery.Trim().Length > 0 || nearby.Count == 0))
            {
                var message = joinSearching ? Loc.T(L.Social.MentionSearching)
                    : joinSearchFailed ? Loc.T(L.AetherStream.JoinSearchFailed)
                    : Loc.T(L.PhotoTag.NoPeople);
                Typography.DrawCentered(new Vector2(listRect.Center.X, cursorY + 40f * scale), message,
                    accentedTheme.TextMuted, TextStyles.Subheadline.Scale);
            }

            for (var index = 0; index < joinResults.Length; index++)
            {
                var row = joinResults[index];
                var rowRect = new Rect(new Vector2(listRect.Min.X, cursorY), new Vector2(listRect.Max.X, cursorY + rowHeight));
                DrawJoinResultRow(rowRect, row, accentedTheme, scale);
                cursorY = rowRect.Max.Y;
            }

            ImGui.SetCursorScreenPos(listRect.Min);
            ImGui.Dummy(new Vector2(listRect.Width, cursorY - listRect.Min.Y + Metrics.Space.Lg * scale));
        }
    }

    private void DrawNearbyResultRow(Rect rect, NearbyStream row, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.06f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(rect.Min.X + 8f * scale + avatarRadius, rect.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, row.DisplayName, string.Empty, null,
            remoteImages, lodestone, 0.8f, 28);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, rect.Center.Y - 8f * scale), row.DisplayName, theme.TextStrong,
            TextStyles.Body);

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
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

    private void DrawJoinResultRow(Rect rect, UserDto row, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.06f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(rect.Min.X + 8f * scale + avatarRadius, rect.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, row.Name, row.World, row.AvatarUrl,
            remoteImages, lodestone, 0.8f, 28);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, rect.Center.Y - 16f * scale), row.DisplayName, theme.TextStrong,
            TextStyles.Body);
        Typography.Draw(new Vector2(textLeft, rect.Center.Y + 2f * scale), $"{row.Name}  ·  {row.World}",
            theme.TextMuted, TextStyles.Caption1);

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
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
