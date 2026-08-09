namespace Aetherphone.Core.Casino;

internal enum CasinoLaunchKind
{
    Floor,
    Tables,
    Table,
}

internal readonly record struct CasinoLaunch(CasinoLaunchKind Kind, string TableId = "");

// Opening an app that is already open re-fires OnOpened, so the request has to survive until the
// app consumes it rather than being acted on at the moment it is made: a turn alert tapped while
// the casino is on screen has to reach the table just as surely as one tapped from the lock screen.
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
