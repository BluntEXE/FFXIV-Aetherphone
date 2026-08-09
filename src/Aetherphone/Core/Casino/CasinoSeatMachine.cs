namespace Aetherphone.Core.Casino;

internal enum CasinoSeatStage
{
    Watching,
    Sitting,
    Seated,
    Elsewhere,
    Claiming,
    Standing,
}

// What the screen is still waiting for the board to confirm after the server has already answered an
// intent. Bound is a sit or a takeover the server granted, Released is a stand it granted outright.
internal enum CasinoSeatSettle
{
    None,
    Bound,
    Released,
}

internal enum CasinoSeatSignal
{
    SitRequested,
    SitGranted,
    SitRefused,
    SeatBoundHere,
    SeatBoundElsewhere,
    TakeOverRequested,
    ClaimGranted,
    ClaimRefused,
    StandRequested,
    StandGranted,
    StandQueued,
    StandRefused,
    SeatLost,
    Left,
}

// A seat belongs to an account, not to a phone, so two phones signed into one account can both be
// looking at the same table. Exactly one of them holds the binding: the seat that sees hole cards
// and spends chips. The other one is told so in plain words and offered the takeover, and the one
// transition this machine refuses to make on its own is Elsewhere into Claiming: a claim always
// costs a gesture, because it moves money and private cards away from a screen somebody may be
// sitting in front of.
//
// Every intent has its own in-flight stage rather than a shared boolean, so a refusal lands the
// player back exactly where they started (a refused stand is still Seated, a refused claim is still
// Elsewhere) and a snapshot that arrives mid-flight can still overrule the whole thing: SeatLost
// and Left win from every stage, because the server closing the seat is not a request.
internal static class CasinoSeatMachine
{
    // A grant the server has already answered has to survive the boards that were taken before it.
    // The poll carrying my new seat can be a whole interval behind the answer that created it, and
    // letting that older board speak would undo the grant on the very frame it landed: the footer
    // would offer the seat back to somebody already sitting in it and the second tap comes back
    // refused. The latch is a grace rather than a lock, so a server that never confirms cannot
    // strand the screen.
    public const long SettleGraceMilliseconds = 6_000;

    public static CasinoSeatStage Next(CasinoSeatStage held, CasinoSeatSignal signal)
    {
        if (signal == CasinoSeatSignal.Left)
        {
            return CasinoSeatStage.Watching;
        }

        // A post already on the wire outranks a snapshot that has not caught up with it yet:
        // between pressing sit and the answer arriving, every frame honestly reports no seat, and
        // letting that cancel the intent would flip the screen back a dozen times a second.
        if (signal == CasinoSeatSignal.SeatLost)
        {
            return held is CasinoSeatStage.Sitting or CasinoSeatStage.Claiming ? held : CasinoSeatStage.Watching;
        }

        return held switch
        {
            CasinoSeatStage.Watching => FromWatching(signal),
            CasinoSeatStage.Sitting => FromSitting(signal),
            CasinoSeatStage.Seated => FromSeated(signal),
            CasinoSeatStage.Elsewhere => FromElsewhere(signal),
            CasinoSeatStage.Claiming => FromClaiming(signal),
            _ => FromStanding(signal),
        };
    }

    public static bool Busy(CasinoSeatStage stage)
    {
        return stage is CasinoSeatStage.Sitting or CasinoSeatStage.Claiming or CasinoSeatStage.Standing;
    }

    public static bool Holds(CasinoSeatStage stage)
    {
        return stage is CasinoSeatStage.Seated or CasinoSeatStage.Standing;
    }

    public static bool Watching(CasinoSeatStage stage)
    {
        return stage is CasinoSeatStage.Watching or CasinoSeatStage.Sitting;
    }

    public static bool ShowsTakeOver(CasinoSeatStage stage)
    {
        return stage is CasinoSeatStage.Elsewhere or CasinoSeatStage.Claiming;
    }

    // The snapshot speaks once a frame and it only ever says one of three things about my seat, so
    // the reading of it lives here rather than at the call site where a missing case would quietly
    // strand the screen in whatever it was showing before.
    public static CasinoSeatSignal SignalFor(bool hasSeat, bool boundElsewhere)
    {
        if (!hasSeat)
        {
            return CasinoSeatSignal.SeatLost;
        }

        return boundElsewhere ? CasinoSeatSignal.SeatBoundElsewhere : CasinoSeatSignal.SeatBoundHere;
    }

    // Which fact the board still owes the screen, read off the stage the intent was posted from. A
    // stand the table queued to the end of the hand owes nothing: the seat is still mine until it
    // settles, which is exactly what the stale board already says.
    public static CasinoSeatSettle SettleFor(CasinoSeatStage requested, bool atHandEnd)
    {
        return requested switch
        {
            CasinoSeatStage.Sitting => CasinoSeatSettle.Bound,
            CasinoSeatStage.Claiming => CasinoSeatSettle.Bound,
            CasinoSeatStage.Standing => atHandEnd ? CasinoSeatSettle.None : CasinoSeatSettle.Released,
            _ => CasinoSeatSettle.None,
        };
    }

    // Whether the board is allowed to speak at all this frame. A latch opens on the first reading
    // that agrees with the grant, and on nothing else until the grace runs out.
    public static bool AcceptsBoard(CasinoSeatSettle awaited, CasinoSeatSignal signal, long armedAtTick,
        long nowTick)
    {
        return Settles(awaited, signal) || nowTick - armedAtTick >= SettleGraceMilliseconds;
    }

    private static bool Settles(CasinoSeatSettle awaited, CasinoSeatSignal signal)
    {
        return awaited switch
        {
            CasinoSeatSettle.Bound => signal == CasinoSeatSignal.SeatBoundHere,
            CasinoSeatSettle.Released => signal == CasinoSeatSignal.SeatLost,
            _ => true,
        };
    }

    private static CasinoSeatStage FromWatching(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.SitRequested => CasinoSeatStage.Sitting,
            CasinoSeatSignal.SeatBoundHere => CasinoSeatStage.Seated,
            CasinoSeatSignal.SeatBoundElsewhere => CasinoSeatStage.Elsewhere,
            _ => CasinoSeatStage.Watching,
        };
    }

    private static CasinoSeatStage FromSitting(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.SitGranted => CasinoSeatStage.Seated,
            CasinoSeatSignal.SitRefused => CasinoSeatStage.Watching,
            CasinoSeatSignal.SeatBoundHere => CasinoSeatStage.Seated,
            CasinoSeatSignal.SeatBoundElsewhere => CasinoSeatStage.Elsewhere,
            _ => CasinoSeatStage.Sitting,
        };
    }

    private static CasinoSeatStage FromSeated(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.StandRequested => CasinoSeatStage.Standing,
            CasinoSeatSignal.SeatBoundElsewhere => CasinoSeatStage.Elsewhere,
            _ => CasinoSeatStage.Seated,
        };
    }

    private static CasinoSeatStage FromElsewhere(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.TakeOverRequested => CasinoSeatStage.Claiming,
            CasinoSeatSignal.SeatBoundHere => CasinoSeatStage.Seated,
            _ => CasinoSeatStage.Elsewhere,
        };
    }

    private static CasinoSeatStage FromClaiming(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.ClaimGranted => CasinoSeatStage.Seated,
            CasinoSeatSignal.ClaimRefused => CasinoSeatStage.Elsewhere,
            CasinoSeatSignal.SeatBoundHere => CasinoSeatStage.Seated,
            _ => CasinoSeatStage.Claiming,
        };
    }

    private static CasinoSeatStage FromStanding(CasinoSeatSignal signal)
    {
        return signal switch
        {
            CasinoSeatSignal.StandGranted => CasinoSeatStage.Watching,
            // A stand posted mid hand is accepted and queued, so the seat is still mine until the
            // hand it is waiting on settles. Treating that acceptance as a departure would take the
            // cards off the felt with money still on them.
            CasinoSeatSignal.StandQueued => CasinoSeatStage.Seated,
            CasinoSeatSignal.StandRefused => CasinoSeatStage.Seated,
            CasinoSeatSignal.SeatBoundElsewhere => CasinoSeatStage.Elsewhere,
            _ => CasinoSeatStage.Standing,
        };
    }
}
