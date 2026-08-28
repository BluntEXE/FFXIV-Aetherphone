using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddPlayTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "games", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.GamesTitle, L.Onboarding.GamesBody),
                GuideStep.Point(L.Onboarding.GamesFeaturedTitle, L.Onboarding.GamesFeaturedBody, "games.featured"),
                GuideStep.Point(L.Onboarding.GamesLibraryTitle, L.Onboarding.GamesLibraryBody, "games.library"),
            });
        Add(tours, "casino", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Casino, L.Onboarding.CasinoBody),
                GuideStep.Point(L.Onboarding.CasinoChipsTitle, L.Onboarding.CasinoChipsBody, "casino.chipbar"),
                GuideStep.Point(L.Onboarding.CasinoSpinTitle, L.Onboarding.CasinoSpinBody, "casino.spin"),
                GuideStep.Point(L.Onboarding.CasinoFloorTitle, L.Onboarding.CasinoFloorBody, "casino.games"),
                GuideStep.Point(L.Onboarding.CasinoRecordsTitle, L.Onboarding.CasinoRecordsBody, "casino.records"),
                GuideStep.Point(L.Onboarding.CasinoLimitsTitle, L.Onboarding.CasinoLimitsBody, "casino.limits"),
                GuideStep.Tap(L.Onboarding.CasinoLiveTitle, L.Onboarding.CasinoLiveBody, "casino.tabs",
                    "casino.tab.live"),
                GuideStep.Point(L.Onboarding.CasinoRoomsTitle, L.Onboarding.CasinoRoomsBody, "casino.live.rooms"),
            });
        Add(tours, "coin", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Coin, L.Onboarding.CoinBody),
                GuideStep.Point(L.Onboarding.CoinBalanceTitle, L.Onboarding.CoinBalanceBody, "coin.balance"),
                GuideStep.Point(L.Onboarding.CoinCheckInTitle, L.Onboarding.CoinCheckInBody, "coin.checkin"),
                GuideStep.Point(L.Onboarding.CoinEarnTitle, L.Onboarding.CoinEarnBody, "coin.earn"),
                GuideStep.Tap(L.Onboarding.CoinShopTitle, L.Onboarding.CoinShopBody, "coin.tabs", "coin.tab.shop"),
                GuideStep.Note(L.Onboarding.CoinFairTitle, L.Onboarding.CoinFairBody),
            });
    }
}
