using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float LimitFieldHeight = 40f;
    private const float LimitPillHeight = 44f;
    private const long SavedFlashMilliseconds = 3000;

    private string limitsBuffer = string.Empty;
    private bool limitsSeeded;
    private long limitsSavedAtTick;

    private void DrawLimits(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);
        ConsumeLimitsResult();
        var state = casino.State;
        if (state is null)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        SeedLimitsBuffer(state);

        if (state.LossLimit > 0 && state.LossHeadroom <= 0)
        {
            DrawLimitReachedCard(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
        else
        {
            DrawTonightCard(state, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        DrawHouseLimitCard(scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        DrawSelfLimitEditor(state, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void ConsumeLimitsResult()
    {
        var result = casino.TakeLimitsResult();
        if (result is null)
        {
            return;
        }

        if (result.Reason.Length == 0)
        {
            limitsSeeded = false;
            limitsSavedAtTick = Environment.TickCount64;
            return;
        }

        confirm.Alert(null, Loc.T(CasinoReasons.MessageFor(result.Reason)), Loc.T(L.Common.Close));
    }

    private void SeedLimitsBuffer(CasinoStateDto state)
    {
        if (limitsSeeded)
        {
            return;
        }

        var current = state.SelfLimit > 0 ? state.SelfLimit : CasinoLimits.HouseDailyLossLimit;
        limitsBuffer = current.ToString(Loc.Culture);
        limitsSeeded = true;
    }

    private void DrawLimitReachedCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 16f * scale;
        var title = Loc.T(L.Casino.LimitReachedTitle);
        var resetsAtUnix = coins.Wallet?.ResetsAtUnix ?? 0;
        var bodyText = resetsAtUnix > 0
            ? Loc.T(L.Casino.LimitReachedBody, TimeText.FutureMoment(resetsAtUnix))
            : Loc.T(L.Casino.LimitReachedBodySoon);
        var titleSize = Typography.Measure(title, TextStyles.Headline);
        var bodyBlock = Typography.MeasureWrappedBlock(bodyText, TextStyles.Subheadline, width - inset * 2f);
        var iconArea = 44f * scale;
        var height = iconArea + titleSize.Y + bodyBlock.Y + 46f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, min, max, rounding, true);
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.4f)), 1f * scale);

        var iconCenter = new Vector2(min.X + width * 0.5f, min.Y + 16f * scale + iconArea * 0.5f);
        drawList.AddCircleFilled(iconCenter, 20f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 40);
        AppSkin.Icon(drawList, iconCenter, FontAwesomeIcon.HandHoldingHeart.ToIconString(), ui.Accent, 1.1f);

        Typography.DrawCentered(drawList, new Vector2(min.X + width * 0.5f,
            min.Y + 22f * scale + iconArea + titleSize.Y * 0.5f), title, ui.TitleInk, TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + 30f * scale + iconArea + titleSize.Y),
            bodyText, ui.MutedInk, TextStyles.Subheadline, width - inset * 2f);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawTonightCard(CasinoStateDto state, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var heading = Loc.T(L.Casino.NetHeading);
        var tonight = state.NetToday switch
        {
            > 0 => Loc.T(L.Casino.TonightDown, state.NetToday.ToString("N0", Loc.Culture)),
            < 0 => Loc.T(L.Casino.TonightUp, (-state.NetToday).ToString("N0", Loc.Culture)),
            _ => Loc.T(L.Casino.TonightEven),
        };
        var room = Loc.T(L.Casino.RoomLeft, Math.Max(0, state.LossHeadroom).ToString("N0", Loc.Culture));
        var headingSize = Typography.Measure(heading, TextStyles.FootnoteEmphasized);
        var tonightSize = Typography.Measure(tonight, TextStyles.SubheadlineEmphasized);
        var roomSize = Typography.Measure(room, TextStyles.Footnote);
        var height = headingSize.Y + tonightSize.Y + roomSize.Y + 34f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 10f * scale), heading, ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + headingSize.Y + 14f * scale), tonight,
            ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList,
            new Vector2(min.X + inset, min.Y + headingSize.Y + tonightSize.Y + 20f * scale), room, ui.MutedInk,
            TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private void DrawHouseLimitCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var title = Loc.T(L.Casino.HouseLimitTitle);
        var line = Loc.T(L.Casino.HouseLimitLine,
            CasinoLimits.HouseDailyLossLimit.ToString("N0", Loc.Culture));
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var lineBlock = Typography.MeasureWrappedBlock(line, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + lineBlock.Y + 26f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 10f * scale), title, ui.Accent,
            TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + titleSize.Y + 16f * scale), line,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private void DrawSelfLimitEditor(CasinoStateDto state, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        ui.SectionHeading(Loc.T(L.Casino.SelfLimitHeading), 4f);

        var effective = state.LossLimit > 0 ? state.LossLimit : CasinoLimits.HouseDailyLossLimit;
        var currentLine = Loc.T(L.Casino.SelfLimitCurrent, effective.ToString("N0", Loc.Culture));
        var currentSize = Typography.Measure(currentLine, TextStyles.Subheadline);
        var currentOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, currentOrigin, currentLine, ui.TitleInk, TextStyles.Subheadline);
        ImGui.Dummy(new Vector2(width, currentSize.Y + 6f * scale));

        if (state.PendingRaiseLimit > 0)
        {
            var pending = Loc.T(L.Casino.PendingRaise, state.PendingRaiseLimit.ToString("N0", Loc.Culture));
            var pendingOrigin = ImGui.GetCursorScreenPos();
            var pendingSize = Typography.Measure(pending, TextStyles.Footnote);
            Typography.Draw(drawList, pendingOrigin, pending, ui.Accent, TextStyles.Footnote);
            ImGui.Dummy(new Vector2(width, pendingSize.Y + 6f * scale));
        }

        var fieldOrigin = ImGui.GetCursorScreenPos();
        var fieldWidth = width * 0.42f;
        var fieldMin = fieldOrigin;
        var fieldMax = new Vector2(fieldOrigin.X + fieldWidth, fieldOrigin.Y + LimitFieldHeight * scale);
        Squircle.Fill(drawList, fieldMin, fieldMax, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 10f * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldWidth - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputText("##casinoLimit", ref limitsBuffer, 5,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }

        var typed = long.TryParse(limitsBuffer, System.Globalization.NumberStyles.Integer, Loc.Culture,
            out var value)
            ? value
            : 0;
        var currentSelf = state.SelfLimit > 0 ? state.SelfLimit : CasinoLimits.HouseDailyLossLimit;
        var valid = typed >= CasinoLimits.SelfLimitFloor && typed <= CasinoLimits.HouseDailyLossLimit;
        var changed = typed != currentSelf && typed != state.PendingRaiseLimit;
        var saveRect = new Rect(
            new Vector2(fieldMax.X + 10f * scale, fieldOrigin.Y + (LimitFieldHeight - LimitPillHeight) * 0.5f * scale),
            new Vector2(fieldOrigin.X + width, fieldOrigin.Y + (LimitFieldHeight + LimitPillHeight) * 0.5f * scale));
        var canSave = valid && changed && !casino.SavingLimits;
        if (AppSkin.PillButton(saveRect, Loc.T(L.Casino.SelfLimitSave), true, canSave, theme))
        {
            casino.SetLimits(typed);
        }

        ImGui.SetCursorScreenPos(fieldOrigin);
        ImGui.Dummy(new Vector2(width, LimitFieldHeight * scale + 8f * scale));

        var hint = Loc.T(L.Casino.SelfLimitHint,
            CasinoLimits.SelfLimitFloor.ToString("N0", Loc.Culture),
            CasinoLimits.HouseDailyLossLimit.ToString("N0", Loc.Culture));
        var hintOrigin = ImGui.GetCursorScreenPos();
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(hintOrigin, hint, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, hintBlock.Y + 6f * scale));

        if (Environment.TickCount64 - limitsSavedAtTick < SavedFlashMilliseconds)
        {
            var saved = Loc.T(L.Casino.SelfLimitSaved);
            var savedOrigin = ImGui.GetCursorScreenPos();
            var savedSize = Typography.Measure(saved, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, savedOrigin, saved, ui.Accent, TextStyles.FootnoteEmphasized);
            ImGui.Dummy(new Vector2(width, savedSize.Y + 4f * scale));
        }
    }
}
