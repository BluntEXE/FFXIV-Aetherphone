namespace Aetherphone.Core.Casino;

// The room cycle the server owns, mirrored so the cabinet can name what it is looking at. The
// client never counts these phases down on its own clock: a phase is whatever the last snapshot
// said, and it ends at the absolute instant the snapshot carried.
internal static class CasinoRoomPhases
{
    public const int Open = 0;

    public const int Locked = 1;

    public const int Result = 2;
}

internal static class CasinoRoomIds
{
    public const string WheelFloor = "wheel-floor";
}
