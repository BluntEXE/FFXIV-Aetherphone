namespace Aetherphone.Core.Casino;

internal enum CasinoLaunchKind
{
    Floor,
    Tables,
    Table,
}

internal readonly record struct CasinoLaunch(CasinoLaunchKind Kind, string TableId = "");

internal sealed class CasinoLauncher
{
    private CasinoLaunch? pending;

    public void RequestTable(string tableId)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        pending = new CasinoLaunch(CasinoLaunchKind.Table, tableId);
    }

    public void RequestTables()
    {
        pending = new CasinoLaunch(CasinoLaunchKind.Tables);
    }

    public bool TryConsume(out CasinoLaunch launch)
    {
        if (pending is not { } held)
        {
            launch = default;
            return false;
        }

        pending = null;
        launch = held;
        return true;
    }
}
