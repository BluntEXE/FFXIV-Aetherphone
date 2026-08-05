namespace Aetherphone.Core.Theme;

internal sealed record NamedColor(string Name, Vector4 Color);

internal static class ThemeCatalog
{
    public static readonly IReadOnlyList<NamedColor> Accents = new NamedColor[]
    {
        new("Violet", new Vector4(0.55f, 0.45f, 0.95f, 1f)), new("Blue", new Vector4(0.30f, 0.55f, 0.98f, 1f)),
        new("Green", new Vector4(0.20f, 0.78f, 0.45f, 1f)), new("Pink", new Vector4(0.95f, 0.40f, 0.65f, 1f)),
        new("Amber", new Vector4(0.96f, 0.65f, 0.20f, 1f)),
    };

    public const string DefaultCaseName = "Titanium";

    private static readonly PhoneCase[] BuiltInCases =
    {
        PhoneCase.Color(DefaultCaseName, new Vector4(0.145f, 0.145f, 0.170f, 1f)),
        PhoneCase.Art("Black", new Vector4(0.158f, 0.158f, 0.158f, 1f)),
        PhoneCase.Art("Blue", new Vector4(0.330f, 0.551f, 0.962f, 1f)),
        PhoneCase.Art("Green", new Vector4(0.254f, 0.766f, 0.460f, 1f)),
        PhoneCase.Art("Grey", new Vector4(0.227f, 0.227f, 0.227f, 1f)),
        PhoneCase.Art("Lavender", new Vector4(0.304f, 0.276f, 0.368f, 1f)),
        PhoneCase.Art("Pink", new Vector4(0.930f, 0.414f, 0.646f, 1f)),
        PhoneCase.Art("Purple", new Vector4(0.551f, 0.460f, 0.930f, 1f)),
        PhoneCase.Art("Teal", new Vector4(0.219f, 0.409f, 0.455f, 1f)),
        PhoneCase.Art("White", new Vector4(0.892f, 0.892f, 0.892f, 1f)),
        PhoneCase.Art("Yellow", new Vector4(0.941f, 0.646f, 0.254f, 1f)),
        PhoneCase.Art("BlackCatGradient", new Vector4(0.379f, 0.254f, 0.325f, 1f)),
        PhoneCase.Art("BruteBomberGradient", new Vector4(0.522f, 0.210f, 0.175f, 1f)),
        PhoneCase.Art("DancingGreenGradient", new Vector4(0.771f, 0.784f, 0.677f, 1f)),
        PhoneCase.Art("HoneyBLovelyGradient", new Vector4(0.810f, 0.704f, 0.551f, 1f)),
        PhoneCase.Art("HowlingBladeGradient", new Vector4(0.500f, 0.512f, 0.443f, 1f)),
        PhoneCase.Art("SugarRiotGradient", new Vector4(0.261f, 0.447f, 0.555f, 1f)),
        PhoneCase.Art("TheTyrantGradient", new Vector4(0.580f, 0.409f, 0.458f, 1f)),
        PhoneCase.Art("VampFataleGradient", new Vector4(0.480f, 0.135f, 0.225f, 1f)),
        PhoneCase.Art("WickedThunderGradient", new Vector4(0.659f, 0.568f, 0.716f, 1f)),
        PhoneCase.Art("Silkie", new Vector4(1.000f, 0.918f, 0.914f, 1f)),
        PhoneCase.Art("FatCat", new Vector4(0.753f, 0.708f, 0.648f, 1f)),
        PhoneCase.Art("CosmicEX", new Vector4(0.160f, 0.160f, 0.197f, 1f)),
        PhoneCase.Art("Caduceus", new Vector4(0.414f, 0.398f, 0.209f, 1f)),
        PhoneCase.Art("MagicalGirl", new Vector4(0.897f, 0.569f, 0.707f, 1f)),
    };

    public static IReadOnlyList<PhoneCase> Cases { get; } = BuiltInCases;

    public static bool IsCustomAccent(string name) => name.Length > 0 && name[0] == '#';

    public static Vector4 ResolveAccent(string name) =>
        IsCustomAccent(name) && HexColor.TryParse(name, out var custom)
            ? custom
            : Accents[IndexOf(Accents, name)].Color;

    public static PhoneCase ResolveCase(string id) => Cases[IndexOf(Cases, id)];

    public static int IndexOf(IReadOnlyList<NamedColor> list, string name)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].Name == name)
            {
                return index;
            }
        }

        return 0;
    }

    public static int IndexOf(IReadOnlyList<PhoneCase> list, string id)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].Id == id)
            {
                return index;
            }
        }

        return 0;
    }
}
