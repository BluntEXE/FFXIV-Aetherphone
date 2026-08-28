namespace Aetherphone.Core.Strats;

internal static class StratsRoles
{
    public const int SlotCount = 8;
    private static readonly string[] Names = { "Tank", "Healer", "Melee", "Ranged" };
    private static readonly string[] EnglishLabels = { "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2" };
    private static readonly string[] JapaneseLabels = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };

    public static string RoleName(int slot) => Names[Math.Clamp(slot, 0, SlotCount - 1) / 2];

    public static int Party(int slot) => Math.Clamp(slot, 0, SlotCount - 1) % 2 + 1;

    public static string Label(int slot, bool japanese) =>
        japanese ? JapaneseLabels[Math.Clamp(slot, 0, SlotCount - 1)] : EnglishLabels[Math.Clamp(slot, 0, SlotCount - 1)];
}

internal sealed class StratsSelection
{
    public string FightKey = string.Empty;
    public string StratId = string.Empty;
    public int Slot;
    public readonly Dictionary<string, string> Toggles = new(StringComparer.Ordinal);
    public string Alignment = string.Empty;
    public int Tab;
    public int Revision;

    public void Touch() => Revision++;

    public void Load(string fightKey, StratsFightSelection? saved, int defaultSlot)
    {
        FightKey = fightKey;
        Toggles.Clear();
        if (saved is null)
        {
            StratId = string.Empty;
            Slot = defaultSlot;
            Alignment = string.Empty;
            Tab = 0;
            Touch();
            return;
        }

        StratId = saved.Strat;
        Slot = Math.Clamp(saved.Slot, 0, StratsRoles.SlotCount - 1);
        foreach (var pair in saved.Toggles)
        {
            Toggles[pair.Key] = pair.Value;
        }

        Alignment = saved.Alignment;
        Tab = Math.Max(0, saved.Tab);
        Touch();
    }

    public StratsFightSelection Capture()
    {
        var snapshot = new StratsFightSelection
        {
            Strat = StratId,
            Slot = Slot,
            Alignment = Alignment,
            Tab = Tab,
        };
        foreach (var pair in Toggles)
        {
            snapshot.Toggles[pair.Key] = pair.Value;
        }

        return snapshot;
    }
}
