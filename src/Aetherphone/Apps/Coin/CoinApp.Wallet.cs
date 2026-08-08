using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private Vector2 checkInAnchor;

    private void DrawWallet(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);
        var wallet = store.Wallet;
        walletRefresh.Draw(body, surface.Pull, surface.Dragging, wallet is null, ui.MutedInk, store.RefreshNow);
        var checkInResult = store.TakeCheckInResult();
        if (checkInResult is { Granted: true, Amount: > 0 })
        {
            floats.Spawn(Loc.T(L.Coin.CheckInReward, checkInResult.Amount.ToString("N0", Loc.Culture)),
                checkInAnchor);
        }

        if (wallet is null)
        {
            TourHolds.Hold(Id);
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var frozen = wallet.FrozenUntilUnix is not null;
        if (frozen)
        {
            NoticeCard(Loc.T(L.Coin.FrozenTitle), Loc.T(L.Coin.FrozenHint));
        }

        var heroOrigin = ImGui.GetCursorScreenPos();
        var heroWidth = ScrollLayout.StableContentWidth();
        CoinHero.Draw(wallet, ui.Palette);
        UiAnchors.Report("coin.balance", new Rect(heroOrigin,
            new Vector2(heroOrigin.X + heroWidth, ImGui.GetCursorScreenPos().Y)));
        ImGui.Dummy(new Vector2(0f, 8f * scale));

        if (wallet.Paused)
        {
            NoticeCard(Loc.T(L.Coin.PausedTitle), Loc.T(L.Coin.PausedHint));
        }
        else
        {
            CoinHero.DrawCapBar(wallet, ui.Palette);
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        DrawCheckIn(wallet, scale);
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        DrawHowToEarn(wallet);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private void DrawCheckIn(CoinWalletDto wallet, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var buttonRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + 44f * scale));
        checkInAnchor = new Vector2(buttonRect.Center.X, buttonRect.Min.Y - 6f * scale);
        UiAnchors.Report("coin.checkin", buttonRect);
        var available = wallet.CheckInAvailable && !wallet.Paused && wallet.FrozenUntilUnix is null
            && !store.CheckingIn;
        var label = wallet.CheckInAvailable ? Loc.T(L.Coin.CheckIn) : Loc.T(L.Coin.CheckedIn);
        if (AppSkin.PillButton(buttonRect, label, wallet.CheckInAvailable, available, theme))
        {
            store.CheckIn();
        }

        ImGui.Dummy(new Vector2(width, 48f * scale));

        var streakText = Loc.T(L.Coin.StreakDays, wallet.StreakDays.ToString("N0", Loc.Culture));
        if (!wallet.CheckInAvailable)
        {
            streakText += " · " + Loc.T(L.Coin.StreakNext);
        }

        var drawList = ImGui.GetWindowDrawList();
        var textSize = Typography.Measure(streakText, TextStyles.Footnote);
        Typography.Draw(drawList,
            new Vector2(origin.X + (width - textSize.X) * 0.5f, ImGui.GetCursorScreenPos().Y),
            streakText, ui.MutedInk, TextStyles.Footnote);
        ImGui.Dummy(new Vector2(width, textSize.Y + 4f * scale));
    }

    private void DrawHowToEarn(CoinWalletDto wallet)
    {
        if (wallet.Rules.Length == 0)
        {
            return;
        }

        var sectionOrigin = ImGui.GetCursorScreenPos();
        var sectionWidth = ScrollLayout.StableContentWidth();
        ui.SectionHeading(Loc.T(L.Coin.EarnHeader), 4f);
        var scale = UiScale.Current;
        for (var index = 0; index < wallet.Rules.Length; index++)
        {
            DrawRuleCard(wallet.Rules[index], scale);
            if (index == 0)
            {
                UiAnchors.Report("coin.earn", new Rect(sectionOrigin,
                    new Vector2(sectionOrigin.X + sectionWidth, ImGui.GetCursorScreenPos().Y)));
            }
        }
    }

    private void DrawRuleCard(in CoinRuleStatusDto rule, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var title = Loc.T(CoinRuleLabels.For(rule.RuleId));
        var progress = rule.EarnedThisPeriod.ToString("N0", Loc.Culture) + " / "
            + rule.PeriodCap.ToString("N0", Loc.Culture);
        var hasHint = CoinRuleLabels.TryHint(rule.RuleId, out var hintEntry);
        var hint = hasHint ? Loc.T(hintEntry) : string.Empty;

        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var hintWidth = width - inset * 2f;
        var hintBlock = hasHint
            ? Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, hintWidth)
            : Vector2.Zero;
        var hasBar = rule.PeriodCap > 0;
        var height = titleSize.Y + (hasHint ? hintBlock.Y + 6f * scale : 0f) + 22f * scale
            + (hasBar ? 9f * scale : 0f);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 14f * scale;
        var complete = rule.PeriodCap > 0 && rule.EarnedThisPeriod >= rule.PeriodCap;
        var accent = ui.Palette.Accent;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardFill));
        if (complete)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.08f)));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.45f)),
                1f * scale);
        }

        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var progressSize = Typography.Measure(progress, TextStyles.SubheadlineEmphasized);
        var checkSize = complete ? 11f * scale : 0f;
        var checkPad = complete ? 6f * scale : 0f;
        var titleMaxWidth = width - inset * 2f - progressSize.X - checkSize - checkPad - 10f * scale;
        var fittedTitle = Typography.FitText(title, MathF.Max(40f * scale, titleMaxWidth),
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 11f * scale), fittedTitle,
            ui.Palette.TitleInk, TextStyles.SubheadlineEmphasized);
        var progressLeft = max.X - inset - progressSize.X;
        Typography.Draw(drawList,
            new Vector2(progressLeft, min.Y + 11f * scale), progress,
            complete ? accent : ui.MutedInk, TextStyles.SubheadlineEmphasized);
        if (complete)
        {
            ProgressRing.CenterIcon(drawList,
                new Vector2(progressLeft - checkPad - checkSize * 0.5f,
                    min.Y + 11f * scale + progressSize.Y * 0.5f),
                FontAwesomeIcon.CheckCircle, accent, checkSize);
        }

        if (hasHint)
        {
            Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + titleSize.Y + 15f * scale), hint,
                ui.MutedInk, TextStyles.Footnote, hintWidth);
        }

        if (hasBar)
        {
            var barHeight = 3f * scale;
            var barMin = new Vector2(min.X + inset, max.Y - 10f * scale);
            var barMax = new Vector2(max.X - inset, barMin.Y + barHeight);
            drawList.AddRectFilled(barMin, barMax,
                ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.18f)), barHeight * 0.5f);
            var fraction = Math.Clamp(rule.EarnedThisPeriod / (float)rule.PeriodCap, 0f, 1f);
            if (fraction > 0f)
            {
                var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * fraction, barMax.Y);
                drawList.AddRectFilled(barMin, fillMax,
                    ImGui.GetColorU32(complete ? accent : Palette.WithAlpha(accent, 0.85f)), barHeight * 0.5f);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private void NoticeCard(string title, string hint)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + hintBlock.Y + 26f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 10f * scale), title,
            ui.Palette.Accent, TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + titleSize.Y + 16f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }
}
