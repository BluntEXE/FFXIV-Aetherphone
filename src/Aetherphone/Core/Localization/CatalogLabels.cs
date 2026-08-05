using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Localization;

internal static class CatalogLabels
{
    public static string ThemeMode(ThemeMode mode) =>
        mode switch
        {
            Core.Theme.ThemeMode.Light => Loc.T(L.Settings.ThemeLight),
            Core.Theme.ThemeMode.Auto => Loc.T(L.Settings.ThemeAuto),
            _ => Loc.T(L.Settings.ThemeDark),
        };

    public static string Accent(string identifier) =>
        identifier switch
        {
            "Violet" => Loc.T(L.Catalogs.AccentViolet),
            "Blue" => Loc.T(L.Catalogs.AccentBlue),
            "Green" => Loc.T(L.Catalogs.AccentGreen),
            "Pink" => Loc.T(L.Catalogs.AccentPink),
            "Amber" => Loc.T(L.Catalogs.AccentAmber),
            _ => identifier,
        };

    public static string PhoneCase(string identifier) =>
        identifier switch
        {
            "Titanium" => Loc.T(L.Catalogs.CaseTitanium),
            "Black" => Loc.T(L.Catalogs.CaseBlack),
            "Blue" => Loc.T(L.Catalogs.CaseBlue),
            "Green" => Loc.T(L.Catalogs.CaseGreen),
            "Grey" => Loc.T(L.Catalogs.CaseGrey),
            "Lavender" => Loc.T(L.Catalogs.CaseLavender),
            "Pink" => Loc.T(L.Catalogs.CasePink),
            "Purple" => Loc.T(L.Catalogs.CasePurple),
            "Teal" => Loc.T(L.Catalogs.CaseTeal),
            "White" => Loc.T(L.Catalogs.CaseWhite),
            "Yellow" => Loc.T(L.Catalogs.CaseYellow),
            "BlackCatGradient" => Loc.T(L.Catalogs.CaseBlackCat),
            "BruteBomberGradient" => Loc.T(L.Catalogs.CaseBruteBomber),
            "DancingGreenGradient" => Loc.T(L.Catalogs.CaseDancingGreen),
            "HoneyBLovelyGradient" => Loc.T(L.Catalogs.CaseHoneyBLovely),
            "HowlingBladeGradient" => Loc.T(L.Catalogs.CaseHowlingBlade),
            "SugarRiotGradient" => Loc.T(L.Catalogs.CaseSugarRiot),
            "TheTyrantGradient" => Loc.T(L.Catalogs.CaseTheTyrant),
            "VampFataleGradient" => Loc.T(L.Catalogs.CaseVampFatale),
            "WickedThunderGradient" => Loc.T(L.Catalogs.CaseWickedThunder),
            "Silkie" => Loc.T(L.Catalogs.CaseSilkie),
            "FatCat" => Loc.T(L.Catalogs.CaseFatCat),
            "CosmicEX" => Loc.T(L.Catalogs.CaseCosmicEx),
            "Caduceus" => Loc.T(L.Catalogs.CaseCaduceus),
            "MagicalGirl" => Loc.T(L.Catalogs.CaseMagicalGirl),
            _ => identifier,
        };

    public static string RadioCategory(string identifier) =>
        identifier switch
        {
            "Lofi" => Loc.T(L.Catalogs.RadioLofi),
            "Chillout" => Loc.T(L.Catalogs.RadioChillout),
            "Jazz" => Loc.T(L.Catalogs.RadioJazz),
            "Classical" => Loc.T(L.Catalogs.RadioClassical),
            "Ambient" => Loc.T(L.Catalogs.RadioAmbient),
            "Electronic" => Loc.T(L.Catalogs.RadioElectronic),
            "Pop" => Loc.T(L.Catalogs.RadioPop),
            "Rock" => Loc.T(L.Catalogs.RadioRock),
            "Metal" => Loc.T(L.Catalogs.RadioMetal),
            "Hip-Hop" => Loc.T(L.Catalogs.RadioHipHop),
            "Soundtrack" => Loc.T(L.Catalogs.RadioSoundtrack),
            "Anime" => Loc.T(L.Catalogs.RadioAnime),
            _ => identifier,
        };
}
