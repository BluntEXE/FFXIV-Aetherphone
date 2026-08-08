using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private void DrawHistory(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);
        historyRefresh.Draw(body, surface.Pull, surface.Dragging, !store.LoadedOnce, ui.MutedInk, store.RefreshNow);

        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var filterRow = new Rect(origin, new Vector2(origin.X + width, origin.Y + 30f * scale));
        filterOptions[CoinLedgerList.FilterAll] = Loc.T(L.Coin.FilterAll);
        filterOptions[CoinLedgerList.FilterEarned] = Loc.T(L.Coin.FilterEarned);
        filterOptions[CoinLedgerList.FilterSpent] = Loc.T(L.Coin.FilterSpent);
        historyFilter = SegmentStrip.Draw("coin.historyFilter", filterRow, filterOptions, historyFilter, ui.Palette);
        ImGui.Dummy(new Vector2(width, 36f * scale));

        var entries = store.Entries;
        if (!store.LoadedOnce)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        if (CoinLedgerList.CountMatching(entries, historyFilter) == 0)
        {
            var empty = new Rect(new Vector2(body.Min.X, filterRow.Max.Y), body.Max);
            EmptyState.Draw(empty, ui, FontAwesomeIcon.Receipt, Loc.T(L.Coin.HistoryEmptyTitle),
                Loc.T(L.Coin.HistoryEmptyHint));
            return;
        }

        CoinLedgerList.Draw(entries, historyFilter, theme, ui);

        if (store.LoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
        }
        else if (!store.EndReached && InfiniteScroll.ReachedBottom())
        {
            store.LoadMore();
        }

        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }
}
