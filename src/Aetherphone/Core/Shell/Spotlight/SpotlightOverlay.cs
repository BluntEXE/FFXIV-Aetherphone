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
    private const float CallButtonRadiusUnits = 15f;
    private static readonly Vector4 CallGreen = new(0.20f, 0.78f, 0.35f, 1f);
    private const float SectionHeaderUnits = 26f;
    private const float PanelPadUnits = 8f;
    private const float PanelRadiusUnits = 22f;
    private const float EmptyPanelHeightUnits = 74f;

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
        var panel = new Rect(list.Min, list.Min);
        if (results.Count > 0)
        {
            panel = DrawResults(drawList, list, theme, navigation, scale, eased, interactive);
        }
        else if (query.Trim().Length >= 2 && interactive)
        {
            panel = DrawEmpty(drawList, list, theme, scale, eased);
        }

        drawList.PopClipRect();
        if (interactive && ImGui.GetFrameCount() != openedFrame && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            !UiInteract.Hover(bar.Min, bar.Max) && !UiInteract.Hover(panel.Min, panel.Max))
        {
            Close();
        }

        if (interactive && ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Close();
        }
    }

    private Rect DrawResults(ImDrawListPtr drawList, Rect list, PhoneTheme theme, INavigator navigation, float scale,
        float eased, bool interactive)
    {
        var results = index.Results;
        var rowHeight = RowHeightUnits * scale;
        var headerHeight = SectionHeaderUnits * scale;
        var padding = PanelPadUnits * scale;
        var radius = PanelRadiusUnits * scale;
        var panelHeight = MathF.Min(Measure(results, rowHeight, headerHeight) + padding * 2f, list.Height);
        var panel = new Rect(list.Min, new Vector2(list.Max.X, list.Min.Y + panelHeight));
        DrawPanel(drawList, panel, radius, scale, eased);

        var content = new Rect(new Vector2(panel.Min.X + padding, panel.Min.Y + padding),
            new Vector2(panel.Max.X - padding, panel.Max.Y - padding));
        drawList.PushClipRect(content.Min, content.Max, true);
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##spotlightResults", content.Size, false,
                   ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar))
        {
            var y = content.Min.Y - ImGui.GetScrollY();
            var lastKind = (SpotlightKind)255;
            for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
            {
                var result = results[resultIndex];
                var newSection = result.Kind != lastKind;
                if (newSection)
                {
                    lastKind = result.Kind;
                    if (y + headerHeight > content.Min.Y && y < content.Max.Y)
                    {
                        Typography.Draw(drawList, new Vector2(content.Min.X + 12f * scale, y + 7f * scale),
                            Loc.T(SectionLabel(result.Kind)), Palette.WithAlpha(theme.TextMuted, 0.85f * eased),
                            TextStyles.FootnoteEmphasized);
                    }

                    y += headerHeight;
                }

                var row = new Rect(new Vector2(content.Min.X, y), new Vector2(content.Max.X, y + rowHeight));
                var visible = row.Max.Y > content.Min.Y && row.Min.Y < content.Max.Y;
                if (visible)
                {
                    if (!newSection)
                    {
                        var separatorLeft = row.Min.X + (21f + BadgeRadiusUnits * 2f) * scale;
                        drawList.AddLine(new Vector2(separatorLeft, row.Min.Y),
                            new Vector2(row.Max.X - 10f * scale, row.Min.Y),
                            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f * eased)), 1f * scale);
                    }

                    DrawRow(drawList, row, result, theme, scale, eased, interactive, navigation);
                }

                y += rowHeight;
            }

            ImGui.Dummy(new Vector2(1f, MathF.Max(1f, y + ImGui.GetScrollY() - content.Min.Y)));
        }

        drawList.PopClipRect();
        return panel;
    }

    private static void DrawPanel(ImDrawListPtr drawList, Rect panel, float radius, float scale, float eased)
    {
        Elevation.Card(drawList, panel.Min, panel.Max, radius, scale, eased);
        Material.Frosted(drawList, panel.Min, panel.Max, radius, scale, eased);
    }

    private static Rect DrawEmpty(ImDrawListPtr drawList, Rect list, PhoneTheme theme, float scale, float eased)
    {
        var height = MathF.Min(EmptyPanelHeightUnits * scale, list.Height);
        var panel = new Rect(list.Min, new Vector2(list.Max.X, list.Min.Y + height));
        DrawPanel(drawList, panel, PanelRadiusUnits * scale, scale, eased);
        Typography.DrawCentered(drawList, panel.Center, Loc.T(L.Spotlight.NoResults),
            Palette.WithAlpha(theme.TextMuted, eased), 0.9f);
        return panel;
    }

    private static float Measure(IReadOnlyList<SpotlightResult> results, float rowHeight, float headerHeight)
    {
        var total = 0f;
        var lastKind = (SpotlightKind)255;
        for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
        {
            var kind = results[resultIndex].Kind;
            if (kind != lastKind)
            {
                lastKind = kind;
                total += headerHeight;
            }

            total += rowHeight;
        }

        return total;
    }

    private void DrawRow(ImDrawListPtr drawList, Rect row, in SpotlightResult result, PhoneTheme theme, float scale,
        float eased, bool interactive, INavigator navigation)
    {
        var hovered = interactive && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, 12f * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * eased)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var badgeRadius = BadgeRadiusUnits * scale;
        var badgeCenter = new Vector2(row.Min.X + 10f * scale + badgeRadius, row.Center.Y);
        DrawBadge(drawList, badgeCenter, badgeRadius, result, theme, scale, eased);
        var textLeft = badgeCenter.X + badgeRadius + 11f * scale;
        var rowRight = row.Max.X - 10f * scale;
        var overCall = false;
        if (result.Kind == SpotlightKind.Contact && index.CallsAvailable)
        {
            var callRadius = CallButtonRadiusUnits * scale;
            var callCenter = new Vector2(rowRight - callRadius, row.Center.Y);
            var callMin = new Vector2(callCenter.X - callRadius, callCenter.Y - callRadius);
            var callMax = new Vector2(callCenter.X + callRadius, callCenter.Y + callRadius);
            overCall = interactive && UiInteract.Hover(callMin, callMax);
            DrawCallButton(drawList, callCenter, callRadius, theme, eased, overCall);
            if (overCall && UiInteract.Click(callMin, callMax, true))
            {
                index.Call(in result, navigation);
                Close();
                return;
            }

            rowRight = callMin.X - 8f * scale;
        }

        var textMax = rowRight - textLeft;
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

        if (interactive && !overCall && UiInteract.Click(row.Min, row.Max, hovered))
        {
            index.Activate(in result, navigation);
            Close();
        }
    }

    private static void DrawCallButton(ImDrawListPtr drawList, Vector2 center, float radius, PhoneTheme theme,
        float eased, bool hovered)
    {
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(Palette.WithAlpha(CallGreen, (hovered ? 1f : 0.82f) * eased)), 32);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Phone,
            Palette.WithAlpha(theme.TextStrong, eased), radius * 0.95f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private static void DrawBadge(ImDrawListPtr drawList, Vector2 center, float radius, in SpotlightResult result,
        PhoneTheme theme, float scale, float eased)
    {
        if (result.Kind == SpotlightKind.App || result.Kind == SpotlightKind.StoreApp)
        {
            IconTile.DrawApp(drawList, result.Payload, center, radius * 2f,
                IconTile.Surface(AppAccents.For(result.Payload)));
            return;
        }

        var tint = result.Kind switch
        {
            SpotlightKind.Calculation => new Vector4(0.98f, 0.62f, 0.16f, 1f),
            SpotlightKind.Action => new Vector4(0.36f, 0.55f, 0.92f, 1f),
            SpotlightKind.Contact => new Vector4(0.30f, 0.62f, 0.95f, 1f),
            SpotlightKind.DmThread => new Vector4(0.20f, 0.78f, 0.35f, 1f),
            SpotlightKind.SettingsPage => new Vector4(0.55f, 0.57f, 0.62f, 1f),
            SpotlightKind.Shortcut => new Vector4(0.62f, 0.42f, 0.94f, 1f),
            SpotlightKind.Aetheryte => new Vector4(0.24f, 0.74f, 0.86f, 1f),
            SpotlightKind.Conversation => new Vector4(0.35f, 0.78f, 0.52f, 1f),
            SpotlightKind.Note => new Vector4(0.98f, 0.80f, 0.28f, 1f),
            SpotlightKind.Guide => new Vector4(0.90f, 0.32f, 0.36f, 1f),
            SpotlightKind.Venue => new Vector4(0.94f, 0.40f, 0.72f, 1f),
            _ => new Vector4(0.86f, 0.62f, 0.28f, 1f),
        };
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.9f * eased)), 32);
        var icon = result.Kind switch
        {
            SpotlightKind.Calculation => FontAwesomeIcon.Calculator,
            SpotlightKind.Action => SpotlightActions.Icon((SpotlightActionKind)result.PageIndex),
            SpotlightKind.Contact => FontAwesomeIcon.User,
            SpotlightKind.DmThread => FontAwesomeIcon.Comment,
            SpotlightKind.SettingsPage => FontAwesomeIcon.Cog,
            SpotlightKind.Shortcut => FontAwesomeIcon.Bolt,
            SpotlightKind.Aetheryte => FontAwesomeIcon.MapMarkerAlt,
            SpotlightKind.Conversation => FontAwesomeIcon.CommentDots,
            SpotlightKind.Note => FontAwesomeIcon.StickyNote,
            SpotlightKind.Guide => FontAwesomeIcon.BookOpen,
            SpotlightKind.Venue => FontAwesomeIcon.GlassCheers,
            _ => FontAwesomeIcon.Coins,
        };
        ProgressRing.CenterIcon(drawList, center, icon, Palette.WithAlpha(new Vector4(1f, 1f, 1f, 1f), eased),
            radius * 1.05f);
    }

    private static LocString SectionLabel(SpotlightKind kind) => kind switch
    {
        SpotlightKind.Calculation => L.Spotlight.Result,
        SpotlightKind.App => L.Spotlight.Apps,
        SpotlightKind.Action => L.Spotlight.Actions,
        SpotlightKind.Contact => L.Spotlight.Contacts,
        SpotlightKind.DmThread => L.Spotlight.Messages,
        SpotlightKind.SettingsPage => L.Spotlight.Settings,
        SpotlightKind.Shortcut => L.Spotlight.Shortcuts,
        SpotlightKind.Aetheryte => L.Apps.Maps,
        SpotlightKind.Conversation => L.Spotlight.Conversations,
        SpotlightKind.Note => L.Spotlight.Notes,
        SpotlightKind.Guide => L.Apps.Strats,
        SpotlightKind.Venue => L.Apps.Venues,
        SpotlightKind.StoreApp => L.Spotlight.Store,
        _ => L.Spotlight.Items,
    };
}
