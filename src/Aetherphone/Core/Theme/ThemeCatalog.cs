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
        PhoneCase.Color("Graphite", new Vector4(0.085f, 0.085f, 0.095f, 1f)),
        PhoneCase.Color("Silver", new Vector4(0.700f, 0.710f, 0.745f, 1f)),
        PhoneCase.Color("Gold", new Vector4(0.660f, 0.530f, 0.300f, 1f)),
        PhoneCase.Color("Rose", new Vector4(0.720f, 0.500f, 0.480f, 1f)),
        PhoneCase.Color("Midnight", new Vector4(0.105f, 0.135f, 0.255f, 1f)),
        PhoneCase.Color("Jade", new Vector4(0.115f, 0.265f, 0.215f, 1f)),
        PhoneCase.Color("Coral", new Vector4(0.740f, 0.310f, 0.280f, 1f)),
        PhoneCase.Color("Lavender", new Vector4(0.480f, 0.420f, 0.680f, 1f)),
        PhoneCase.Color("Porcelain", new Vector4(0.880f, 0.880f, 0.905f, 1f)),
    };

    public static IReadOnlyList<PhoneCase> Cases { get; } = BuiltInCases;

    public static Vector4 ResolveAccent(string name) => Accents[IndexOf(Accents, name)].Color;

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
