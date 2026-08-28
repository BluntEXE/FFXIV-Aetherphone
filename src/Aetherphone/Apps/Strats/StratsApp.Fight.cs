using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp
{
    private const float RoleChipHeight = 34f;
    private const float RoleChipGap = 6f;
    private const float RoleCaptionGap = 4f;
    private const float SetupRowHeight = 42f;
    private const float PagerPillHeight = 38f;
    private const float LinkPillHeight = 30f;
    private const float PillStrokeBleed = 2f;
    private const float MaxImageHeight = 420f;
    private const float BadgeGap = 4f;
    private const float BadgeLabelGap = 10f;
    private const float SpotBarWidth = 3f;
    private const float SpotFillAlpha = 0.10f;
    private const string StratRowIdPrefix = "strats.strat:";
    private const string LinkRowIdPrefix = "strats.link:";

    private static readonly TextStyle RoleCaptionStyle = new(0.72f, FontWeight.Medium);
    private static readonly string VideoGlyph = IconGlyph.Of(FontAwesomeIcon.Video);
    private static readonly string BoardGlyph = IconGlyph.Of(FontAwesomeIcon.Map);
    private static readonly string DocumentGlyph = IconGlyph.Of(FontAwesomeIcon.FileAlt);
    private static readonly string LinkGlyph = IconGlyph.Of(FontAwesomeIcon.ExternalLinkAlt);

    private FightDoc? labelsDoc;
    private string[] stratLabels = Array.Empty<string>();
    private string[] stratRowIds = Array.Empty<string>();
    private string[][] stratBadgeTexts = Array.Empty<string[]>();
    private Vector4[][] stratBadgeInks = Array.Empty<Vector4[]>();
    private string[] tabLabels = Array.Empty<string>();
    private bool[] tabActive = Array.Empty<bool>();
    private string[][] toggleLabels = Array.Empty<string[]>();
    private bool[][] toggleActive = Array.Empty<bool[]>();
    private string[] alignmentLabels = Array.Empty<string>();
    private string[] roleLabels = Array.Empty<string>();
    private bool roleLabelsJapanese;
    private string[] roleColumnRoles = Array.Empty<string>();
    private int[][] roleColumnSlots = Array.Empty<int[]>();
    private Vector4[] roleColumnInks = Array.Empty<Vector4>();
    private int roleRows;
    private TimelineEntry[] timelineSource = Array.Empty<TimelineEntry>();
    private string[] timelineTimes = Array.Empty<string>();
    private GuideLink[] linksSource = Array.Empty<GuideLink>();
    private string[] linkGlyphs = Array.Empty<string>();
    private string[] linkRowIds = Array.Empty<string>();
    private string linksCountLabel = string.Empty;
    private float sectionsScrollY;

    private void DrawFight(Rect area, StratsView view)
    {
        var scale = UiScale.Current;
        if (!manifestStore.TryFind(view.FightKey, out var fight))
        {
            router.Pop();
            return;
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, fight.Abbrev, back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var entry = guideStore.Request(fight, false);
        var doc = entry.Doc;
        if (doc is null)
        {
            DrawGuideState(body, entry, fight, scale);
            return;
        }

        var current = ResolveCurrent(doc);
        if (current is null)
        {
            return;
        }

        EnsureLabels(doc, current);
        using (var surface = AppSurface.Begin(body))
        {
            DrawFightTitle(fight, scale);
            DrawStratPicker(doc, current, scale);
            DrawRolePicker(scale);
            DrawTabs(doc, current, scale);
            DrawToggles(doc, current, scale);
            DrawAlignment(doc, current, scale);
            DrawSetupCard(current, scale);
            DrawStratIntro(current, scale);
            DrawStratDifferences(current, scale);
            DrawPhases(current, scale);
            DrawSectionPager(doc, current, surface, scale);
            DrawResources(current, scale);
            DrawBackToTop(surface, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawGuideState(Rect body, GuideEntry entry, ManifestFight fight, float scale)
    {
        if (entry.State == StratsState.Failed)
        {
            if (EmptyState.Draw(body, ui, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Strats.GuideFailed),
                    Loc.T(L.Strats.GuideFailedHint), Loc.T(L.Strats.Retry)))
            {
                guideStore.Request(fight, true);
            }

            return;
        }

        LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 110f * scale), 13f * scale, ui.Accent,
            AppPalettes.Strats.MutedInk, Loc.T(L.Strats.GuideLoading));
    }

    private void EnsureLabels(FightDoc doc, ResolvedFight current)
    {
        if (!ReferenceEquals(labelsDoc, doc))
        {
            labelsDoc = doc;
            BuildStratLabels(doc);
            tabLabels = new string[doc.Tabs.Length];
            tabActive = new bool[doc.Tabs.Length];
            for (var index = 0; index < doc.Tabs.Length; index++)
            {
                tabLabels[index] = doc.Tabs[index].Label;
            }

            toggleLabels = new string[doc.Toggles.Length][];
            toggleActive = new bool[doc.Toggles.Length][];
            for (var toggleIndex = 0; toggleIndex < doc.Toggles.Length; toggleIndex++)
            {
                var options = doc.Toggles[toggleIndex].Options;
                var labels = new string[options.Length];
                for (var optionIndex = 0; optionIndex < options.Length; optionIndex++)
                {
                    labels[optionIndex] = options[optionIndex].Label;
                }

                toggleLabels[toggleIndex] = labels;
                toggleActive[toggleIndex] = new bool[options.Length];
            }

            alignmentLabels = new string[doc.Alignments.Length];
            for (var index = 0; index < doc.Alignments.Length; index++)
            {
                alignmentLabels[index] = doc.Alignments[index].Label;
            }

            roleLabels = Array.Empty<string>();
            tabRail.Reset();
            toggleRails.Clear();
        }

        var japanese = current.Strat.JpRoles;
        if (roleLabels.Length == 0 || roleLabelsJapanese != japanese)
        {
            roleLabelsJapanese = japanese;
            BuildRoleColumns(doc, japanese);
        }

        if (!ReferenceEquals(timelineSource, current.Timeline))
        {
            timelineSource = current.Timeline;
            timelineTimes = new string[current.Timeline.Length];
            for (var index = 0; index < current.Timeline.Length; index++)
            {
                timelineTimes[index] = FormatDuration(current.Timeline[index].StartMs);
            }
        }

        if (!ReferenceEquals(linksSource, current.Strat.Links))
        {
            linksSource = current.Strat.Links;
            linkGlyphs = new string[linksSource.Length];
            linkRowIds = new string[linksSource.Length];
            for (var index = 0; index < linksSource.Length; index++)
            {
                linkGlyphs[index] = LinkGlyphFor(linksSource[index].Url);
                linkRowIds[index] = string.Concat(LinkRowIdPrefix, index.ToString());
            }

            linksCountLabel = linksSource.Length.ToString();
        }
    }

    private void BuildStratLabels(FightDoc doc)
    {
        var count = doc.Strats.Length;
        stratLabels = new string[count];
        stratRowIds = new string[count];
        stratBadgeTexts = new string[count][];
        stratBadgeInks = new Vector4[count][];
        for (var index = 0; index < count; index++)
        {
            var strat = doc.Strats[index];
            stratLabels[index] = strat.Label;
            stratRowIds[index] = string.Concat(StratRowIdPrefix, index.ToString());
            var badges = strat.Badges;
            var texts = new string[badges.Length];
            var inks = new Vector4[badges.Length];
            for (var badgeIndex = 0; badgeIndex < badges.Length; badgeIndex++)
            {
                texts[badgeIndex] = badges[badgeIndex].Text;
                inks[badgeIndex] = BadgeInk(badges[badgeIndex].Kind);
            }

            stratBadgeTexts[index] = texts;
            stratBadgeInks[index] = inks;
        }
    }

    private Vector4 BadgeInk(string kind)
    {
        if (kind == "na" || kind.Contains("blue", StringComparison.Ordinal))
        {
            return StratsInk.Resolve("blue", ui.BodyInk, ui.MutedInk);
        }

        if (kind == "eu" || kind.Contains("yellow", StringComparison.Ordinal) ||
            kind.Contains("amber", StringComparison.Ordinal))
        {
            return StratsInk.Resolve("yellow", ui.BodyInk, ui.MutedInk);
        }

        if (kind == "oce" || kind.Contains("green", StringComparison.Ordinal))
        {
            return StratsInk.Resolve("green", ui.BodyInk, ui.MutedInk);
        }

        if (kind == "jp" || kind.Contains("red", StringComparison.Ordinal))
        {
            return StratsInk.Resolve("red", ui.BodyInk, ui.MutedInk);
        }

        return ui.MutedInk;
    }

    private void BuildRoleColumns(FightDoc doc, bool japanese)
    {
        if (doc.RoleOptions.Length > 0)
        {
            roleLabels = new string[doc.RoleOptions.Length];
            var roles = new List<string>();
            var members = new List<List<int>>();
            for (var index = 0; index < doc.RoleOptions.Length; index++)
            {
                var option = doc.RoleOptions[index];
                roleLabels[index] = option.Label;
                var column = roles.IndexOf(option.Role);
                if (column < 0)
                {
                    roles.Add(option.Role);
                    members.Add(new List<int>());
                    column = roles.Count - 1;
                }

                members[column].Add(index);
            }

            roleColumnRoles = roles.ToArray();
            roleColumnSlots = new int[roles.Count][];
            for (var column = 0; column < roles.Count; column++)
            {
                roleColumnSlots[column] = members[column].ToArray();
            }
        }
        else
        {
            roleLabels = new string[StratsRoles.SlotCount];
            for (var slot = 0; slot < StratsRoles.SlotCount; slot++)
            {
                roleLabels[slot] = StratsRoles.Label(slot, japanese);
            }

            var columns = StratsRoles.SlotCount / 2;
            roleColumnRoles = new string[columns];
            roleColumnSlots = new int[columns][];
            for (var column = 0; column < columns; column++)
            {
                roleColumnRoles[column] = StratsRoles.RoleName(column * 2);
                roleColumnSlots[column] = new[] { column * 2, column * 2 + 1 };
            }
        }

        roleColumnInks = new Vector4[roleColumnRoles.Length];
        roleRows = 0;
        for (var column = 0; column < roleColumnRoles.Length; column++)
        {
            roleColumnInks[column] = RoleInk(roleColumnRoles[column]);
            roleRows = Math.Max(roleRows, roleColumnSlots[column].Length);
        }
    }

    private Vector4 RoleInk(string role) =>
        role switch
        {
            "Tank" => StratsInk.Resolve("blue", ui.BodyInk, ui.MutedInk),
            "Healer" => StratsInk.Resolve("green", ui.BodyInk, ui.MutedInk),
            "Melee" => StratsInk.Resolve("red", ui.BodyInk, ui.MutedInk),
            "Ranged" => StratsInk.Resolve("red", ui.BodyInk, ui.MutedInk),
            _ => ui.MutedInk,
        };

    private static string RoleCaption(string role) =>
        role switch
        {
            "Tank" => Loc.T(L.Strats.RoleTank),
            "Healer" => Loc.T(L.Strats.RoleHealer),
            "Melee" => Loc.T(L.Strats.RoleMelee),
            "Ranged" => Loc.T(L.Strats.RoleRanged),
            _ => role,
        };

    private static string LinkGlyphFor(string url)
    {
        if (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("twitch.tv", StringComparison.OrdinalIgnoreCase))
        {
            return VideoGlyph;
        }

        if (url.Contains("raidplan.io", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("board.wtfdig", StringComparison.OrdinalIgnoreCase))
        {
            return BoardGlyph;
        }

        if (url.Contains("pastebin", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("docs.google", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("drive.google", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentGlyph;
        }

        return LinkGlyph;
    }

    private static string FormatDuration(int milliseconds)
    {
        var totalSeconds = Math.Max(0, milliseconds / 1000);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return string.Concat(minutes.ToString(), ":", seconds.ToString("00"));
    }

    private void DrawFightTitle(ManifestFight fight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + Metrics.Space.Xs * scale),
            fight.Title, ui.TitleInk, TextStyles.Title3, width);
        var subtitleY = origin.Y + Metrics.Space.Xs * scale + titleHeight + 2f * scale;
        Typography.Draw(drawList, new Vector2(origin.X, subtitleY), fight.Subtitle, ui.MutedInk, TextStyles.Footnote);
        var total = Metrics.Space.Xs * scale + titleHeight + 2f * scale + Typography.LineHeight(TextStyles.Footnote) +
            Metrics.Space.Md * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, total));
    }

    private void DrawStratPicker(FightDoc doc, ResolvedFight current, float scale)
    {
        ui.SectionLabel(Loc.T(L.Strats.Strategy));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowCount = doc.Strats.Length;
        UiAnchors.Report("strats.chips",
            new Rect(origin, new Vector2(origin.X + width, origin.Y + rowCount * GroupCard.DefaultRowHeight * scale)));
        var interactive = rowCount > 1;
        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(theme, rowCount);
        for (var index = 0; index < rowCount; index++)
        {
            var row = card.NextRow();
            var badgesWidth = StratBadgesWidth(index, scale);
            var reserve = badgesWidth > 0f ? badgesWidth + BadgeLabelGap * scale : 0f;
            var tapped = SettingsRow.Selectable(row, stratLabels[index], index == current.StratIndex, theme,
                stratRowIds[index], reserve, interactive);
            DrawStratBadges(drawList, row, index, badgesWidth, scale);
            if (tapped && index != current.StratIndex)
            {
                selection.StratId = doc.Strats[index].Id;
                selection.Toggles.Clear();
                TouchSelection();
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private float StratBadgesWidth(int stratIndex, float scale)
    {
        var texts = stratBadgeTexts[stratIndex];
        var total = 0f;
        for (var index = 0; index < texts.Length; index++)
        {
            total += InlineBadge.Width(texts[index], scale) + (index > 0 ? BadgeGap * scale : 0f);
        }

        return total;
    }

    private void DrawStratBadges(ImDrawListPtr drawList, Rect row, int stratIndex, float totalWidth, float scale)
    {
        var texts = stratBadgeTexts[stratIndex];
        if (texts.Length == 0)
        {
            return;
        }

        var inks = stratBadgeInks[stratIndex];
        var left = row.Max.X - SettingsRow.CheckWidth * scale - totalWidth;
        for (var index = 0; index < texts.Length; index++)
        {
            left += InlineBadge.Draw(drawList, left, row.Center.Y, texts[index], inks[index], scale) + BadgeGap * scale;
        }
    }

    private void DrawRolePicker(float scale)
    {
        ui.SectionLabel(Loc.T(L.Strats.Role));
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var columnGap = Metrics.Space.Sm * scale;
        var chipGap = RoleChipGap * scale;
        var columns = roleColumnSlots.Length;
        var columnWidth = (width - columnGap * (columns - 1)) / columns;
        var chipHeight = RoleChipHeight * scale;
        var captionLineHeight = Typography.LineHeight(RoleCaptionStyle);
        var captionHeight = captionLineHeight + RoleCaptionGap * scale;
        var gridHeight = captionHeight + roleRows * chipHeight + (roleRows - 1) * chipGap;
        UiAnchors.Report("strats.role", new Rect(origin, new Vector2(origin.X + width, origin.Y + gridHeight)));
        var activeSlot = Math.Clamp(selection.Slot, 0, roleLabels.Length - 1);
        for (var column = 0; column < columns; column++)
        {
            var left = origin.X + column * (columnWidth + columnGap);
            var caption = Typography.FitText(RoleCaption(roleColumnRoles[column]), columnWidth, RoleCaptionStyle);
            Typography.DrawCentered(drawList, new Vector2(left + columnWidth * 0.5f, origin.Y + captionLineHeight * 0.5f),
                caption, roleColumnInks[column], RoleCaptionStyle.Scale, RoleCaptionStyle.Weight);
            var slots = roleColumnSlots[column];
            for (var row = 0; row < slots.Length; row++)
            {
                var top = origin.Y + captionHeight + row * (chipHeight + chipGap);
                var rect = new Rect(new Vector2(left, top), new Vector2(left + columnWidth, top + chipHeight));
                var slot = slots[row];
                if (ui.Chip(rect, roleLabels[slot], slot == activeSlot) && slot != activeSlot)
                {
                    selection.Slot = slot;
                    TouchSelection();
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, gridHeight + Metrics.Space.Md * scale));
    }

    private void DrawTabs(FightDoc doc, ResolvedFight current, float scale)
    {
        if (doc.Tabs.Length == 0)
        {
            return;
        }

        sectionsScrollY = ImGui.GetCursorPosY();
        ui.SectionLabel(Loc.T(L.Strats.Section));
        for (var index = 0; index < tabActive.Length; index++)
        {
            tabActive[index] = index == current.TabIndex;
        }

        var tapped = tabRail.Draw(ui, tabLabels, tabActive);
        if (tapped >= 0 && tapped != current.TabIndex)
        {
            selection.Tab = tapped;
            TouchSelection();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private void DrawToggles(FightDoc doc, ResolvedFight current, float scale)
    {
        for (var toggleIndex = 0; toggleIndex < doc.Toggles.Length; toggleIndex++)
        {
            if (!current.ToggleVisible[toggleIndex])
            {
                continue;
            }

            var toggle = doc.Toggles[toggleIndex];
            var active = toggleActive[toggleIndex];
            var selected = current.ToggleOptionIndices[toggleIndex];
            for (var index = 0; index < active.Length; index++)
            {
                active[index] = index == selected;
            }

            ui.SectionLabel(toggle.Label);
            if (!toggleRails.TryGetValue(toggle.Key, out var rail))
            {
                rail = new ChipRail();
                toggleRails[toggle.Key] = rail;
            }

            var tapped = rail.Draw(ui, toggleLabels[toggleIndex], active);
            if (tapped >= 0 && tapped != selected)
            {
                selection.Toggles[toggle.Key] = toggle.Options[tapped].Value;
                TouchSelection();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }
    }

    private void DrawAlignment(FightDoc doc, ResolvedFight current, float scale)
    {
        if (doc.Alignments.Length == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Strats.Orientation));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + 38f * scale));
        var picked = SegmentStrip.Draw("strats.alignment", row, alignmentLabels, current.AlignmentIndex,
            AppPalettes.Strats, 32f, 0.85f);
        if (picked != current.AlignmentIndex)
        {
            selection.Alignment = doc.Alignments[picked].Id;
            TouchSelection();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 38f * scale + Metrics.Space.Md * scale));
    }

    private void DrawSetupCard(ResolvedFight current, float scale)
    {
        var timeline = current.Timeline;
        var links = linksSource;
        if (timeline.Length == 0 && links.Length == 0)
        {
            return;
        }

        var rowCount = 0;
        if (timeline.Length > 0)
        {
            rowCount += 1 + (timelineOpen ? timeline.Length : 0);
        }

        if (links.Length > 0)
        {
            rowCount += 1 + (linksOpen ? links.Length : 0);
        }

        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(theme, rowCount, SetupRowHeight);
        if (timeline.Length > 0)
        {
            var value = timelineOpen ? Loc.T(L.Strats.HideTimeline) : Loc.T(L.Strats.ShowTimeline);
            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Strats.Timeline), value, theme, "strats.timeline"))
            {
                timelineOpen = !timelineOpen;
            }

            if (timelineOpen)
            {
                for (var index = 0; index < timeline.Length; index++)
                {
                    DrawTimelineRow(drawList, card.NextRow(), timeline[index], index, scale);
                }
            }
        }

        if (links.Length > 0)
        {
            var value = linksOpen ? Loc.T(L.Strats.HideTimeline) : linksCountLabel;
            if (SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Strats.Sources), value, theme, "strats.sources"))
            {
                linksOpen = !linksOpen;
            }

            if (linksOpen)
            {
                for (var index = 0; index < links.Length; index++)
                {
                    DrawLinkRow(drawList, card.NextRow(), links[index], index, scale);
                }
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawTimelineRow(ImDrawListPtr drawList, Rect row, TimelineEntry item, int index, float scale)
    {
        var timeX = row.Min.X + Metrics.Space.Md * scale;
        Typography.Draw(drawList, new Vector2(timeX, row.Center.Y - Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f),
            timelineTimes[index], ui.MutedInk, TextStyles.FootnoteEmphasized);
        var dotCenter = new Vector2(timeX + 44f * scale, row.Center.Y);
        drawList.AddCircleFilled(dotCenter, 4f * scale, ImGui.GetColorU32(TimelineColor(item.Type)), 12);
        var nameX = dotCenter.X + 12f * scale;
        var name = Typography.FitText(item.Name, row.Max.X - nameX, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(nameX, row.Center.Y - Typography.LineHeight(TextStyles.Subheadline) * 0.5f),
            name, ui.BodyInk, TextStyles.Subheadline);
    }

    private void DrawLinkRow(ImDrawListPtr drawList, Rect row, GuideLink link, int index, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            SettingsRow.DrawRowHighlight(row, theme);
        }

        var iconCenter = new Vector2(row.Min.X + Metrics.Space.Md * scale + 8f * scale, row.Center.Y);
        AppSkin.Icon(drawList, iconCenter, linkGlyphs[index], ui.Accent, 0.75f);
        var labelX = iconCenter.X + 18f * scale;
        var trailingCenter = new Vector2(row.Max.X - 6f * scale, row.Center.Y);
        var labelMaxWidth = MathF.Max(1f, trailingCenter.X - 14f * scale - labelX);
        Marquee.DrawLeft(drawList, linkRowIds[index], link.Label, labelX,
            row.Center.Y - Typography.LineHeight(TextStyles.Subheadline) * 0.5f, labelMaxWidth, TextStyles.Subheadline,
            ui.BodyInk, hovered);
        AppSkin.Icon(drawList, trailingCenter, LinkGlyph, ui.MutedInk, 0.6f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            UrlActions.AskThenOpen(link.Url);
        }
    }

    private Vector4 TimelineColor(string type) =>
        type switch
        {
            "Raidwide" => StratsInk.Resolve("orange", ui.BodyInk, ui.MutedInk),
            "Tankbuster" => StratsInk.Resolve("blue", ui.BodyInk, ui.MutedInk),
            "Enrage" => StratsInk.Resolve("red", ui.BodyInk, ui.MutedInk),
            "Mechanic" => ui.Accent,
            _ => ui.MutedInk,
        };

    private void DrawStratIntro(ResolvedFight current, float scale)
    {
        var strat = current.Strat;
        if (strat.Description.IsEmpty && strat.Notes.IsEmpty)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Md * scale;
        var innerWidth = width - pad * 2f;
        var descriptionHeight = strat.Description.IsEmpty
            ? 0f
            : richText.Measure(strat.Description, innerWidth, TextStyles.Subheadline, scale);
        var notesHeight = strat.Notes.IsEmpty ? 0f : richText.Measure(strat.Notes, innerWidth, TextStyles.Footnote, scale);
        var height = pad + descriptionHeight + (descriptionHeight > 0f && notesHeight > 0f ? Metrics.Space.Sm * scale : 0f) +
            notesHeight + pad;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Card * scale);
        var y = origin.Y + pad;
        if (descriptionHeight > 0f)
        {
            richText.Draw(drawList, new Vector2(origin.X + pad, y), strat.Description, innerWidth, TextStyles.Subheadline,
                ui.BodyInk, ui.MutedInk, scale, images);
            y += descriptionHeight + (notesHeight > 0f ? Metrics.Space.Sm * scale : 0f);
        }

        if (notesHeight > 0f)
        {
            richText.Draw(drawList, new Vector2(origin.X + pad, y), strat.Notes, innerWidth, TextStyles.Footnote,
                ui.MutedInk, ui.MutedInk, scale, images);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawLinkPills(ImDrawListPtr drawList, Vector2 origin, float width, GuideLink[] links, float scale)
    {
        var cursorX = origin.X;
        var gap = Metrics.Space.Sm * scale;
        var pillHeight = LinkPillHeight * scale;
        var bleed = PillStrokeBleed * scale;
        drawList.PushClipRect(new Vector2(origin.X - bleed, origin.Y - bleed),
            new Vector2(origin.X + width + bleed, origin.Y + pillHeight + bleed), true);
        for (var index = 0; index < links.Length; index++)
        {
            var link = links[index];
            var pillWidth = AppSkin.PillWidthFor(link.Label, pillHeight);
            var rect = new Rect(new Vector2(cursorX, origin.Y), new Vector2(cursorX + pillWidth, origin.Y + pillHeight));
            if (rect.Max.X > origin.X + width && index > 0)
            {
                break;
            }

            if (ui.GhostButton(rect, link.Label))
            {
                UrlActions.AskThenOpen(link.Url);
            }

            cursorX += pillWidth + gap;
        }

        drawList.PopClipRect();
    }

    private void DrawStratDifferences(ResolvedFight current, float scale)
    {
        var differences = current.Doc.StratDifferences;
        if (differences.Length == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Strats.StratDifferences));
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Md * scale;
        var innerWidth = width - pad * 2f;
        var labelHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var height = pad;
        for (var index = 0; index < differences.Length; index++)
        {
            height += labelHeight + 2f * scale +
                richText.Measure(differences[index].Text, innerWidth, TextStyles.Footnote, scale) +
                (index < differences.Length - 1 ? Metrics.Space.Sm * scale : 0f);
        }

        height += pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Card * scale);
        var y = origin.Y + pad;
        for (var index = 0; index < differences.Length; index++)
        {
            var difference = differences[index];
            Typography.Draw(drawList, new Vector2(origin.X + pad, y), difference.Label, ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
            y += labelHeight + 2f * scale;
            y += richText.Draw(drawList, new Vector2(origin.X + pad, y), difference.Text, innerWidth, TextStyles.Footnote,
                ui.BodyInk, ui.MutedInk, scale, images);
            y += Metrics.Space.Sm * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawPhases(ResolvedFight current, float scale)
    {
        for (var phaseIndex = 0; phaseIndex < current.Phases.Length; phaseIndex++)
        {
            var phase = current.Phases[phaseIndex];
            ui.SectionHeading(phase.Name, Metrics.Space.Sm);
            DrawPhaseIntro(phase, phaseIndex, scale);
            for (var mechIndex = 0; mechIndex < phase.Mechs.Length; mechIndex++)
            {
                DrawMechanicCard(current, phase.Mechs[mechIndex], phaseIndex, mechIndex, scale);
            }
        }
    }

    private void DrawSectionPager(FightDoc doc, ResolvedFight current, in AppSurface.SurfaceScope surface, float scale)
    {
        if (doc.Tabs.Length <= 1)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = Metrics.Space.Sm * scale;
        var half = (width - gap) * 0.5f;
        var captionHeight = Typography.LineHeight(TextStyles.Caption2) + 2f * scale;
        var pillTop = origin.Y + captionHeight;
        var pillHeight = PagerPillHeight * scale;
        if (current.TabIndex > 0)
        {
            Typography.Draw(drawList, origin, Loc.T(L.Common.Previous), ui.MutedInk, TextStyles.Caption2);
            var rect = new Rect(new Vector2(origin.X, pillTop), new Vector2(origin.X + half, pillTop + pillHeight));
            if (ui.PillButton(rect, tabLabels[current.TabIndex - 1], false, "strats.pager.previous"))
            {
                SwitchSection(current.TabIndex - 1, surface, scale);
            }
        }

        if (current.TabIndex < doc.Tabs.Length - 1)
        {
            var left = origin.X + half + gap;
            var caption = Loc.T(L.Common.Next);
            var captionWidth = Typography.Measure(caption, TextStyles.Caption2).X;
            Typography.Draw(drawList, new Vector2(origin.X + width - captionWidth, origin.Y), caption, ui.MutedInk,
                TextStyles.Caption2);
            var rect = new Rect(new Vector2(left, pillTop), new Vector2(origin.X + width, pillTop + pillHeight));
            if (ui.PillButton(rect, tabLabels[current.TabIndex + 1], true, "strats.pager.next"))
            {
                SwitchSection(current.TabIndex + 1, surface, scale);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, captionHeight + pillHeight + Metrics.Space.Md * scale));
    }

    private void SwitchSection(int tabIndex, in AppSurface.SurfaceScope surface, float scale)
    {
        selection.Tab = tabIndex;
        TouchSelection();
        surface.JumpTo(MathF.Max(0f, sectionsScrollY - Metrics.Space.Sm * scale));
    }

    private void DrawBackToTop(in AppSurface.SurfaceScope surface, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var label = Loc.T(L.Strats.BackToTop);
        var pillHeight = LinkPillHeight * scale;
        var pillWidth = MathF.Min(width, AppSkin.PillWidthFor(label, pillHeight));
        var left = origin.X + (width - pillWidth) * 0.5f;
        var rect = new Rect(new Vector2(left, origin.Y), new Vector2(left + pillWidth, origin.Y + pillHeight));
        if (ui.GhostButton(rect, label))
        {
            surface.JumpToTop();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, pillHeight + Metrics.Space.Sm * scale));
    }

    private void DrawPhaseIntro(ResolvedPhase phase, int phaseIndex, float scale)
    {
        var hasText = phase.Description is not null;
        var hasImage = phase.Image is not null;
        var hasLinks = phase.Links.Length > 0 || phase.BoardUrl.Length > 0;
        if (!hasText && !hasImage && !hasLinks)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var y = origin.Y;
        if (hasText)
        {
            y += richText.Draw(drawList, new Vector2(origin.X, y), phase.Description!, width, TextStyles.Subheadline,
                ui.BodyInk, ui.MutedInk, scale, images);
            y += Metrics.Space.Sm * scale;
        }

        if (hasImage)
        {
            var frameHeight = MathF.Min(MaxImageHeight * scale, SpotlightImage.HeightFor(phase.Image!, string.Empty, width));
            var frame = new Rect(new Vector2(origin.X, y), new Vector2(origin.X + width, y + frameHeight));
            DrawImageFrame(drawList, frame, phase.Image!, phase.Spotlight, string.Empty, scale,
                new StratsView(StratsScreen.Viewer, selection.FightKey, phaseIndex));
            y = frame.Max.Y + Metrics.Space.Sm * scale;
        }

        if (hasLinks)
        {
            DrawPhaseLinks(drawList, new Vector2(origin.X, y), width, phase, scale);
            y += LinkPillHeight * scale + Metrics.Space.Sm * scale;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Metrics.Space.Xs * scale));
    }

    private void DrawPhaseLinks(ImDrawListPtr drawList, Vector2 origin, float width, ResolvedPhase phase, float scale)
    {
        var cursorX = origin.X;
        var gap = Metrics.Space.Sm * scale;
        var pillHeight = LinkPillHeight * scale;
        if (phase.BoardUrl.Length > 0)
        {
            var boardLabel = Loc.T(L.Strats.Board);
            var boardWidth = AppSkin.PillWidthFor(boardLabel, pillHeight);
            var rect = new Rect(new Vector2(cursorX, origin.Y), new Vector2(cursorX + boardWidth, origin.Y + pillHeight));
            if (ui.PillButton(rect, boardLabel, false, "strats.board"))
            {
                UrlActions.AskThenOpen(phase.BoardUrl);
            }

            cursorX += boardWidth + gap;
        }

        var bleed = PillStrokeBleed * scale;
        drawList.PushClipRect(new Vector2(cursorX - bleed, origin.Y - bleed),
            new Vector2(origin.X + width + bleed, origin.Y + pillHeight + bleed), true);
        for (var index = 0; index < phase.Links.Length; index++)
        {
            var link = phase.Links[index];
            var pillWidth = AppSkin.PillWidthFor(link.Label, pillHeight);
            if (cursorX + pillWidth > origin.X + width && cursorX > origin.X)
            {
                break;
            }

            var rect = new Rect(new Vector2(cursorX, origin.Y), new Vector2(cursorX + pillWidth, origin.Y + pillHeight));
            if (ui.GhostButton(rect, link.Label))
            {
                UrlActions.AskThenOpen(link.Url);
            }

            cursorX += pillWidth + gap;
        }

        drawList.PopClipRect();
    }

    private void DrawMechanicCard(ResolvedFight current, ResolvedMechanic mech, int phaseIndex, int mechIndex,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Md * scale;
        var innerWidth = width - pad * 2f;
        var gap = Metrics.Space.Sm * scale;
        var separate = current.Doc.SeparateDescriptionAction;

        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var descriptionHeight = mech.Description is null
            ? 0f
            : richText.Measure(mech.Description, innerWidth, TextStyles.Subheadline, scale);
        var actionHeight = mech.Action is null ? 0f : richText.Measure(mech.Action, innerWidth, TextStyles.Subheadline, scale);
        var mechImageHeight = mech.Image is null
            ? 0f
            : MathF.Min(MaxImageHeight * scale, SpotlightImage.HeightFor(mech.Image, mech.Transform, innerWidth));
        var arenaHeight = mech.Arena is null ? 0f : innerWidth;
        var notesHeight = mech.Notes is null ? 0f : richText.Measure(mech.Notes, innerWidth, TextStyles.Footnote, scale);
        var labelHeight = Typography.LineHeight(TextStyles.Caption2);
        var linksHeight = mech.Links.Length == 0 ? 0f : LinkPillHeight * scale;

        var spotPad = Metrics.Space.Sm * scale;
        var spotInnerWidth = innerWidth - SpotBarWidth * scale - spotPad * 2f;
        var playerTextHeight = mech.PlayerText is null
            ? 0f
            : richText.Measure(mech.PlayerText, spotInnerWidth, TextStyles.SubheadlineEmphasized, scale);
        var playerImageHeight = mech.PlayerImage is null
            ? 0f
            : MathF.Min(MaxImageHeight * scale, SpotlightImage.HeightFor(mech.PlayerImage, mech.PlayerTransform, spotInnerWidth));
        var spotHeight = playerTextHeight > 0f || playerImageHeight > 0f
            ? spotPad + labelHeight + 2f * scale + playerTextHeight +
              (playerTextHeight > 0f && playerImageHeight > 0f ? gap : 0f) + playerImageHeight + spotPad
            : 0f;

        var height = pad + nameHeight;
        height += Block(descriptionHeight, separate ? labelHeight + 2f * scale : 0f, gap);
        height += Block(actionHeight, separate ? labelHeight + 2f * scale : 0f, gap);
        height += Block(mechImageHeight, 0f, gap);
        height += Block(arenaHeight, 0f, gap);
        height += Block(spotHeight, 0f, gap);
        height += Block(notesHeight, 0f, gap);
        height += Block(linksHeight, 0f, gap);
        height += pad;

        var max = new Vector2(origin.X + width, origin.Y + height);
        if (ImGui.IsRectVisible(origin, max))
        {
            ui.Card(drawList, origin, max, Metrics.Radius.Card * scale);
            var x = origin.X + pad;
            var y = origin.Y + pad;
            Typography.Draw(drawList, new Vector2(x, y), Typography.FitText(mech.Name, innerWidth, TextStyles.Headline),
                ui.TitleInk, TextStyles.Headline);
            y += nameHeight;

            if (descriptionHeight > 0f)
            {
                y += gap;
                if (separate)
                {
                    Typography.Draw(drawList, new Vector2(x, y), Loc.T(L.Strats.WhatHappens), ui.MutedInk, TextStyles.Caption2);
                    y += labelHeight + 2f * scale;
                }

                richText.Draw(drawList, new Vector2(x, y), mech.Description!, innerWidth, TextStyles.Subheadline,
                    ui.BodyInk, ui.MutedInk, scale, images);
                y += descriptionHeight;
            }

            if (actionHeight > 0f)
            {
                y += gap;
                if (separate)
                {
                    Typography.Draw(drawList, new Vector2(x, y), Loc.T(L.Strats.WhatToDo), ui.Accent, TextStyles.Caption2);
                    y += labelHeight + 2f * scale;
                }

                richText.Draw(drawList, new Vector2(x, y), mech.Action!, innerWidth, TextStyles.Subheadline, ui.BodyInk,
                    ui.MutedInk, scale, images);
                y += actionHeight;
            }

            if (mechImageHeight > 0f)
            {
                y += gap;
                var frame = new Rect(new Vector2(x, y), new Vector2(x + innerWidth, y + mechImageHeight));
                DrawImageFrame(drawList, frame, mech.Image!, null, mech.Transform, scale,
                    new StratsView(StratsScreen.Viewer, selection.FightKey, phaseIndex, mechIndex));
                y += mechImageHeight;
            }

            if (arenaHeight > 0f)
            {
                y += gap;
                var stage = new Rect(new Vector2(x, y), new Vector2(x + innerWidth, y + arenaHeight));
                ArenaDiagramView.Draw(drawList, stage, mech.Arena!, scale, ui.BodyInk);
                if (mech.Arena!.FallbackUrl.Length > 0)
                {
                    DrawArenaFallback(stage, mech.Arena.FallbackUrl, scale);
                }

                y += arenaHeight;
            }

            if (spotHeight > 0f)
            {
                y += gap;
                var panel = new Rect(new Vector2(x, y), new Vector2(x + innerWidth, y + spotHeight));
                DrawSpotPanel(drawList, panel, mech, spotPad, spotInnerWidth, labelHeight, playerTextHeight,
                    playerImageHeight, gap, scale, phaseIndex, mechIndex);
                y += spotHeight;
            }

            if (notesHeight > 0f)
            {
                y += gap;
                richText.Draw(drawList, new Vector2(x, y), mech.Notes!, innerWidth, TextStyles.Footnote, ui.MutedInk,
                    ui.MutedInk, scale, images);
                y += notesHeight;
            }

            if (linksHeight > 0f)
            {
                y += gap;
                DrawLinkPills(drawList, new Vector2(x, y), innerWidth, mech.Links, scale);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawSpotPanel(ImDrawListPtr drawList, Rect panel, ResolvedMechanic mech, float spotPad,
        float spotInnerWidth, float labelHeight, float playerTextHeight, float playerImageHeight, float gap, float scale,
        int phaseIndex, int mechIndex)
    {
        Squircle.Fill(drawList, panel.Min, panel.Max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, SpotFillAlpha)));
        var barWidth = SpotBarWidth * scale;
        Squircle.Fill(drawList, new Vector2(panel.Min.X, panel.Min.Y + spotPad),
            new Vector2(panel.Min.X + barWidth, panel.Max.Y - spotPad), barWidth * 0.5f, ImGui.GetColorU32(ui.Accent));
        var x = panel.Min.X + barWidth + spotPad;
        var y = panel.Min.Y + spotPad;
        Typography.Draw(drawList, new Vector2(x, y), Loc.T(L.Strats.ForYou), ui.Accent, TextStyles.Caption2);
        y += labelHeight + 2f * scale;
        if (playerTextHeight > 0f)
        {
            richText.Draw(drawList, new Vector2(x, y), mech.PlayerText!, spotInnerWidth, TextStyles.SubheadlineEmphasized,
                ui.TitleInk, ui.MutedInk, scale, images);
            y += playerTextHeight + (playerImageHeight > 0f ? gap : 0f);
        }

        if (playerImageHeight > 0f)
        {
            var frame = new Rect(new Vector2(x, y), new Vector2(x + spotInnerWidth, y + playerImageHeight));
            DrawImageFrame(drawList, frame, mech.PlayerImage!, mech.PlayerSpotlight, mech.PlayerTransform, scale,
                new StratsView(StratsScreen.Viewer, selection.FightKey, phaseIndex, mechIndex, true));
        }
    }

    private static float Block(float contentHeight, float labelHeight, float gap) =>
        contentHeight > 0f ? gap + labelHeight + contentHeight : 0f;

    private void DrawImageFrame(ImDrawListPtr drawList, Rect frame, ImageRef image, SpotlightMask? mask,
        string transform, float scale, StratsView viewerRoute)
    {
        var texture = images.Sized(StratsContent.Url(image.Key), frame.Width);
        SpotlightImage.Draw(drawList, frame, texture, mask, transform, Metrics.Radius.Md * scale, scale,
            SpotlightImage.PlaceholderFor(theme), ui.Accent);
        var hovered = UiInteract.Hover(frame.Min, frame.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            UiInteract.HoverHighlight(drawList, frame.Min, frame.Max, Metrics.Radius.Md * scale);
        }

        if (texture is not null && UiInteract.Click(frame.Min, frame.Max, hovered))
        {
            OpenViewer(viewerRoute);
        }
    }

    private void DrawArenaFallback(Rect stage, string url, float scale)
    {
        var label = Loc.T(L.Strats.OpenOnSite);
        var pillHeight = LinkPillHeight * scale;
        var pillWidth = AppSkin.PillWidthFor(label, pillHeight);
        var rect = new Rect(new Vector2(stage.Max.X - pillWidth - Metrics.Space.Sm * scale, stage.Max.Y - pillHeight - Metrics.Space.Sm * scale),
            new Vector2(stage.Max.X - Metrics.Space.Sm * scale, stage.Max.Y - Metrics.Space.Sm * scale));
        if (ui.GhostButton(rect, label))
        {
            UrlActions.AskThenOpen(url);
        }
    }

    private void DrawResources(ResolvedFight current, float scale)
    {
        var resources = current.Doc.Resources;
        if (resources is null)
        {
            return;
        }

        ui.SectionHeading(resources.Title.Length > 0 ? resources.Title : Loc.T(L.Strats.Resources), Metrics.Space.Sm);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Md * scale;
        var innerWidth = width - pad * 2f;
        var textHeight = resources.Text.IsEmpty ? 0f : richText.Measure(resources.Text, innerWidth, TextStyles.Footnote, scale);
        var linksHeight = resources.Links.Length == 0 ? 0f : LinkPillHeight * scale;
        var height = pad + textHeight + (textHeight > 0f && linksHeight > 0f ? Metrics.Space.Sm * scale : 0f) + linksHeight + pad;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Card * scale);
        var y = origin.Y + pad;
        if (textHeight > 0f)
        {
            richText.Draw(drawList, new Vector2(origin.X + pad, y), resources.Text, innerWidth, TextStyles.Footnote,
                ui.BodyInk, ui.MutedInk, scale, images);
            y += textHeight + (linksHeight > 0f ? Metrics.Space.Sm * scale : 0f);
        }

        if (linksHeight > 0f)
        {
            DrawLinkPills(drawList, new Vector2(origin.X + pad, y), innerWidth, resources.Links, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }
}
