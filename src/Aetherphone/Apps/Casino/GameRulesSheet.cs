using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed class GameRulesSheet
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float RevealSmoothTime = 0.16f;
    private const float MaxDim = 0.45f;
    private const float PanelRounding = 26f;
    private const float PadX = 18f;
    private const float PanelHeightShare = 0.78f;
    private const float StepGap = 12f;
    private const float FactRowHeight = 34f;
    private const float BulletRadius = 11f;
    private const float PlayPillHeight = 46f;

    private Spring reveal;
    private bool open;
    private int openedFrame;
    private bool playRequested;
    private string gameId = string.Empty;

    public bool IsOpen => open;

    public string GameId => gameId;

    public void Open(string game)
    {
        gameId = game;
        if (open)
        {
            return;
        }

        open = true;
        openedFrame = ImGui.GetFrameCount();
        playRequested = false;
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

    public bool TakePlayRequest()
    {
        if (!playRequested)
        {
            return false;
        }

        playRequested = false;
        return true;
    }

    public void Draw(Rect screen, AppSkin ui)
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
        using (ImRaii.Child("##casinoRules", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            var panel = DrawPanel(screen, ui, drawList, slide, open && opacity > 0.5f);
            if (!open || opacity <= 0.5f)
            {
                return;
            }

            if (ImGui.GetFrameCount() != openedFrame && UiInteract.ClickedOutside(panel.Min, panel.Max))
            {
                Close();
            }
        }
    }

    private Rect DrawPanel(Rect screen, AppSkin ui, ImDrawListPtr drawList, float slide, bool interactive)
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

        var title = Loc.T(CasinoRules.TitleOf(gameId));
        var titleHeight = Typography.Measure(title, TextStyles.Headline).Y;
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, panelTop + 14f * scale + titleHeight * 0.5f),
            title, ui.TitleInk, TextStyles.Headline);

        var pillTop = panelBottom - 12f * scale - PlayPillHeight * scale;
        var contentTop = panelTop + titleHeight + 24f * scale;
        var contentMin = new Vector2(panelMin.X + PadX * scale, contentTop);
        var contentSize = new Vector2(panelMax.X - PadX * scale - contentMin.X,
            MathF.Max(24f * scale, pillTop - 10f * scale - contentTop));
        ImGui.SetCursorScreenPos(contentMin);
        using (ImRaii.Child("##casinoRulesBody", contentSize, false, ImGuiWindowFlags.NoBackground))
        {
            DrawBody(ui, contentSize.X, scale);
        }

        var pillRect = new Rect(new Vector2(panelMin.X + PadX * scale, pillTop),
            new Vector2(panelMax.X - PadX * scale, pillTop + PlayPillHeight * scale));
        if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.RulesPlay), true, interactive, ui.Theme))
        {
            playRequested = true;
            Close();
        }

        return new Rect(panelMin, panelMax);
    }

    private void DrawBody(AppSkin ui, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();

        var pitch = Loc.T(CasinoRules.PitchOf(gameId));
        var pitchOrigin = ImGui.GetCursorScreenPos();
        var pitchBlock = Typography.MeasureWrappedBlock(pitch, TextStyles.Subheadline, width);
        Typography.DrawWrappedLeft(pitchOrigin, pitch, ui.BodyInk, TextStyles.Subheadline, width);
        ImGui.Dummy(new Vector2(width, pitchBlock.Y + Metrics.Space.Md * scale));

        var headingOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, headingOrigin, Loc.T(L.Casino.RulesHowToPlay), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        ImGui.Dummy(new Vector2(width, 24f * scale));

        var steps = CasinoRules.StepsOf(gameId);
        var textLeft = BulletRadius * 2f + 12f * scale;
        for (var index = 0; index < steps.Length; index++)
        {
            var stepText = Loc.T(steps[index]);
            var stepOrigin = ImGui.GetCursorScreenPos();
            var stepWidth = width - textLeft;
            var block = Typography.MeasureWrappedBlock(stepText, TextStyles.Footnote, stepWidth);
            var bulletCenter = new Vector2(stepOrigin.X + BulletRadius * scale,
                stepOrigin.Y + BulletRadius * scale - 1f * scale);
            drawList.AddCircleFilled(bulletCenter, BulletRadius * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.18f)), 24);
            Typography.DrawCentered(drawList, bulletCenter, (index + 1).ToString(Loc.Culture), ui.Accent,
                TextStyles.Caption1);
            Typography.DrawWrappedLeft(new Vector2(stepOrigin.X + textLeft, stepOrigin.Y), stepText, ui.BodyInk,
                TextStyles.Footnote, stepWidth);
            ImGui.Dummy(new Vector2(width, MathF.Max(block.Y, BulletRadius * 2f * scale) + StepGap * scale));
        }

        ImGui.Dummy(new Vector2(width, Metrics.Space.Sm * scale));
        var factsOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, factsOrigin, Loc.T(L.Casino.RulesNumbers), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        ImGui.Dummy(new Vector2(width, 24f * scale));

        for (var index = 0; CasinoRules.FactsOf(gameId, index, out var label, out var value); index++)
        {
            var rowOrigin = ImGui.GetCursorScreenPos();
            var rowCenterY = rowOrigin.Y + FactRowHeight * scale * 0.5f;
            var labelText = Loc.T(label);
            var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
            var labelFitted = Typography.FitText(labelText, width - valueSize.X - 12f * scale,
                TextStyles.Footnote);
            var labelSize = Typography.Measure(labelFitted, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(rowOrigin.X, rowCenterY - labelSize.Y * 0.5f), labelFitted,
                ui.MutedInk, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(rowOrigin.X + width - valueSize.X, rowCenterY - valueSize.Y * 0.5f),
                value, ui.TitleInk, TextStyles.SubheadlineEmphasized);
            drawList.AddLine(new Vector2(rowOrigin.X, rowOrigin.Y + FactRowHeight * scale),
                new Vector2(rowOrigin.X + width, rowOrigin.Y + FactRowHeight * scale),
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), 1f);
            ImGui.Dummy(new Vector2(width, FactRowHeight * scale));
        }

        var fairness = Loc.T(L.Casino.RulesFairness);
        ImGui.Dummy(new Vector2(width, Metrics.Space.Md * scale));
        var fairnessOrigin = ImGui.GetCursorScreenPos();
        var fairnessBlock = Typography.MeasureWrappedBlock(fairness, TextStyles.Caption1, width);
        Typography.DrawWrappedLeft(fairnessOrigin, fairness, Palette.WithAlpha(ui.MutedInk, 0.85f),
            TextStyles.Caption1, width);
        ImGui.Dummy(new Vector2(width, fairnessBlock.Y + Metrics.Space.Lg * scale));
    }
}
