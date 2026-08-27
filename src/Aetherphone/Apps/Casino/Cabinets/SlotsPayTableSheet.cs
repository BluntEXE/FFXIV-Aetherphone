using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class SlotsPayTableSheet
{
    private const float RowHeight = 40f;
    private const float PanelHeightShare = 0.78f;
    private const float TableLeftInset = 44f;
    private const float CellPadX = 8f;
    private const float MinPayFontFraction = 0.7f;

    private readonly SheetSurface sheet = new("casino.slotsPayTable");
    private readonly Action<Rect> drawSheetBody;

    private AppSkin skin = null!;
    private long stake;

    public SlotsPayTableSheet()
    {
        drawSheetBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open()
    {
        sheet.Open();
    }

    public void Close()
    {
        sheet.Close();
    }

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui, long stake)
    {
        skin = ui;
        this.stake = stake;
        sheet.Draw(screen, ui.Theme, Loc.T(L.Casino.SlotsPays), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##slotsPayRows", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawRows(skin, UiScale.Current, stake);
        }
    }

    private static void DrawRows(AppSkin ui, float scale, long stake)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();
        var columnWidth = ColumnWidth(width, scale);
        var subtitle = Loc.T(L.Casino.SlotsPaysMatches);
        var subtitleOrigin = ImGui.GetCursorScreenPos();
        var subtitleBlock = Typography.MeasureWrappedBlock(subtitle, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(subtitleOrigin, subtitle, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, subtitleBlock.Y + 10f * scale));

        var headerOrigin = ImGui.GetCursorScreenPos();
        for (var column = 0; column < 3; column++)
        {
            DrawPayCell(drawList, new Vector2(ColumnCenterX(headerOrigin.X, width, column, scale),
                headerOrigin.Y + 8f * scale), GameNumber.Label(column + 3), ui.MutedInk,
                TextStyles.FootnoteEmphasized, columnWidth);
        }

        ImGui.Dummy(new Vector2(width, 20f * scale));

        for (var symbol = 0; symbol < SlotsRules.SymbolCount; symbol++)
        {
            var rowOrigin = ImGui.GetCursorScreenPos();
            var rowCenterY = rowOrigin.Y + RowHeight * scale * 0.5f;
            SlotsSymbolArt.Draw(drawList, symbol, new Vector2(rowOrigin.X + 18f * scale, rowCenterY), 13f * scale);
            for (var column = 0; column < 3; column++)
            {
                var pay = SlotsRules.LinePays[symbol, column] * stake;
                var text = pay > 0 ? pay.ToString("N0", Loc.Culture) : "-";
                var ink = pay > 0 ? ui.TitleInk : ui.MutedInk;
                DrawPayCell(drawList, new Vector2(ColumnCenterX(rowOrigin.X, width, column, scale), rowCenterY),
                    text, ink, TextStyles.SubheadlineEmphasized, columnWidth);
            }

            ImGui.Dummy(new Vector2(width, RowHeight * scale));
        }

        ImGui.Dummy(new Vector2(width, 6f * scale));
        DrawNamedNote(ui, drawList, width, scale, SlotsRules.WildSymbol, Loc.T(L.Casino.SlotsWildName),
            Loc.T(L.Casino.SlotsWildNote));
        DrawNamedNote(ui, drawList, width, scale, SlotsRules.ScatterSymbol, Loc.T(L.Casino.SlotsScatterName),
            Loc.T(L.Casino.SlotsScatterNote,
                (SlotsRules.ScatterPays[3] * stake).ToString("N0", Loc.Culture),
                (SlotsRules.ScatterPays[4] * stake).ToString("N0", Loc.Culture),
                (SlotsRules.ScatterPays[5] * stake).ToString("N0", Loc.Culture),
                GameNumber.Label(SlotsRules.FreeSpinAwards[3]),
                GameNumber.Label(SlotsRules.FreeSpinAwards[4]),
                GameNumber.Label(SlotsRules.FreeSpinAwards[5])));

        DrawFootnote(ui, width, scale, Loc.T(L.Casino.SlotsBonusNote,
            GameNumber.Label(SlotsRules.RetriggerSpins), GameNumber.Label(SlotsRules.FreeSpinCap)));
        DrawFootnote(ui, width, scale, Loc.T(L.Casino.SlotsCapRule,
            SlotsRules.PayoutCapMultiple.ToString(Loc.Culture)));
        ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
    }

    private static void DrawNamedNote(AppSkin ui, ImDrawListPtr drawList, float width, float scale, int symbol,
        string name, string note)
    {
        var origin = ImGui.GetCursorScreenPos();
        var textLeft = origin.X + 38f * scale;
        var textWidth = width - 38f * scale;
        var nameSize = Typography.Measure(name, TextStyles.SubheadlineEmphasized);
        var noteBlock = Typography.MeasureWrappedBlock(note, TextStyles.Footnote, textWidth);
        var height = nameSize.Y + noteBlock.Y + 14f * scale;
        SlotsSymbolArt.Draw(drawList, symbol, new Vector2(origin.X + 18f * scale, origin.Y + 16f * scale),
            13f * scale);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y), name, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + nameSize.Y + 4f * scale), note, ui.MutedInk,
            TextStyles.Footnote, textWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 6f * scale));
    }

    private static void DrawFootnote(AppSkin ui, float width, float scale, string text)
    {
        var origin = ImGui.GetCursorScreenPos();
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(origin, text, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, block.Y + 8f * scale));
    }

    private static void DrawPayCell(ImDrawListPtr drawList, Vector2 center, string text, Vector4 ink,
        in TextStyle style, float maxWidth)
    {
        var fittedScale = Typography.FitScale(text, maxWidth, style.Scale, style.Scale * MinPayFontFraction,
            style.Weight);
        var textSize = Typography.Measure(text, fittedScale, style.Weight);
        Typography.Draw(drawList, center - textSize * 0.5f, text, ink, fittedScale, style.Weight);
    }

    private static float ColumnWidth(float width, float scale)
    {
        return (width - TableLeftInset * scale) / 3f - CellPadX * scale;
    }

    private static float ColumnCenterX(float left, float width, int column, float scale)
    {
        var tableLeft = left + TableLeftInset * scale;
        var tableWidth = width - TableLeftInset * scale;
        return tableLeft + tableWidth * (column + 0.5f) / 3f;
    }
}
