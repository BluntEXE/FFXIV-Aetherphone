using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class CoinPage : ISettingsPage
{
    public string Title => Loc.T(L.Coin.SettingsRow);
    public string Summary => SummaryText();
    public FontAwesomeIcon Icon => FontAwesomeIcon.Coins;
    public Vector4 Tint => AppAccents.For("coin");

    private readonly CoinStore store;
    private readonly AppSkin ui = new(AppPalettes.Coin);

    public CoinPage(CoinStore store)
    {
        this.store = store;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        ui.Theme = theme;
        store.EnsureFresh();
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Title, theme);
            var wallet = store.Wallet;
            if (wallet is null)
            {
                SettingsSection.Hint(Loc.T(L.Coin.SignInHint), theme);
                return;
            }

            CoinHero.Draw(wallet, AppPalettes.Coin);
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            if (!wallet.Paused)
            {
                CoinHero.DrawCapBar(wallet, AppPalettes.Coin);
                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }

            SettingsSection.Hint(Loc.T(L.Coin.AboutWhat), theme);
            ImGui.Dummy(new Vector2(0f, 8f * scale));

            var entries = store.Entries;
            if (entries.Length > 0)
            {
                CoinLedgerList.Draw(entries, CoinLedgerList.FilterAll, theme, ui);
                if (store.LoadingMore)
                {
                    InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
                }
                else if (!store.EndReached && InfiniteScroll.ReachedBottom())
                {
                    store.LoadMore();
                }
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
    }

    private string SummaryText()
    {
        var wallet = store.Wallet;
        return wallet is null ? string.Empty : wallet.Balance.ToString("N0", Loc.Culture);
    }
}
