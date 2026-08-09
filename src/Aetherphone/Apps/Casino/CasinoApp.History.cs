using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float HistoryRowHeight = 56f;
    private const float DetailRowHeight = 44f;
    private const float ActionPillHeight = 44f;
    private const int FairnessRecentRoundLimit = 8;

    private bool historyLoadFailed;

    private void DrawHistory(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);
        history.EnsureFresh();
        if (history.TakeLoadFailure())
        {
            historyLoadFailed = true;
        }

        var rounds = history.Rounds;
        if (rounds.Length == 0)
        {
            if (history.Loading || (!historyLoadFailed && !history.Loaded))
            {
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk,
                    LoadingPulse.SafeLabel());
                return;
            }

            if (historyLoadFailed)
            {
                if (EmptyState.Draw(body, ui, FontAwesomeIcon.CloudShowersHeavy,
                        Loc.T(L.Casino.HistoryEmptyTitle), Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable)),
                        Loc.T(L.Common.Retry)))
                {
                    historyLoadFailed = false;
                    history.Invalidate();
                }

                return;
            }

            EmptyState.Draw(body, ui, FontAwesomeIcon.Receipt, Loc.T(L.Casino.HistoryEmptyTitle),
                Loc.T(L.Casino.HistoryEmptyHint));
            return;
        }

        var index = 0;
        while (index < rounds.Length)
        {
            var dayStart = index;
            var probe = dayStart;
            while (probe < rounds.Length
                && TimeText.SameLocalDay(rounds[dayStart].CreatedAtUnix, rounds[probe].CreatedAtUnix))
            {
                probe++;
            }

            ui.SectionHeading(TimeText.DayLabel(rounds[dayStart].CreatedAtUnix), 8f);
            var card = GroupCard.Begin(theme, probe - dayStart, HistoryRowHeight);
            for (var entryIndex = dayStart; entryIndex < probe; entryIndex++)
            {
                DrawHistoryRow(card.NextRow(), rounds[entryIndex], scale);
            }

            card.End();
            index = probe;
        }

        if (history.HasMore && !history.Loading && InfiniteScroll.ReachedBottom())
        {
            history.LoadMore();
        }

        if (history.Loading)
        {
            InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void DrawHistoryRow(Rect row, CasinoRoundHistoryDto round, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var gameId = ClientGameId(round.GameKind);
        var glyphCenter = new Vector2(row.Min.X + 16f * scale, row.Center.Y);
        var glyphRadius = 15f * scale;
        drawList.AddCircleFilled(glyphCenter, glyphRadius, ImGui.GetColorU32(ui.FieldSurface), 32);
        CasinoGlyphs.Draw(drawList, gameId, glyphCenter, 8f * scale, ImGui.GetColorU32(ui.TitleInk),
            ImGui.GetColorU32(ui.FieldSurface));

        var outcome = OutcomeText(round, out var outcomeInk, out var outcomeStyle);
        var outcomeSize = Typography.Measure(outcome, outcomeStyle);
        var stakeLine = Loc.T(L.Casino.HistoryStakeLine, round.Stake.ToString("N0", Loc.Culture));
        var stakeSize = Typography.Measure(stakeLine, TextStyles.Caption1);
        var rightEdge = row.Max.X - 4f * scale;
        Typography.Draw(drawList, new Vector2(rightEdge - outcomeSize.X, row.Center.Y - outcomeSize.Y + 2f * scale),
            outcome, outcomeInk, outcomeStyle);
        Typography.Draw(drawList, new Vector2(rightEdge - stakeSize.X, row.Center.Y + 4f * scale), stakeLine,
            ui.MutedInk, TextStyles.Caption1);

        var textLeft = row.Min.X + 40f * scale;
        var textWidth = rightEdge - MathF.Max(outcomeSize.X, stakeSize.X) - 10f * scale - textLeft;
        var title = Typography.FitText(Loc.T(GameName(gameId)), textWidth, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - 18f * scale), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var subtitle = Typography.FitText(TimeText.Clock(round.CreatedAtUnix), textWidth, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y + 2f * scale), subtitle, ui.MutedInk,
            TextStyles.Caption1);

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            router.Push(new CasinoRoute(CasinoScreen.RoundDetail, gameId, round.RoundId));
        }
    }

    private string OutcomeText(CasinoRoundHistoryDto round, out Vector4 ink, out TextStyle style)
    {
        if (round.State == CasinoRoundStates.Open)
        {
            ink = ui.MutedInk;
            style = TextStyles.Footnote;
            return Loc.T(L.Casino.StateOpen);
        }

        if (round.State == CasinoRoundStates.Voided)
        {
            ink = ui.MutedInk;
            style = TextStyles.Footnote;
            return Loc.T(L.Casino.StateVoided);
        }

        var net = round.Payout - round.Stake;
        if (net > 0)
        {
            ink = ui.Accent;
            style = TextStyles.SubheadlineEmphasized;
            return "+" + net.ToString("N0", Loc.Culture);
        }

        if (net < 0)
        {
            ink = ui.MutedInk;
            style = TextStyles.Subheadline;
            return net.ToString("N0", Loc.Culture);
        }

        ink = ui.BodyInk;
        style = TextStyles.Subheadline;
        return "0";
    }

    private void DrawFairness(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);
        history.EnsureFresh();
        var width = ScrollLayout.StableContentWidth();

        DrawWrappedParagraph(Loc.T(L.Casino.FairnessIntro), width, scale);
        DrawFairnessStep(Loc.T(L.Casino.FairnessLockTitle), Loc.T(L.Casino.FairnessLockBody), scale);
        DrawFairnessStep(Loc.T(L.Casino.FairnessRevealTitle), Loc.T(L.Casino.FairnessRevealBody), scale);
        DrawFairnessStep(Loc.T(L.Casino.FairnessReplayTitle), Loc.T(L.Casino.FairnessReplayBody), scale);
        DrawWrappedParagraph(Loc.T(L.Casino.FairnessChainNote), width, scale);

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        ui.SectionHeading(Loc.T(L.Casino.FairnessRecentHeading), 4f);
        var rounds = history.Rounds;
        var checkableCount = 0;
        for (var roundIndex = 0; roundIndex < rounds.Length && checkableCount < FairnessRecentRoundLimit;
             roundIndex++)
        {
            if (rounds[roundIndex].State != CasinoRoundStates.Open)
            {
                checkableCount++;
            }
        }

        if (checkableCount == 0)
        {
            DrawWrappedParagraph(Loc.T(L.Casino.FairnessNoRounds), width, scale);
        }
        else
        {
            var card = GroupCard.Begin(theme, checkableCount, HistoryRowHeight);
            var drawn = 0;
            for (var roundIndex = 0; roundIndex < rounds.Length && drawn < checkableCount; roundIndex++)
            {
                if (rounds[roundIndex].State == CasinoRoundStates.Open)
                {
                    continue;
                }

                DrawHistoryRow(card.NextRow(), rounds[roundIndex], scale);
                drawn++;
            }

            card.End();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void DrawWrappedParagraph(string text, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(origin, text, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, block.Y + Metrics.Space.Sm * scale));
    }

    private void DrawFairnessStep(string title, string bodyText, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var bodyBlock = Typography.MeasureWrappedBlock(bodyText, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + bodyBlock.Y + 26f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 10f * scale), title, ui.Accent,
            TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + titleSize.Y + 16f * scale), bodyText,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private void DrawRoundDetail(Rect body, string roundId)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);
        if (history.TakeVerifyFailure())
        {
            confirm.Alert(null, Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable)), Loc.T(L.Common.Close));
        }

        var round = FindHistoryRound(roundId);
        var hasVerified = history.TryGetVerified(roundId, out var verifiedRound);
        if (hasVerified)
        {
            DrawVerdictCard(verifiedRound.Verdict, scale);
        }

        if (round is not null)
        {
            DrawRoundFacts(round, scale);
        }

        DrawRoundReference(roundId, round, hasVerified ? verifiedRound : null, scale);

        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var verifyRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + ActionPillHeight * scale));
        var canVerify = !history.Verifying
            && (!hasVerified || verifiedRound.Verdict == CasinoRoundVerdict.Unrevealed);
        if (AppSkin.PillButton(verifyRect, Loc.T(L.Casino.VerifyAction), true, canVerify, theme))
        {
            history.RequestVerify(roundId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ActionPillHeight * scale + 10f * scale));

        if (hasVerified)
        {
            var copyOrigin = ImGui.GetCursorScreenPos();
            var copyRect = new Rect(copyOrigin,
                new Vector2(copyOrigin.X + width, copyOrigin.Y + ActionPillHeight * scale));
            if (AppSkin.PillButton(copyRect, Loc.T(L.Casino.CopyDetails), false, true, theme))
            {
                ImGui.SetClipboardText(BuildRoundDetailsBlob(verifiedRound));
                CopyToast.Show();
            }

            ImGui.SetCursorScreenPos(copyOrigin);
            ImGui.Dummy(new Vector2(width, ActionPillHeight * scale + 10f * scale));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private CasinoRoundHistoryDto? FindHistoryRound(string roundId)
    {
        var rounds = history.Rounds;
        for (var roundIndex = 0; roundIndex < rounds.Length; roundIndex++)
        {
            if (string.Equals(rounds[roundIndex].RoundId, roundId, StringComparison.Ordinal))
            {
                return rounds[roundIndex];
            }
        }

        return null;
    }

    private void DrawVerdictCard(CasinoRoundVerdict verdict, float scale)
    {
        var title = verdict switch
        {
            CasinoRoundVerdict.Match => Loc.T(L.Casino.VerdictMatchTitle),
            CasinoRoundVerdict.Mismatch => Loc.T(L.Casino.VerdictMismatchTitle),
            _ => Loc.T(L.Casino.VerdictUnrevealedTitle),
        };
        var hint = verdict switch
        {
            CasinoRoundVerdict.Match => Loc.T(L.Casino.VerdictMatchHint),
            CasinoRoundVerdict.Mismatch => Loc.T(L.Casino.VerdictMismatchHint),
            _ => Loc.T(L.Casino.VerdictUnrevealedHint),
        };
        var accent = verdict switch
        {
            CasinoRoundVerdict.Match => ui.Accent,
            CasinoRoundVerdict.Mismatch => theme.Danger,
            _ => ui.MutedInk,
        };

        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + hintBlock.Y + 28f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.40f)),
            1f * scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + 10f * scale), title, accent,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + titleSize.Y + 16f * scale), hint,
            ui.BodyInk, TextStyles.Footnote, width - inset * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 10f * scale));
    }

    private void DrawRoundFacts(CasinoRoundHistoryDto round, float scale)
    {
        var rowCount = round.SettledAtUnix is not null ? 5 : 4;
        var card = GroupCard.Begin(theme, rowCount, DetailRowHeight);
        DrawFactRow(card.NextRow(), Loc.T(L.Casino.RoundGame), Loc.T(GameName(ClientGameId(round.GameKind))),
            ui.TitleInk, scale);
        DrawFactRow(card.NextRow(), Loc.T(L.Casino.RoundState), StateText(round.State), ui.TitleInk, scale);
        DrawFactRow(card.NextRow(), Loc.T(L.Casino.RoundStake), round.Stake.ToString("N0", Loc.Culture),
            ui.TitleInk, scale);
        var payoutInk = round.State == CasinoRoundStates.Settled && round.Payout > round.Stake
            ? ui.Accent
            : ui.TitleInk;
        DrawFactRow(card.NextRow(), Loc.T(L.Casino.RoundPayout), round.Payout.ToString("N0", Loc.Culture),
            payoutInk, scale);
        if (round.SettledAtUnix is long settledAtUnix)
        {
            DrawFactRow(card.NextRow(), Loc.T(L.Casino.RoundSettledAt),
                TimeText.DayLabel(settledAtUnix) + " " + TimeText.Clock(settledAtUnix), ui.TitleInk, scale);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawFactRow(Rect row, string label, string value, Vector4 valueInk, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - 9f * scale), label, ui.BodyInk,
            TextStyles.Subheadline);
        var labelWidth = Typography.Measure(label, TextStyles.Subheadline).X;
        var fitted = Typography.FitText(value, row.Width - labelWidth - 12f * scale,
            TextStyles.SubheadlineEmphasized);
        var valueSize = Typography.Measure(fitted, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, row.Center.Y - 9f * scale), fitted,
            valueInk, TextStyles.SubheadlineEmphasized);
    }

    private string StateText(int state) => state switch
    {
        CasinoRoundStates.Open => Loc.T(L.Casino.StateOpen),
        CasinoRoundStates.Voided => Loc.T(L.Casino.StateVoided),
        _ => Loc.T(L.Casino.StateSettled),
    };

    private void DrawRoundReference(string roundId, CasinoRoundHistoryDto? round,
        VerifiedCasinoRound? verifiedRound, float scale)
    {
        var commit = verifiedRound?.Round.SeedCommitHash ?? round?.SeedCommitHash ?? string.Empty;
        var playedAtUnix = round?.CreatedAtUnix ?? 0;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var innerWidth = width - inset * 2f;

        var idLabel = Loc.T(L.Casino.RoundIdLabel);
        var idLabelSize = Typography.Measure(idLabel, TextStyles.Caption1);
        var idBlock = Typography.MeasureWrappedBlock(roundId, TextStyles.Caption1, innerWidth);
        var height = 10f * scale + idLabelSize.Y + 4f * scale + idBlock.Y;
        var commitLabel = Loc.T(L.Casino.RoundCommit);
        var commitLabelSize = Vector2.Zero;
        var commitBlock = Vector2.Zero;
        if (commit.Length > 0)
        {
            commitLabelSize = Typography.Measure(commitLabel, TextStyles.Caption1);
            commitBlock = Typography.MeasureWrappedBlock(commit, TextStyles.Caption1, innerWidth);
            height += 10f * scale + commitLabelSize.Y + 4f * scale + commitBlock.Y;
        }

        var playedLine = playedAtUnix > 0
            ? Loc.T(L.Casino.RoundPlayed) + ": " + TimeText.DayLabel(playedAtUnix) + " "
                + TimeText.Clock(playedAtUnix)
            : string.Empty;
        var playedSize = Vector2.Zero;
        if (playedLine.Length > 0)
        {
            playedSize = Typography.Measure(playedLine, TextStyles.Caption1);
            height += 10f * scale + playedSize.Y;
        }

        height += 12f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 16f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, rounding, scale);

        var cursorY = min.Y + 10f * scale;
        Typography.Draw(drawList, new Vector2(min.X + inset, cursorY), idLabel, ui.MutedInk, TextStyles.Caption1);
        cursorY += idLabelSize.Y + 4f * scale;
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, cursorY), roundId, ui.BodyInk, TextStyles.Caption1,
            innerWidth);
        cursorY += idBlock.Y;
        if (commit.Length > 0)
        {
            cursorY += 10f * scale;
            Typography.Draw(drawList, new Vector2(min.X + inset, cursorY), commitLabel, ui.MutedInk,
                TextStyles.Caption1);
            cursorY += commitLabelSize.Y + 4f * scale;
            Typography.DrawWrappedLeft(new Vector2(min.X + inset, cursorY), commit, ui.BodyInk,
                TextStyles.Caption1, innerWidth);
            cursorY += commitBlock.Y;
        }

        if (playedLine.Length > 0)
        {
            cursorY += 10f * scale;
            Typography.Draw(drawList, new Vector2(min.X + inset, cursorY), playedLine, ui.MutedInk,
                TextStyles.Caption1);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 10f * scale));
    }

    internal static string BuildRoundDetailsBlob(VerifiedCasinoRound verifiedRound)
    {
        var round = verifiedRound.Round;
        var verdict = verifiedRound.Verdict switch
        {
            CasinoRoundVerdict.Match => "match",
            CasinoRoundVerdict.Mismatch => "mismatch",
            _ => "unrevealed",
        };
        var builder = new System.Text.StringBuilder(256 + round.DrawLog.Length);
        builder.Append("roundId=").Append(round.RoundId).Append('\n');
        builder.Append("gameKind=").Append(round.GameKind).Append('\n');
        builder.Append("state=").Append(round.State).Append('\n');
        builder.Append("stake=").Append(round.Stake).Append('\n');
        builder.Append("payout=").Append(round.Payout).Append('\n');
        builder.Append("seedCommitHash=").Append(round.SeedCommitHash).Append('\n');
        builder.Append("seedRevealed=").Append(round.SeedRevealed).Append('\n');
        builder.Append("nextSeedHash=").Append(round.NextSeedHash).Append('\n');
        builder.Append("drawLog=").Append(round.DrawLog).Append('\n');
        builder.Append("verdict=").Append(verdict);
        return builder.ToString();
    }
}
