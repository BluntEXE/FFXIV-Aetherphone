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
    private const float TimelineRowHeight = 36f;
    private const float LinkPillHeight = 30f;
    private const float MaxImageHeight = 420f;
    private const int RoleChipsPerRow = 4;

    private FightDoc? labelsDoc;
    private string[] stratLabels = Array.Empty<string>();
    private bool[] stratActive = Array.Empty<bool>();
    private string[] tabLabels = Array.Empty<string>();
    private bool[] tabActive = Array.Empty<bool>();
    private string[][] toggleLabels = Array.Empty<string[]>();
    private bool[][] toggleActive = Array.Empty<bool[]>();
    private string[] alignmentLabels = Array.Empty<string>();
    private string[] roleLabels = Array.Empty<string>();
    private bool roleLabelsJapanese;
    private TimelineEntry[] timelineSource = Array.Empty<TimelineEntry>();
    private string[] timelineTimes = Array.Empty<string>();

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
        var actionCenter = new Vector2(area.Max.X - 22f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        if (ui.IconButton(actionCenter, 14f * scale, FontAwesomeIcon.InfoCircle.ToIconString(),
                AppPalettes.Strats.BodyInk, AppSkin.Transparent, 0.9f, Loc.T(L.Strats.About)))
        {
            router.Push(new StratsView(StratsScreen.About));
        }

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
        using (AppSurface.Begin(body))
        {
            DrawFightTitle(fight, scale);
            DrawStratPicker(doc, current, scale);
            DrawRolePicker(doc, current, scale);
            DrawTabs(doc, current, scale);
            DrawToggles(doc, current, scale);
            DrawAlignment(doc, current, scale);
            DrawTimeline(current, scale);
            DrawStratIntro(current, scale);
            DrawStratDifferences(current, scale);
            DrawPhases(current, scale);
            DrawResources(current, scale);
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
            stratLabels = new string[doc.Strats.Length];
            stratActive = new bool[doc.Strats.Length];
            for (var index = 0; index < doc.Strats.Length; index++)
            {
                stratLabels[index] = StratLabel(doc.Strats[index]);
            }

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
            stratRail.Reset();
            tabRail.Reset();
            toggleRails.Clear();
        }

        var japanese = current.Strat.JpRoles;
        if (roleLabels.Length == 0 || roleLabelsJapanese != japanese)
        {
            roleLabelsJapanese = japanese;
            if (doc.RoleOptions.Length > 0)
            {
                roleLabels = new string[doc.RoleOptions.Length];
                for (var index = 0; index < doc.RoleOptions.Length; index++)
                {
                    roleLabels[index] = doc.RoleOptions[index].Label;
                }
            }
            else
            {
                roleLabels = new string[StratsRoles.SlotCount];
                for (var index = 0; index < StratsRoles.SlotCount; index++)
                {
                    roleLabels[index] = StratsRoles.Label(index, japanese);
                }
            }
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
    }

    private static string StratLabel(StratVariant strat)
    {
        if (strat.Badges.Length == 0)
        {
            return strat.Label;
        }

        var builder = new System.Text.StringBuilder(strat.Label);
        builder.Append("  ·");
        for (var index = 0; index < strat.Badges.Length; index++)
        {
            builder.Append(' ').Append(strat.Badges[index].Text);
        }

        return builder.ToString();
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
        if (doc.Strats.Length <= 1)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Strats.Strategy));
        for (var index = 0; index < stratActive.Length; index++)
        {
            stratActive[index] = index == current.StratIndex;
        }

        var tapped = stratRail.Draw(ui, stratLabels, stratActive, "strats.chips");
        if (tapped >= 0 && tapped != current.StratIndex)
        {
            selection.StratId = doc.Strats[tapped].Id;
            selection.Toggles.Clear();
            TouchSelection();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private void DrawRolePicker(FightDoc doc, ResolvedFight current, float scale)
    {
        ui.SectionLabel(Loc.T(L.Strats.Role));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = Metrics.Space.Sm * scale;
        var perRow = Math.Min(RoleChipsPerRow, roleLabels.Length);
        var chipWidth = (width - gap * (perRow - 1)) / perRow;
        var chipHeight = RoleChipHeight * scale;
        var rows = (roleLabels.Length + perRow - 1) / perRow;
        var gridHeight = rows * chipHeight + (rows - 1) * gap;
        UiAnchors.Report("strats.role", new Rect(origin, new Vector2(origin.X + width, origin.Y + gridHeight)));
        var activeSlot = Math.Clamp(selection.Slot, 0, roleLabels.Length - 1);
        for (var index = 0; index < roleLabels.Length; index++)
        {
            var column = index % perRow;
            var row = index / perRow;
            var min = new Vector2(origin.X + column * (chipWidth + gap), origin.Y + row * (chipHeight + gap));
            var rect = new Rect(min, new Vector2(min.X + chipWidth, min.Y + chipHeight));
            if (ui.Chip(rect, roleLabels[index], index == activeSlot) && index != activeSlot)
            {
                selection.Slot = index;
                TouchSelection();
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

    private void DrawTimeline(ResolvedFight current, float scale)
    {
        if (current.Timeline.Length == 0)
        {
            return;
        }

        var header = GroupCard.Begin(theme, 1);
        var label = timelineOpen ? Loc.T(L.Strats.HideTimeline) : Loc.T(L.Strats.ShowTimeline);
        if (SettingsRow.Disclosure(header.NextRow(), Loc.T(L.Strats.Timeline), label, theme, "strats.timeline"))
        {
            timelineOpen = !timelineOpen;
        }

        header.End();
        if (!timelineOpen)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(theme, current.Timeline.Length, TimelineRowHeight);
        for (var index = 0; index < current.Timeline.Length; index++)
        {
            var item = current.Timeline[index];
            var row = card.NextRow();
            var pad = Metrics.Space.Lg * scale;
            var timeColor = ui.MutedInk;
            Typography.Draw(drawList, new Vector2(row.Min.X + pad, row.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
                timelineTimes[index], timeColor, TextStyles.FootnoteEmphasized);
            var dotCenter = new Vector2(row.Min.X + pad + 44f * scale, row.Center.Y);
            drawList.AddCircleFilled(dotCenter, 4f * scale, ImGui.GetColorU32(TimelineColor(item.Type)), 12);
            var nameX = dotCenter.X + 12f * scale;
            var name = Typography.FitText(item.Name, row.Max.X - pad - nameX, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(nameX, row.Center.Y - Typography.LineHeight(TextStyles.Subheadline) * 0.5f),
                name, ui.BodyInk, TextStyles.Subheadline);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
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
        var hasText = !strat.Description.IsEmpty || !strat.Notes.IsEmpty;
        if (!hasText && strat.Links.Length == 0)
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
        var linksHeight = strat.Links.Length == 0 ? 0f : LinkPillHeight * scale + Metrics.Space.Sm * scale;
        var height = pad + descriptionHeight + (descriptionHeight > 0f && notesHeight > 0f ? Metrics.Space.Sm * scale : 0f) +
            notesHeight + (hasText && linksHeight > 0f ? Metrics.Space.Sm * scale : 0f) + linksHeight + pad;
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
            y += notesHeight;
        }

        if (linksHeight > 0f)
        {
            y += hasText ? Metrics.Space.Sm * scale : 0f;
            DrawLinkPills(drawList, new Vector2(origin.X + pad, y), innerWidth, strat.Links, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawLinkPills(ImDrawListPtr drawList, Vector2 origin, float width, GuideLink[] links, float scale)
    {
        var cursorX = origin.X;
        var gap = Metrics.Space.Sm * scale;
        var pillHeight = LinkPillHeight * scale;
        drawList.PushClipRect(origin, new Vector2(origin.X + width, origin.Y + pillHeight), true);
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

        drawList.PushClipRect(new Vector2(cursorX, origin.Y), new Vector2(origin.X + width, origin.Y + pillHeight), true);
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
        var playerTextHeight = mech.PlayerText is null
            ? 0f
            : richText.Measure(mech.PlayerText, innerWidth, TextStyles.SubheadlineEmphasized, scale);
        var mechImageHeight = mech.Image is null
            ? 0f
            : MathF.Min(MaxImageHeight * scale, SpotlightImage.HeightFor(mech.Image, mech.Transform, innerWidth));
        var playerImageHeight = mech.PlayerImage is null
            ? 0f
            : MathF.Min(MaxImageHeight * scale, SpotlightImage.HeightFor(mech.PlayerImage, mech.PlayerTransform, innerWidth));
        var arenaHeight = mech.Arena is null ? 0f : innerWidth;
        var notesHeight = mech.Notes is null ? 0f : richText.Measure(mech.Notes, innerWidth, TextStyles.Footnote, scale);
        var labelHeight = Typography.LineHeight(TextStyles.Caption2);
        var linksHeight = mech.Links.Length == 0 ? 0f : LinkPillHeight * scale;

        var height = pad + nameHeight;
        height += Block(descriptionHeight, separate ? labelHeight + 2f * scale : 0f, gap);
        height += Block(actionHeight, separate ? labelHeight + 2f * scale : 0f, gap);
        height += Block(mechImageHeight, 0f, gap);
        height += Block(arenaHeight, 0f, gap);
        height += Block(playerTextHeight, labelHeight + 2f * scale, gap);
        height += Block(playerImageHeight, 0f, gap);
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

            if (playerTextHeight > 0f)
            {
                y += gap;
                Typography.Draw(drawList, new Vector2(x, y), Loc.T(L.Strats.ForYou), ui.Accent, TextStyles.Caption2);
                y += labelHeight + 2f * scale;
                richText.Draw(drawList, new Vector2(x, y), mech.PlayerText!, innerWidth, TextStyles.SubheadlineEmphasized,
                    ui.TitleInk, ui.MutedInk, scale, images);
                y += playerTextHeight;
            }

            if (playerImageHeight > 0f)
            {
                y += gap;
                var frame = new Rect(new Vector2(x, y), new Vector2(x + innerWidth, y + playerImageHeight));
                DrawImageFrame(drawList, frame, mech.PlayerImage!, mech.PlayerSpotlight, mech.PlayerTransform, scale,
                    new StratsView(StratsScreen.Viewer, selection.FightKey, phaseIndex, mechIndex, true));
                y += playerImageHeight;
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
            zoom.Reset();
            router.Push(viewerRoute);
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
