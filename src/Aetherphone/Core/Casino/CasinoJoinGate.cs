namespace Aetherphone.Core.Casino;

internal static class CasinoJoinGate
{
    public static bool ArmsWait(int phaseAtSit)
    {
        return phaseAtSit != CasinoRoomPhases.Open;
    }

    public static bool ClearsWait(int phase)
    {
        return phase == CasinoRoomPhases.Open;
    }

    public static bool Waiting(bool seated, bool armed, bool serverSaysNextHand)
    {
        return seated && (armed || serverSaysNextHand);
    }

    public static bool DealtThisHand(bool seated, bool waiting)
    {
        return seated && !waiting;
    }

    public static bool CanPlaceBet(int phase, bool seated, bool waiting, bool draining, bool stakesPaused)
    {
        return phase == CasinoRoomPhases.Open && DealtThisHand(seated, waiting) && !draining && !stakesPaused;
    }

    public static bool CanAct(bool seated, bool waiting, bool myTurn)
    {
        return myTurn && DealtThisHand(seated, waiting);
    }
}
