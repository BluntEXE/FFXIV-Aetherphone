using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class TableBrowser
{
    private const float PillHeight = 44f;
    private const float RowGap = 10f;
    private const float FieldHeight = 40f;
    private const int TokenBufferLength = 80;

    private static readonly LocString[] FilterLabels =
    {
        L.Casino.TableFilterAll,
        L.Casino.TableFilterOpenSeats,
        L.Casino.TableFilterLowStakes,
        L.Casino.TableFilterHighStakes,
        L.Casino.TableFilterMine,
    };

    private readonly CasinoTablesStore tables;
    private readonly Action<string> openTable;
    private readonly Action<string> openDoor;
    private readonly ChipRail rail = new();
    private readonly string[] filterLabels = new string[FilterLabels.Length];
    private readonly bool[] filterActive = new bool[FilterLabels.Length];

    private int filterIndex;
    private string tokenBuffer = string.Empty;
    private string inlineReason = string.Empty;

    public TableBrowser(CasinoTablesStore tables, Action<string> openTable, Action<string> openDoor)
    {
        this.tables = tables;
        this.openTable = openTable;
        this.openDoor = openDoor;
    }

    public CasinoTableFilter Filter => CasinoTableFilters.All[filterIndex];

    public void Enter()
    {
        inlineReason = string.Empty;
        rail.Reset();
        tables.RefreshNow();
    }

    public void Reset()
    {
        inlineReason = string.Empty;
        tokenBuffer = string.Empty;
        rail.Reset();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        ConsumeOutcomes();
        using var surface = AppSurface.Begin(body);

        DrawQuickSeatCard(ui, scale);
        if (inlineReason.Length > 0)
        {
            DrawInlineReason(ui, scale);
        }

        DrawFilters(ui);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        DrawRows(ui, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        ui.SectionHeading(Loc.T(L.Casino.PrivateHeading), 4f);
        DrawHostRow(ui, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        DrawJoinByToken(ui, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void ConsumeOutcomes()
    {
        var directoryFailed = tables.TakeTablesFailure();
        var intentFailed = tables.TakeIntentFailure();
        if (directoryFailed || intentFailed)
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        var notice = tables.TakeNoticeOutcome();
        if (notice is not null)
        {
            inlineReason = notice.Granted ? string.Empty : notice.Reason;
        }
    }

    private void DrawQuickSeatCard(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var titleSize = Typography.Measure(Loc.T(L.Casino.QuickSeatTitle), TextStyles.SubheadlineEmphasized);
        var hintText = Typography.FitText(Loc.T(L.Casino.QuickSeatHint), width - pad * 2f, TextStyles.Footnote);
        var hintSize = Typography.Measure(hintText, TextStyles.Footnote);
        var height = 12f * scale + titleSize.Y + 6f * scale + hintSize.Y + 12f * scale
            + PillHeight * scale + 12f * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding);
        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);

        Typography.Draw(drawList, new Vector2(card.Min.X + pad, card.Min.Y + 12f * scale),
            Loc.T(L.Casino.QuickSeatTitle), ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList,
            new Vector2(card.Min.X + pad, card.Min.Y + 12f * scale + titleSize.Y + 6f * scale), hintText,
            ui.MutedInk, TextStyles.Footnote);

        var pillRect = new Rect(new Vector2(card.Min.X + pad, card.Max.Y - PillHeight * scale - 12f * scale),
            new Vector2(card.Max.X - pad, card.Max.Y - 12f * scale));
        if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.QuickSeatAction), true, !tables.IntentInFlight, ui.Theme))
        {
            inlineReason = string.Empty;
            tables.QuickSeat(CasinoStakeTiers.From(Filter));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawInlineReason(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
        var pad = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk, TextStyles.Footnote,
            width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawFilters(AppSkin ui)
    {
        for (var index = 0; index < FilterLabels.Length; index++)
        {
            filterLabels[index] = Loc.T(FilterLabels[index]);
            filterActive[index] = index == filterIndex;
        }

        var tapped = rail.Draw(ui, filterLabels, filterActive);
        if (tapped >= 0)
        {
            filterIndex = tapped;
        }
    }

    private void DrawRows(AppSkin ui, float scale)
    {
        var directory = tables.Tables;
        var filter = Filter;
        var width = ScrollLayout.StableContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        var drawn = 0;
        for (var index = 0; index < directory.Length; index++)
        {
            var row = directory[index];
            if (!CasinoTableFilters.Matches(filter, row))
            {
                continue;
            }

            var origin = ImGui.GetCursorScreenPos();
            var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + TableRow.Height * scale));
            if (TableRow.Draw(drawList, rect, ui, ViewOf(row), scale))
            {
                inlineReason = string.Empty;
                if (CasinoTableFilters.IsPrivate(row))
                {
                    openDoor(row.TableId);
                }
                else
                {
                    openTable(row.TableId);
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, TableRow.Height * scale + RowGap * scale));
            drawn++;
        }

        if (drawn > 0)
        {
            return;
        }

        DrawEmptyRow(ui, scale, tables.Loaded ? L.Casino.TablesEmpty : L.Casino.TablesLoading);
    }

    private static void DrawEmptyRow(AppSkin ui, float scale, LocString message)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var text = Loc.T(message);
        var pad = 16f * scale;
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), text, ui.MutedInk, TextStyles.Footnote,
            width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
    }

    private void DrawHostRow(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = 60f * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        ui.Card(drawList, row.Min, row.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconCenter = new Vector2(row.Min.X + 26f * scale, row.Center.Y);
        drawList.AddCircleFilled(iconCenter, 15f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 32);
        AppSkin.Icon(drawList, iconCenter, IconGlyph.Of(FontAwesomeIcon.UserFriends), ui.Accent, 0.85f);

        var textLeft = row.Min.X + 48f * scale;
        var textWidth = row.Width - 62f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 12f * scale),
            Typography.FitText(Loc.T(L.Casino.HostTableAction), textWidth, TextStyles.SubheadlineEmphasized),
            ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 32f * scale),
            Typography.FitText(Loc.T(L.Casino.HostTableHint), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);

        var clicked = UiInteract.Click(row.Min, row.Max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (clicked && !tables.IntentInFlight)
        {
            inlineReason = string.Empty;
            tables.CreatePrivateTable(CasinoStakeTiers.From(Filter));
        }
    }

    private void DrawJoinByToken(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(drawList, origin, Loc.T(L.Casino.JoinByInvite), ui.MutedInk, TextStyles.FootnoteEmphasized);
        var fieldTop = origin.Y + 20f * scale;
        var pillWidth = 110f * scale;
        var fieldMin = new Vector2(origin.X, fieldTop);
        var fieldMax = new Vector2(origin.X + width - pillWidth - 8f * scale, fieldTop + FieldHeight * scale);
        Squircle.Fill(drawList, fieldMin, fieldMax, Metrics.Radius.Field * scale,
            ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 10f * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldMax.X - fieldMin.X - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##casinoInviteToken", Loc.T(L.Casino.JoinByInviteHint), ref tokenBuffer,
                TokenBufferLength);
        }

        var pillRect = new Rect(new Vector2(fieldMax.X + 8f * scale, fieldTop),
            new Vector2(origin.X + width, fieldTop + FieldHeight * scale));
        var parsed = CasinoShare.TryParse(tokenBuffer, out var tableId);
        if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.JoinAction), true,
                parsed && !tables.IntentInFlight, ui.Theme) && parsed)
        {
            inlineReason = string.Empty;
            tables.ResolveToken(tableId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 20f * scale + FieldHeight * scale));
    }

    private static TableRowView ViewOf(CasinoTableRowDto row)
    {
        var full = !CasinoTableFilters.HasOpenSeat(row);
        var isPrivate = CasinoTableFilters.IsPrivate(row);
        var name = isPrivate && row.OwnerName.Length > 0
            ? Loc.T(L.Casino.TableHostedBy, row.OwnerName)
            : Loc.T(L.Casino.TableUnnamed);
        var stakes = Loc.T(L.Casino.TableStakes, row.MinBet.ToString("N0", Loc.Culture),
            row.MaxBet.ToString("N0", Loc.Culture));
        var seats = Loc.T(L.Casino.TableSeats, row.SeatedCount.ToString(Loc.Culture),
            row.MaxSeats.ToString(Loc.Culture));
        var watching = CasinoTableFilters.SpectatorsOf(row);
        var spectators = watching > 0
            ? Loc.T(L.Casino.TableSpectators, watching.ToString(Loc.Culture))
            : string.Empty;
        return new TableRowView(name, stakes, seats, spectators, full, isPrivate, isPrivate, !row.Admitted);
    }
}
