using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class ScratchOddsSheet
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float RevealSmoothTime = 0.16f;
    private const float MaxDim = 0.45f;
    private const float PanelRounding = 26f;
    private const float PadX = 18f;
    private const float RowHeight = 40f;
    private const float PanelHeightShare = 0.72f;

    private Spring reveal;
    private bool open;
    private int openedFrame;

    public bool IsOpen => open;

    public void Open()
    {
        if (open)
        {
            return;
        }

        open = true;
        openedFrame = ImGui.GetFrameCount();
    }

    public void Close()
    {
        open = false;
    }

    public void Gate()
    {
        if (open)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui, int tier)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return;
        }

        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        var slide = Easing.EaseOutQuint(opacity);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##scratchOdds", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            var panel = DrawPanel(screen, ui, drawList, slide, tier);
            var interactive = open && opacity > 0.5f;
            if (!interactive)
            {
                return;
            }

            if (ImGui.GetFrameCount() != openedFrame && UiInteract.ClickedOutside(panel.Min, panel.Max))
            {
                Close();
            }
        }
    }

    private Rect DrawPanel(Rect screen, AppSkin ui, ImDrawListPtr drawList, float slide, int tier)
    {
        var scale = UiScale.Current;
        var panelHeight = screen.Height * PanelHeightShare;
        var panelBottom = screen.Max.Y + panelHeight * (1f - slide);
        var panelTop = panelBottom - panelHeight;
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, panelBottom);
        var rounding = PanelRounding * scale;
        Squircle.Fill(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.Lighten(ui.Palette.BackdropTop, 0.10f) with { W = 1f }));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.08f)), Metrics.Stroke.Hairline);

        var title = Loc.T(L.Casino.ScratchOdds);
        var titleHeight = Typography.Measure(title, TextStyles.Headline).Y;
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, panelTop + 14f * scale + titleHeight * 0.5f),
            title, ui.TitleInk, TextStyles.Headline);

        var contentTop = panelTop + titleHeight + 24f * scale;
        var contentMin = new Vector2(panelMin.X + PadX * scale, contentTop);
        var contentSize = new Vector2(panelMax.X - PadX * scale - contentMin.X,
            panelBottom - 12f * scale - contentTop);
        ImGui.SetCursorScreenPos(contentMin);
        using (ImRaii.Child("##scratchOddsRows", contentSize, false, ImGuiWindowFlags.NoBackground))
        {
            DrawRows(ui, contentSize.X, scale, tier);
        }

        return new Rect(panelMin, panelMax);
    }

    private static void DrawRows(AppSkin ui, float width, float scale, int tier)
    {
        var drawList = ImGui.GetWindowDrawList();
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

        ImGui.Dummy(new Vector2(width, 8f * scale));
        var anyWin = Loc.T(L.Casino.ScratchOddsAnyWin, ChancePercent(ScratchRules.WinCountPerMillion(tier)));
        var anyWinOrigin = ImGui.GetCursorScreenPos();
        var anyWinBlock = Typography.MeasureWrappedBlock(anyWin, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(anyWinOrigin, anyWin, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, anyWinBlock.Y + Metrics.Space.Lg * scale));
    }

    private static string ChancePercent(long countPerMillion)
    {
        return (countPerMillion / 10_000.0).ToString("0.#", Loc.Culture);
    }
}
