using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class ScratchOddsSheet
{
    private const float RowHeight = 40f;
    private const float PanelHeightShare = 0.72f;

    private readonly SheetSurface sheet = new("casino.scratchOdds");
    private readonly Action<Rect> drawSheetBody;

    private AppSkin skin = null!;
    private int tier;

    public ScratchOddsSheet()
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

    public void Draw(Rect screen, AppSkin ui, int tier)
    {
        skin = ui;
        this.tier = tier;
        sheet.Draw(screen, ui.Theme, Loc.T(L.Casino.ScratchOdds), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##scratchOddsRows", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawRows(skin, UiScale.Current, tier);
        }
    }

    private static void DrawRows(AppSkin ui, float scale, int tier)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();
        var intro = Loc.T(L.Casino.ScratchOddsIntro);
        var introOrigin = ImGui.GetCursorScreenPos();
        var introBlock = Typography.MeasureWrappedBlock(intro, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(introOrigin, intro, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, introBlock.Y + 10f * scale));

        var priceLine = Loc.T(L.Casino.ScratchPrice) + ": "
            + ScratchRules.Prices[tier].ToString("N0", Loc.Culture);
        var priceOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, priceOrigin, priceLine, ui.TitleInk, TextStyles.SubheadlineEmphasized);
        ImGui.Dummy(new Vector2(width, 26f * scale));

        var headerOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, new Vector2(headerOrigin.X + 44f * scale, headerOrigin.Y + 6f * scale),
            Loc.T(L.Casino.ScratchOddsPrize), ui.MutedInk, TextStyles.FootnoteEmphasized);
        var chanceHeader = Loc.T(L.Casino.ScratchOddsChance);
        var chanceHeaderSize = Typography.Measure(chanceHeader, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(headerOrigin.X + width - chanceHeaderSize.X, headerOrigin.Y + 6f * scale),
            chanceHeader, ui.MutedInk, TextStyles.FootnoteEmphasized);
        ImGui.Dummy(new Vector2(width, 24f * scale));

        var table = ScratchRules.PrizeTables[tier];
        for (var prizeIndex = 0; prizeIndex < table.Length; prizeIndex++)
        {
            var rowOrigin = ImGui.GetCursorScreenPos();
            var rowCenterY = rowOrigin.Y + RowHeight * scale * 0.5f;
            ScratchSymbolArt.Draw(drawList, prizeIndex, new Vector2(rowOrigin.X + 18f * scale, rowCenterY),
                13f * scale);
            Typography.Draw(drawList, new Vector2(rowOrigin.X + 44f * scale, rowCenterY - 9f * scale),
                table[prizeIndex].Chips.ToString("N0", Loc.Culture), ui.TitleInk, TextStyles.SubheadlineEmphasized);
            var chance = Loc.T(L.Casino.ScratchOddsChanceValue, ChancePercent(table[prizeIndex].CountPerMillion));
            var chanceSize = Typography.Measure(chance, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(rowOrigin.X + width - chanceSize.X, rowCenterY - 9f * scale),
                chance, ui.BodyInk, TextStyles.Subheadline);
            ImGui.Dummy(new Vector2(width, RowHeight * scale));
        }

        ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
    }

    private static string ChancePercent(long countPerMillion)
    {
        return (countPerMillion / 10_000.0).ToString("0.#", Loc.Culture);
    }
}
