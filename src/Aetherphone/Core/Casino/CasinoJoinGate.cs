namespace Aetherphone.Core.Casino;

// Sitting down and being dealt in are two different moments. A seat taken while a hand is in play
// waits at the rail until the table comes back round to its betting window, and that wait is armed
// by the phase the seat was taken in and cleared by the phase alone: a timer would either free the
// seat early on a slow hand or hold it past a fast one, and both of those look like a bug to a
// player watching cards land on everybody else.
//
// Draining and the floor pause are the same shape from the other end. They refuse new money without
// touching the hand already dealt, so the bet path closes while the action path stays open: a table
// winding down still lets the hand it started finish honestly.
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

    // Acting on a hand that is already dealt is not a new stake, so it survives the drain and the
    // pause. Refusing it would strand chips in the middle of a hand nobody can finish.
    public static bool CanAct(bool seated, bool waiting, bool myTurn)
    {
        return myTurn && DealtThisHand(seated, waiting);
    }
}
