namespace Aetherphone.Windows.Components;

internal readonly struct TextRun
{
    public readonly string Text;
    public readonly Vector4 Tint;
    public readonly int Target;
    public readonly bool Interactive;

    public TextRun(string text, Vector4 tint, int target, bool interactive)
    {
        Text = text;
        Tint = tint;
        Target = target;
        Interactive = interactive;
    }

    public static TextRun Plain(string text) => new(text, default, -1, false);

    public static TextRun Link(string text, Vector4 tint, int target) => new(text, tint, target, true);
}
