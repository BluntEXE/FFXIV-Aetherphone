using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float JoinDebounceSeconds = 0.20f;
    private const float JoinRowHeight = 52f;

    private string joinQuery = string.Empty;
    private string joinApplied = string.Empty;
    private float joinDebounce;
    private UserDto[] joinResults = Array.Empty<UserDto>();
    private bool joinSearching;

    // Full page rather than a picker overlay - there is no "who's live" directory to browse (the
    // protocol has no discovery signal, deliberately, so presence isn't leaked), so this is always
    // just search-for-a-person. Mutual-contact and block checks happen entirely server-side.
    //
    // Uses AccountClient.SearchAsync (the general /users/search endpoint), not the mention-suggest
    // one used for @-mentions elsewhere - mention-suggest's DTO has no World field, and results
    // here need to show it. Mirrors SocialFeedStore.Search's own use of the same endpoint.
    private void DrawJoinScreen(PhoneContext context, Rect area, float scale)
    {
        var accentedTheme = AccentedTheme(context.Theme);
        var accentedContext = new PhoneContext(context.Content, accentedTheme, context.Navigation);
        AppHeader.Draw(accentedContext, Loc.T(L.AetherStream.JoinStream), () => router.Pop());

        var margin = Metrics.Space.Lg * scale;
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top), new Vector2(area.Max.X - margin, area.Max.Y));

        var fieldRect = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + 36f * scale));
        SearchField.Draw(fieldRect, "##aetherstreamJoinSearch", Loc.T(L.AetherStream.JoinSearchHint), ref joinQuery,
            accentedTheme);
        TickJoinSearch();

        var listRect = new Rect(new Vector2(content.Min.X, fieldRect.Max.Y + 10f * scale), content.Max);
        using (AppSurface.Begin(listRect))
        {
            if (joinResults.Length == 0)
            {
                var message = joinSearching ? Loc.T(L.Social.MentionSearching) : Loc.T(L.PhotoTag.NoPeople);
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 40f * scale), message,
                    accentedTheme.TextMuted, TextStyles.Subheadline.Scale);
            }

            var rowHeight = JoinRowHeight * scale;
            for (var index = 0; index < joinResults.Length; index++)
            {
                var row = joinResults[index];
                var rowRect = new Rect(new Vector2(listRect.Min.X, listRect.Min.Y + index * rowHeight),
                    new Vector2(listRect.Max.X, listRect.Min.Y + (index + 1) * rowHeight));
                DrawJoinResultRow(rowRect, row, accentedTheme, scale);
            }

            ImGui.SetCursorScreenPos(listRect.Min);
            ImGui.Dummy(new Vector2(listRect.Width, joinResults.Length * rowHeight + Metrics.Space.Lg * scale));
        }
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
        joinWork.Run("join search", async token =>
        {
            var result = await joinAccount.SearchAsync(trimmed, token).ConfigureAwait(false);
            if (result is not null)
            {
                joinResults = result.Users;
            }
        }, () => joinSearching = false);
    }
}
