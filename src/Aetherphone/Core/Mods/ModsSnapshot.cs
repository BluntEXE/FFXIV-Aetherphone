namespace Aetherphone.Core.Mods;

internal sealed class ModsSnapshot
{
    public bool ShowNsfw { get; set; }
    public bool ShowNsfl { get; set; }
    public bool ShowContentWarnings { get; set; } = true;
    public bool HidePaid { get; set; }
    public bool BlurSensitive { get; set; } = true;
    public string OneClickPassword { get; set; } = string.Empty;
    public int Sort { get; set; }

    public ModsFilter Filter => new(ShowNsfw, ShowNsfw && ShowNsfl, ShowContentWarnings, !HidePaid);

    public ModsSort SortKind => Sort >= 0 && Sort < ModsQuery.SortCount ? (ModsSort)Sort : ModsSort.Trending;
}
