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
        PhoneCase.Art("Ironworks", new Vector4(0.335f, 0.375f, 0.421f, 1f)),
        PhoneCase.Art("Emberforge", new Vector4(0.473f, 0.259f, 0.088f, 1f)),
        PhoneCase.Art("Voidsent", new Vector4(0.190f, 0.122f, 0.361f, 1f)),
        PhoneCase.Art("Bulwark", new Vector4(0.224f, 0.249f, 0.277f, 1f)),
        PhoneCase.Art("Carbonweave", new Vector4(0.122f, 0.133f, 0.153f, 1f)),
        PhoneCase.Art("Alabaster", new Vector4(0.826f, 0.798f, 0.751f, 1f)),
        PhoneCase.Art("Silkie", new Vector4(0.918f, 0.894f, 0.867f, 1f)),
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
