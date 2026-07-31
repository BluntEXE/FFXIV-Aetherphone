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
        PhoneCase.Art("Foxtail", new Vector4(0.310f, 0.196f, 0.114f, 1f)),
        PhoneCase.Art("Frostnip", new Vector4(0.599f, 0.698f, 0.746f, 1f)),
        PhoneCase.Art("Nacrelle", new Vector4(0.562f, 0.473f, 0.329f, 1f)),
        PhoneCase.Art("Verdigry", new Vector4(0.490f, 0.465f, 0.360f, 1f)),
        PhoneCase.Art("Voltpup", new Vector4(0.158f, 0.279f, 0.324f, 1f)),
        PhoneCase.Art("Aetherite", new Vector4(0.624f, 0.672f, 0.907f, 1f)),
        PhoneCase.Art("Chocorunner", new Vector4(0.717f, 0.541f, 0.186f, 1f)),
        PhoneCase.Art("Gembuncle", new Vector4(0.685f, 0.694f, 0.436f, 1f)),
        PhoneCase.Art("Ironsworn", new Vector4(0.273f, 0.311f, 0.371f, 1f)),
        PhoneCase.Art("Kuponuff", new Vector4(0.780f, 0.666f, 0.587f, 1f)),
        PhoneCase.Art("Prickletot", new Vector4(0.570f, 0.635f, 0.380f, 1f)),
        PhoneCase.Art("Amourette", new Vector4(0.969f, 0.877f, 0.860f, 1f)),
        PhoneCase.Art("Barkleigh", new Vector4(0.744f, 0.549f, 0.260f, 1f)),
        PhoneCase.Art("Emberlash", new Vector4(0.794f, 0.697f, 0.490f, 1f)),
        PhoneCase.Art("Tidecaller", new Vector4(0.266f, 0.516f, 0.556f, 1f)),
        PhoneCase.Art("Vesperine", new Vector4(0.719f, 0.690f, 0.949f, 1f)),
        PhoneCase.Art("Whiskerlune", new Vector4(0.242f, 0.222f, 0.360f, 1f)),
        PhoneCase.Art("Silkie", new Vector4(1.000f, 0.922f, 0.918f, 1f)),
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
