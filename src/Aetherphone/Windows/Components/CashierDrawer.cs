using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class CashierDrawer
{
    public readonly record struct GameOption(string GameId, LocString Name);

    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float RevealSmoothTime = 0.16f;
    private const float MaxDim = 0.45f;
    private const float PanelRounding = 26f;
    private const float PadX = 18f;
    private const float SectionGap = 10f;
    private const float SummaryRowHeight = 22f;
    private const float CardPad = 12f;
    private const float ChipHeight = 30f;
    private const float FieldHeight = 40f;
    private const float PillHeight = 44f;
    private const long FallbackMinBuyIn = 10;
    private const long FallbackMaxBuyIn = 2000;

    private readonly CasinoStore store;
    private readonly CoinStore coins;
    private readonly ConfirmService confirm;

    private static readonly LocString[] PresetLabels =
    {
        L.Casino.AmountMin,
        L.Casino.AmountHalf,
        L.Casino.AmountMax,
    };

    private Spring reveal;
    private bool open;
    private int openedFrame;
    private string amountBuffer = string.Empty;
    private int selectedGame;
    private string inlineReason = string.Empty;

    public CashierDrawer(CasinoStore store, CoinStore coins, ConfirmService confirm)
    {
        this.store = store;
        this.coins = coins;
        this.confirm = confirm;
    }

    public bool IsOpen => open;

    public void Open()
    {
        if (open)
        {
            return;
        }

        open = true;
        openedFrame = ImGui.GetFrameCount();
        amountBuffer = string.Empty;
        inlineReason = string.Empty;
        store.RefreshNow();
        coins.EnsureFresh();
    }

    public void Close()
    {
        open = false;
    }

    public void Gate()
    {
        if (open && confirm.Active is null)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui, ReadOnlySpan<GameOption> games, Action openLimits)
    {
        ConsumeResults(openLimits);
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
        using (ImRaii.Child("##cashierDrawer", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            var interactive = open && confirm.Active is null && opacity > 0.5f;
            var panel = DrawPanel(screen, ui, games, drawList, slide, interactive);
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

    private void ConsumeResults(Action openLimits)
    {
        var sitting = store.TakeSittingResult();
        if (sitting is not null)
        {
            HandleOutcome(sitting.Reason, openLimits, true);
        }

        var closed = store.TakeCloseResult();
        if (closed is not null)
        {
            HandleOutcome(closed.Reason, openLimits, false);
        }
    }

    private void HandleOutcome(string reason, Action openLimits, bool clearAmount)
    {
        if (reason.Length == 0)
        {
            inlineReason = string.Empty;
            if (clearAmount)
            {
                amountBuffer = string.Empty;
            }

            return;
        }

        if (string.Equals(reason, CasinoReasons.LossLimit, StringComparison.Ordinal))
        {
            Close();
            openLimits();
            return;
        }

        if (open)
        {
            inlineReason = reason;
            return;
        }

        confirm.Alert(null, Loc.T(CasinoReasons.MessageFor(reason)), Loc.T(L.Common.Close));
    }

    private Rect DrawPanel(Rect screen, AppSkin ui, ReadOnlySpan<GameOption> games, ImDrawListPtr drawList,
        float slide, bool interactive)
    {
        var scale = UiScale.Current;
        var state = store.State;
        var wallet = coins.Wallet;
        var sittingOpen = state is not null && state.SittingId.Length > 0;
        var frozen = wallet?.FrozenUntilUnix is not null;
        var paused = state?.StakesPaused == true;
        var draining = state?.Draining == true;
        var stakeBlocked = frozen || paused || draining;
        var innerWidth = screen.Width - PadX * 2f * scale;

        var noticeTitle = string.Empty;
        var noticeHint = string.Empty;
        if (frozen)
        {
            noticeTitle = Loc.T(L.Coin.FrozenTitle);
            noticeHint = Loc.T(L.Coin.FrozenHint);
        }
        else if (paused)
        {
            noticeTitle = Loc.T(L.Casino.PausedTitle);
            noticeHint = Loc.T(L.Casino.PausedHint);
        }
        else if (draining)
        {
            noticeTitle = Loc.T(L.Casino.DrainingTitle);
            noticeHint = Loc.T(L.Casino.DrainingHint);
        }

        var reasonText = inlineReason.Length > 0 ? Loc.T(CasinoReasons.MessageFor(inlineReason)) : string.Empty;

        var titleHeight = Typography.Measure(Loc.T(L.Casino.Cashier), TextStyles.Headline).Y;
        var summaryRows = sittingOpen ? 3 : 2;
        var summaryHeight = summaryRows * SummaryRowHeight * scale + CardPad * 2f * scale;
        var noticeHeight = 0f;
        if (noticeTitle.Length > 0)
        {
            var hintBlock = Typography.MeasureWrappedBlock(noticeHint, TextStyles.Footnote, innerWidth - CardPad * 2f * scale);
            noticeHeight = Typography.Measure(noticeTitle, TextStyles.FootnoteEmphasized).Y + hintBlock.Y
                + CardPad * 2f * scale + 6f * scale + SectionGap * scale;
        }

        var reasonHeight = 0f;
        if (reasonText.Length > 0)
        {
            var reasonBlock = Typography.MeasureWrappedBlock(reasonText, TextStyles.Footnote, innerWidth - CardPad * 2f * scale);
            reasonHeight = reasonBlock.Y + CardPad * 2f * scale + SectionGap * scale;
        }

        var pickHeight = !sittingOpen && !stakeBlocked && games.Length > 0
            ? (18f + ChipHeight + SectionGap) * scale
            : 0f;
        var stakeHeight = stakeBlocked ? 0f : (18f + 6f + FieldHeight + SectionGap + PillHeight) * scale;
        var cashOutHeight = sittingOpen ? (SectionGap + PillHeight + 20f) * scale : 0f;
        var panelHeight = 14f * scale + titleHeight + SectionGap * scale + summaryHeight + SectionGap * scale
            + noticeHeight + reasonHeight + pickHeight + stakeHeight + cashOutHeight + 18f * scale;

        var panelBottom = screen.Max.Y + panelHeight * (1f - slide);
        var panelTop = panelBottom - panelHeight;
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, panelBottom);
        var rounding = PanelRounding * scale;
        Squircle.Fill(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.Lighten(ui.Palette.BackdropTop, 0.10f) with { W = 1f }));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.08f)), Metrics.Stroke.Hairline);

        var left = panelMin.X + PadX * scale;
        var y = panelTop + 14f * scale;
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, y + titleHeight * 0.5f),
            Loc.T(L.Casino.Cashier), ui.TitleInk, TextStyles.Headline);
        y += titleHeight + SectionGap * scale;

        y = DrawSummary(drawList, ui, state, wallet, sittingOpen, games, left, y, innerWidth, scale);
        y += SectionGap * scale;

        if (noticeTitle.Length > 0)
        {
            y = DrawNotice(drawList, ui, noticeTitle, noticeHint, left, y, innerWidth, scale);
            y += SectionGap * scale;
        }

        if (reasonText.Length > 0)
        {
            y = DrawReason(drawList, ui, reasonText, left, y, innerWidth, scale);
            y += SectionGap * scale;
        }

        if (!sittingOpen && !stakeBlocked && games.Length > 0)
        {
            y = DrawGamePicker(drawList, ui, games, left, y, scale, interactive);
            y += SectionGap * scale;
        }

        if (!stakeBlocked)
        {
            y = DrawStakeEntry(drawList, ui, state, wallet, sittingOpen, games, left, y, innerWidth, scale,
                interactive);
        }

        if (sittingOpen)
        {
            y += SectionGap * scale;
            DrawCashOut(drawList, ui, state!, left, y, innerWidth, scale, interactive);
        }

        return new Rect(panelMin, panelMax);
    }

    private static float DrawSummary(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto? state,
        CoinWalletDto? wallet, bool sittingOpen, ReadOnlySpan<GameOption> games,
        float left, float y, float innerWidth, float scale)
    {
        var rows = sittingOpen ? 3 : 2;
        var height = rows * SummaryRowHeight * scale + CardPad * 2f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + innerWidth, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, 16f * scale, scale);

        var rowY = min.Y + CardPad * scale;
        var balanceText = (wallet?.Balance ?? 0).ToString("N0", Loc.Culture);
        DrawSummaryRow(drawList, ui, Loc.T(L.Casino.WalletRow), balanceText, left, rowY, innerWidth, scale,
            ui.TitleInk);
        rowY += SummaryRowHeight * scale;

        if (sittingOpen)
        {
            var stackText = (state?.Stack ?? 0).ToString("N0", Loc.Culture);
            DrawSummaryRow(drawList, ui, GameLabel(state?.GameKind ?? string.Empty, games), stackText, left, rowY,
                innerWidth, scale, ui.Accent);
            rowY += SummaryRowHeight * scale;
        }

        var net = state?.NetToday ?? 0;
        var tonight = net switch
        {
            > 0 => Loc.T(L.Casino.TonightDown, net.ToString("N0", Loc.Culture)),
            < 0 => Loc.T(L.Casino.TonightUp, (-net).ToString("N0", Loc.Culture)),
            _ => Loc.T(L.Casino.TonightEven),
        };
        Typography.Draw(drawList, new Vector2(left + CardPad * scale, rowY + 2f * scale), tonight, ui.MutedInk,
            TextStyles.Footnote);
        return max.Y;
    }

    private static void DrawSummaryRow(ImDrawListPtr drawList, AppSkin ui, string label, string value, float left,
        float rowY, float innerWidth, float scale, Vector4 valueInk)
    {
        Typography.Draw(drawList, new Vector2(left + CardPad * scale, rowY), label, ui.BodyInk,
            TextStyles.Subheadline);
        var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left + innerWidth - CardPad * scale - valueSize.X, rowY), value,
            valueInk, TextStyles.SubheadlineEmphasized);
    }

    private static string GameLabel(string wireKind, ReadOnlySpan<GameOption> games)
    {
        for (var index = 0; index < games.Length; index++)
        {
            if (string.Equals(CasinoWire.Kind(games[index].GameId), wireKind, StringComparison.Ordinal))
            {
                return Loc.T(L.Casino.ChipsRow) + " · " + Loc.T(games[index].Name);
            }
        }

        return Loc.T(L.Casino.ChipsRow);
    }

    private static float DrawNotice(ImDrawListPtr drawList, AppSkin ui, string title, string hint, float left,
        float y, float innerWidth, float scale)
    {
        var pad = CardPad * scale;
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, innerWidth - pad * 2f);
        var height = titleSize.Y + hintBlock.Y + pad * 2f + 6f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + innerWidth, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, 16f * scale, scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), title, ui.Accent,
            TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, innerWidth - pad * 2f);
        return max.Y;
    }

    private static float DrawReason(ImDrawListPtr drawList, AppSkin ui, string message, float left, float y,
        float innerWidth, float scale)
    {
        var pad = CardPad * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, innerWidth - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + innerWidth, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk,
            TextStyles.Footnote, innerWidth - pad * 2f);
        return max.Y;
    }

    private float DrawGamePicker(ImDrawListPtr drawList, AppSkin ui, ReadOnlySpan<GameOption> games, float left,
        float y, float scale, bool interactive)
    {
        Typography.Draw(drawList, new Vector2(left, y), Loc.T(L.Casino.PickGame), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        y += 18f * scale;
        if (selectedGame >= games.Length)
        {
            selectedGame = 0;
        }

        var chipX = left;
        for (var index = 0; index < games.Length; index++)
        {
            var label = Loc.T(games[index].Name);
            var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
            var chipMin = new Vector2(chipX, y);
            var chipMax = new Vector2(chipX + labelSize.X + 24f * scale, y + ChipHeight * scale);
            var active = index == selectedGame;
            if (RawChip(drawList, new Rect(chipMin, chipMax), label, active, ui, scale, interactive))
            {
                selectedGame = index;
            }

            chipX = chipMax.X + 8f * scale;
        }

        return y + ChipHeight * scale;
    }

    private float DrawStakeEntry(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto? state,
        CoinWalletDto? wallet, bool sittingOpen, ReadOnlySpan<GameOption> games,
        float left, float y, float innerWidth, float scale, bool interactive)
    {
        var minBuyIn = state is { MinBuyIn: > 0 } ? state.MinBuyIn : FallbackMinBuyIn;
        var maxBuyIn = state is { MaxBuyIn: > 0 } ? state.MaxBuyIn : FallbackMaxBuyIn;
        var room = sittingOpen ? Math.Max(0, maxBuyIn - (state?.ChipsIn ?? 0)) : maxBuyIn;
        var balance = wallet?.Balance ?? 0;
        var effectiveMax = Math.Min(room, balance);

        var heading = sittingOpen ? Loc.T(L.Casino.TopUp) : Loc.T(L.Casino.BuyIn);
        Typography.Draw(drawList, new Vector2(left, y), heading, ui.MutedInk, TextStyles.FootnoteEmphasized);
        var bounds = Loc.T(L.Casino.BuyInBounds, minBuyIn.ToString("N0", Loc.Culture),
            effectiveMax.ToString("N0", Loc.Culture));
        var boundsSize = Typography.Measure(bounds, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left + innerWidth - boundsSize.X, y), bounds, ui.MutedInk,
            TextStyles.Footnote);
        y += (18f + 6f) * scale;

        var fieldWidth = innerWidth * 0.44f;
        var fieldMin = new Vector2(left, y);
        var fieldMax = new Vector2(left + fieldWidth, y + FieldHeight * scale);
        Squircle.Fill(drawList, fieldMin, fieldMax, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 10f * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldWidth - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputText("##cashierAmount", ref amountBuffer, 7,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }

        var presetWidth = (innerWidth - fieldWidth - 3f * 8f * scale) / 3f;
        var presetLeft = fieldMax.X + 8f * scale;
        Span<long> presetValues = stackalloc long[3];
        presetValues[0] = minBuyIn;
        presetValues[1] = Math.Max(minBuyIn, effectiveMax / 2);
        presetValues[2] = effectiveMax;
        var presetLabels = PresetLabels;
        for (var index = 0; index < 3; index++)
        {
            var chipMin = new Vector2(presetLeft, y + (FieldHeight - ChipHeight) * 0.5f * scale);
            var chipMax = new Vector2(presetLeft + presetWidth, chipMin.Y + ChipHeight * scale);
            if (RawChip(drawList, new Rect(chipMin, chipMax), Loc.T(presetLabels[index]), false, ui, scale,
                    interactive && effectiveMax >= minBuyIn))
            {
                amountBuffer = presetValues[index].ToString(Loc.Culture);
            }

            presetLeft = chipMax.X + 8f * scale;
        }

        y += FieldHeight * scale + SectionGap * scale;

        var amount = ParseAmount();
        var amountValid = amount >= minBuyIn && amount <= effectiveMax && effectiveMax >= minBuyIn;
        var busy = store.MovingMoney;
        var label = amount > 0 && amountValid
            ? Loc.T(sittingOpen ? L.Casino.TopUpFor : L.Casino.BuyInFor, amount.ToString("N0", Loc.Culture))
            : Loc.T(sittingOpen ? L.Casino.TopUp : L.Casino.BuyIn);
        var confirmRect = new Rect(new Vector2(left, y), new Vector2(left + innerWidth, y + PillHeight * scale));
        var canConfirm = interactive && amountValid && !busy && (sittingOpen || games.Length > 0);
        if (RawPill(drawList, confirmRect, label, true, canConfirm, ui, scale))
        {
            AskStake(sittingOpen, games, amount);
        }

        return y + PillHeight * scale;
    }

    private void AskStake(bool sittingOpen, ReadOnlySpan<GameOption> games, long amount)
    {
        var amountText = amount.ToString("N0", Loc.Culture);
        if (sittingOpen)
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Casino.TopUpConfirmTitle, amountText),
                Message = Loc.T(L.Casino.TopUpConfirmBody, amountText),
                ConfirmLabel = Loc.T(L.Casino.TopUp),
                CancelLabel = Loc.T(L.Common.Cancel),
                Danger = false,
                Confirm = () => store.TopUp(amount),
            });
            return;
        }

        if (selectedGame >= games.Length)
        {
            return;
        }

        var option = games[selectedGame];
        var wireKind = CasinoWire.Kind(option.GameId);
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Casino.BuyInConfirmTitle, amountText),
            Message = Loc.T(L.Casino.BuyInConfirmBody, amountText, Loc.T(option.Name)),
            ConfirmLabel = Loc.T(L.Casino.BuyIn),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = () => store.OpenSitting(wireKind, amount),
        });
    }

    private void DrawCashOut(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state,
        float left, float y, float innerWidth, float scale, bool interactive)
    {
        var stackText = state.Stack.ToString("N0", Loc.Culture);
        var rect = new Rect(new Vector2(left, y), new Vector2(left + innerWidth, y + PillHeight * scale));
        var canCashOut = interactive && !store.MovingMoney;
        if (RawPill(drawList, rect, Loc.T(L.Casino.CashOutFor, stackText), false, canCashOut, ui, scale))
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Casino.CashOutConfirmTitle, stackText),
                Message = Loc.T(L.Casino.CashOutConfirmBody),
                ConfirmLabel = Loc.T(L.Casino.CashOut),
                CancelLabel = Loc.T(L.Common.Cancel),
                Danger = false,
                Confirm = store.CloseSitting,
            });
        }

        Typography.Draw(drawList, new Vector2(left, y + PillHeight * scale + 4f * scale),
            Loc.T(L.Casino.CashOutHint), ui.MutedInk, TextStyles.Caption1);
    }

    private long ParseAmount()
    {
        return long.TryParse(amountBuffer, System.Globalization.NumberStyles.Integer, Loc.Culture, out var value)
            ? value
            : 0;
    }

    private static bool RawChip(ImDrawListPtr drawList, Rect rect, string label, bool active, AppSkin ui,
        float scale, bool interactive)
    {
        var rounding = rect.Height * 0.5f;
        var hovered = interactive && UiInteract.HoverWindowOnly(rect.Min, rect.Max);
        var fill = active ? Palette.WithAlpha(ui.Accent, 0.9f) : ui.FieldSurface;
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill));
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = active ? ui.Palette.HeaderInk : ui.BodyInk;
        var fitted = Typography.FitText(label, rect.Width - 10f * scale, TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, rect.Center, fitted, ink, TextStyles.FootnoteEmphasized);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool RawPill(ImDrawListPtr drawList, Rect rect, string label, bool filled, bool enabled,
        AppSkin ui, float scale)
    {
        var rounding = rect.Height * 0.5f;
        var hovered = enabled && UiInteract.HoverWindowOnly(rect.Min, rect.Max);
        var fill = filled
            ? Palette.WithAlpha(ui.Accent, enabled ? 1f : 0.4f)
            : Palette.WithAlpha(ui.FieldSurface, enabled ? 1f : 0.5f);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill));
        if (!filled)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f * scale);
        }

        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = filled ? ui.Palette.HeaderInk : ui.TitleInk;
        var fitted = Typography.FitText(label, rect.Width - rect.Height, 0.9f, FontWeight.SemiBold);
        var textSize = Typography.Measure(fitted, 0.9f, FontWeight.SemiBold);
        Typography.Draw(drawList, rect.Center - textSize * 0.5f, fitted,
            enabled ? ink : Palette.WithAlpha(ink, 0.6f), 0.9f, FontWeight.SemiBold);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
