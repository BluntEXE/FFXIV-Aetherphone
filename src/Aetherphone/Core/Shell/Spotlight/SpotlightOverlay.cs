using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell.Spotlight;

internal sealed class SpotlightOverlay
{
    private const float SlideSmoothTime = 0.18f;
    private const float BarTopUnits = 64f;
    private const float BarHeightUnits = 36f;
    private const float RowHeightUnits = 46f;
    private const float SectionGapUnits = 12f;
    private const float BadgeRadiusUnits = 15f;

    private readonly SpotlightIndex index;
    private Spring slide;
    private bool open;
    private bool focusPending;
    private int openedFrame;
    private string query = string.Empty;

    public SpotlightOverlay(SpotlightIndex index)
    {
        this.index = index;
    }

    public bool Active => open || slide.Value > 0.01f;

    public void Open()
    {
        if (open)
        {
            return;
        }

        open = true;
        focusPending = true;
        query = string.Empty;
        index.Clear();
        openedFrame = ImGui.GetFrameCount();
    }

    public void Close() => open = false;

    public void CloseImmediate()
    {
        open = false;
        slide.SnapTo(0f);
    }

    public void Draw(Rect screen, PhoneTheme theme, INavigator navigation, float delta, float scale)
    {
        slide.Step(open ? 1f : 0f, SlideSmoothTime, delta);
        var eased = Math.Clamp(slide.Value, 0f, 1f);
        if (eased <= 0.001f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        Material.Veil(drawList, screen.Min, screen.Max, 0.55f * eased);
        var drop = (1f - Easing.EaseOutCubic(eased)) * -18f * scale;
        var barTop = screen.Min.Y + BarTopUnits * scale + drop;
        var bar = new Rect(new Vector2(screen.Min.X + 22f * scale, barTop),
            new Vector2(screen.Max.X - 22f * scale, barTop + BarHeightUnits * scale));
        Material.Frosted(drawList, bar.Min, bar.Max, bar.Height * 0.5f, scale, eased);
        var interactive = open && eased > 0.9f;
        if (interactive)
        {
            var previous = query;
            SearchField.Draw(bar, "##spotlightQuery", Loc.T(L.Spotlight.Hint), ref query, theme, 64, focusPending);
            focusPending = false;
            if (!string.Equals(previous, query, StringComparison.Ordinal))
            {
                index.Search(query);
            }
        }

        var results = index.Results;
        var listTop = bar.Max.Y + SectionGapUnits * scale;
        var list = new Rect(new Vector2(bar.Min.X, listTop), new Vector2(bar.Max.X, screen.Max.Y - 24f * scale));
        if (results.Count > 0)
        {
            DrawResults(drawList, list, theme, navigation, scale, eased, interactive);
        }
        else if (query.Trim().Length >= 2 && interactive)
        {
            Typography.DrawCentered(new Vector2(list.Center.X, list.Min.Y + 30f * scale),
                Loc.T(L.Spotlight.NoResults), Palette.WithAlpha(theme.TextMuted, eased), 0.9f);
        }

        drawList.PopClipRect();
        if (interactive && ImGui.GetFrameCount() != openedFrame && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            !UiInteract.Hover(bar.Min, bar.Max) && !UiInteract.Hover(list.Min, list.Max))
        {
            Close();
        }

        if (interactive && ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Close();
        }
    }

    private void DrawResults(ImDrawListPtr drawList, Rect list, PhoneTheme theme, INavigator navigation, float scale,
        float eased, bool interactive)
    {
        ImGui.SetCursorScreenPos(list.Min);
        using var child = ImRaii.Child("##spotlightResults", list.Size, false,
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
        var results = index.Results;
        var rowHeight = RowHeightUnits * scale;
        var y = list.Min.Y - ImGui.GetScrollY();
        var lastKind = (SpotlightKind)255;
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            if (result.Kind != lastKind)
            {
                lastKind = result.Kind;
                Typography.Draw(drawList, new Vector2(list.Min.X + 6f * scale, y + 4f * scale),
                    Loc.T(SectionLabel(result.Kind)), Palette.WithAlpha(theme.TextMuted, eased),
                    TextStyles.FootnoteEmphasized);
                y += 24f * scale;
            }

            var row = new Rect(new Vector2(list.Min.X, y), new Vector2(list.Max.X, y + rowHeight));
            DrawRow(drawList, row, result, theme, scale, eased, interactive, navigation);
            y += rowHeight;
        }

        ImGui.Dummy(new Vector2(1f, MathF.Max(1f, y + ImGui.GetScrollY() - list.Min.Y)));
    }

    private void DrawRow(ImDrawListPtr drawList, Rect row, in SpotlightResult result, PhoneTheme theme, float scale,
        float eased, bool interactive, INavigator navigation)
    {
        var hovered = interactive && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, 12f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * eased)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var badgeRadius = BadgeRadiusUnits * scale;
        var badgeCenter = new Vector2(row.Min.X + 10f * scale + badgeRadius, row.Center.Y);
        DrawBadge(drawList, badgeCenter, badgeRadius, result, theme, scale, eased);
        var textLeft = badgeCenter.X + badgeRadius + 11f * scale;
        var textMax = row.Max.X - 10f * scale - textLeft;
        var hasSubtitle = result.Subtitle.Length > 0;
        var titleY = hasSubtitle ? row.Center.Y - 15f * scale : row.Center.Y - 8f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, titleY),
            Typography.FitText(result.Title, textMax, 0.95f, FontWeight.SemiBold),
            Palette.WithAlpha(theme.TextStrong, eased), 0.95f, FontWeight.SemiBold);
        if (hasSubtitle)
        {
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y + 2f * scale),
                Typography.FitText(result.Subtitle, textMax, 0.78f, FontWeight.Regular),
                Palette.WithAlpha(theme.TextMuted, eased), 0.78f);
        }

        if (interactive && UiInteract.Click(row.Min, row.Max, hovered))
        {
            index.Activate(in result, navigation);
            Close();
        }
    }

    private static void DrawBadge(ImDrawListPtr drawList, Vector2 center, float radius, in SpotlightResult result,
        PhoneTheme theme, float scale, float eased)
    {
        if (result.Kind == SpotlightKind.App)
        {
            IconTile.DrawApp(drawList, result.Payload, center, radius * 2f,
                IconTile.Surface(AppAccents.For(result.Payload)));
            return;
        }

        var tint = result.Kind switch
        {
            SpotlightKind.Contact => new Vector4(0.30f, 0.62f, 0.95f, 1f),
            SpotlightKind.SettingsPage => new Vector4(0.55f, 0.57f, 0.62f, 1f),
            SpotlightKind.Conversation => new Vector4(0.35f, 0.78f, 0.52f, 1f),
            SpotlightKind.Note => new Vector4(0.98f, 0.80f, 0.28f, 1f),
            _ => new Vector4(0.86f, 0.62f, 0.28f, 1f),
        };
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.9f * eased)), 32);
        var icon = result.Kind switch
        {
            SpotlightKind.Contact => FontAwesomeIcon.User,
            SpotlightKind.SettingsPage => FontAwesomeIcon.Cog,
            SpotlightKind.Conversation => FontAwesomeIcon.CommentDots,
            SpotlightKind.Note => FontAwesomeIcon.StickyNote,
            _ => FontAwesomeIcon.Coins,
        };
        ProgressRing.CenterIcon(drawList, center, icon, Palette.WithAlpha(new Vector4(1f, 1f, 1f, 1f), eased),
            radius * 1.05f);
    }

    private static LocString SectionLabel(SpotlightKind kind) => kind switch
    {
        SpotlightKind.App => L.Spotlight.Apps,
        SpotlightKind.Contact => L.Spotlight.Contacts,
        SpotlightKind.SettingsPage => L.Spotlight.Settings,
        SpotlightKind.Conversation => L.Spotlight.Conversations,
        SpotlightKind.Note => L.Spotlight.Notes,
        _ => L.Spotlight.Items,
    };
}
