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
            "Foxtail" => Loc.T(L.Catalogs.CaseFoxtail),
            "Frostnip" => Loc.T(L.Catalogs.CaseFrostnip),
            "Nacrelle" => Loc.T(L.Catalogs.CaseNacrelle),
            "Verdigry" => Loc.T(L.Catalogs.CaseVerdigry),
            "Voltpup" => Loc.T(L.Catalogs.CaseVoltpup),
            "Aetherite" => Loc.T(L.Catalogs.CaseAetherite),
            "Chocorunner" => Loc.T(L.Catalogs.CaseChocorunner),
            "Gembuncle" => Loc.T(L.Catalogs.CaseGembuncle),
            "Ironsworn" => Loc.T(L.Catalogs.CaseIronsworn),
            "Kuponuff" => Loc.T(L.Catalogs.CaseKuponuff),
            "Prickletot" => Loc.T(L.Catalogs.CasePrickletot),
            "Amourette" => Loc.T(L.Catalogs.CaseAmourette),
            "Barkleigh" => Loc.T(L.Catalogs.CaseBarkleigh),
            "Emberlash" => Loc.T(L.Catalogs.CaseEmberlash),
            "Tidecaller" => Loc.T(L.Catalogs.CaseTidecaller),
            "Vesperine" => Loc.T(L.Catalogs.CaseVesperine),
            "Whiskerlune" => Loc.T(L.Catalogs.CaseWhiskerlune),
            "Silkie" => Loc.T(L.Catalogs.CaseSilkie),
            _ => identifier,
        };

    public static string Ringtone(uint soundId) =>
        soundId switch
        {
            7 => Loc.T(L.Catalogs.RingtonePing),
            1 => Loc.T(L.Catalogs.RingtoneChime),
            3 => Loc.T(L.Catalogs.RingtoneBell),
            10 => Loc.T(L.Catalogs.RingtoneAlert),
            16 => Loc.T(L.Catalogs.RingtoneKnock),
            0 => Loc.T(L.Catalogs.RingtoneSilent),
            _ => string.Empty,
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
