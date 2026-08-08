using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Windows.Widgets;

internal sealed class CoinWidget : IHomeWidget
{
    private readonly CoinStore coins;
    private readonly AethernetSession session;
    private RollingValue balance;

    public CoinWidget(CoinStore coins, AethernetSession session)
    {
        this.coins = coins;
        this.session = session;
    }

    public string Id => "coin.balance";
    public string DisplayName => Loc.T(L.Apps.Coin);
    public string AppId => "coin";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Card(context.DrawList, context.Bounds, context.Scale, context.Opacity);
        var bounds = context.Bounds;
        var scale = context.Scale;
        var pad = 13f * scale;
        var accent = AppAccents.For("coin");

        WidgetChrome.Eyebrow(context.DrawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad),
            Loc.T(L.Coin.Balance), Palette.WithAlpha(accent, context.Opacity), scale, context.Opacity);

        var wallet = coins.Wallet;
        var target = wallet?.Balance ?? session.CurrentUser?.Coins ?? 0;
        balance.Update((int)Math.Clamp(target, 0, int.MaxValue), context.Delta);

        var text = balance.Display.ToString("N0", Loc.Culture);
        var valueScale = (context.Size == WidgetSize.Small ? 1.45f : 1.85f) * balance.PopScale;
        var valueSize = Typography.Measure(text, valueScale, FontWeight.Bold);
        var fitted = Typography.FitScale(text, bounds.Width - pad * 2f, valueScale, 0.9f, FontWeight.Bold);
        valueSize = Typography.Measure(text, fitted, FontWeight.Bold);
        var center = new Vector2(bounds.Center.X, bounds.Center.Y + 4f * scale);
        Typography.Draw(context.DrawList, new Vector2(center.X - valueSize.X * 0.5f, center.Y - valueSize.Y * 0.5f),
            text, Palette.WithAlpha(context.Theme.TextStrong, context.Opacity), fitted, FontWeight.Bold);

        if (context.Size != WidgetSize.Medium || wallet is null)
        {
            return;
        }

        var progress = Loc.T(L.Coin.CapProgress,
            wallet.EarnedToday.ToString("N0", Loc.Culture),
            wallet.DailyCap.ToString("N0", Loc.Culture));
        var progressSize = Typography.Measure(progress, TextStyles.Footnote);
        Typography.Draw(context.DrawList,
            new Vector2(bounds.Center.X - progressSize.X * 0.5f, bounds.Max.Y - pad - progressSize.Y),
            progress, Palette.WithAlpha(context.Theme.TextMuted, context.Opacity), TextStyles.Footnote);
    }

    public void Dispose()
    {
    }
}
