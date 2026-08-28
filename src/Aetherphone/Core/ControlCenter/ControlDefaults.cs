namespace Aetherphone.Core.ControlCenter;

internal readonly struct ControlDefault
{
    public readonly string ModuleId;
    public readonly ControlSpan Span;

    public ControlDefault(string moduleId, ControlSpan span)
    {
        ModuleId = moduleId;
        Span = span;
    }
}

internal static class ControlDefaults
{
    public static readonly ControlDefault[] Layout =
    {
        new("dnd", ControlSpan.Small),
        new("silent", ControlSpan.Small),
        new("calls", ControlSpan.Small),
        new("idle", ControlSpan.Small),
        new("media", ControlSpan.Large),
        new("brightness", ControlSpan.Tall),
        new("volume", ControlSpan.Tall),
        new("settings", ControlSpan.Small),
        new("accent", ControlSpan.Wide),
    };
}
