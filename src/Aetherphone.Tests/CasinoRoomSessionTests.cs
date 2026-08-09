using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoRoomSessionTests
{
    private const string Room = "wheel-1";
    private const long Now = 1_754_784_000_000;

    [Fact]
    public void EnteringARoomAsksTheOneSocketToAttach()
    {
        var session = Session(out var sent);

        session.Enter(Room);

        Assert.Single(sent);
        Assert.Equal(SignalType.CasinoAttach, sent[0].Type);
        Assert.NotNull(sent[0].Casino);
        Assert.Equal(Room, sent[0].Casino!.RoomId);
        Assert.Equal(Room, session.RoomId);
        Assert.True(session.AwaitingSnapshot);
        Assert.False(session.Attached);
    }

    [Fact]
    public void ReEnteringTheRoomAlreadyInPlayDoesNotAttachTwice()
    {
        var session = Session(out var sent);

        session.Enter(Room);
        session.Enter(Room);

        Assert.Single(sent);
    }

    [Fact]
    public void LeavingDetachesAndDropsTheHeldRoom()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Leave();

        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoDetach, sent[1].Type);
        Assert.Equal(Room, sent[1].Casino!.RoomId);
        Assert.Equal(string.Empty, session.RoomId);
        Assert.Null(session.State);
    }

    [Fact]
    public void AttachingBaselinesTheEpochAndSeq()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.Receive(Attached(epoch: 3, seq: 41), Now);

        var state = session.State;
        Assert.NotNull(state);
        Assert.Equal(3, state.Epoch);
        Assert.Equal(41, state.Seq);
        Assert.True(session.Attached);
        Assert.False(session.AwaitingSnapshot);
    }

    [Fact]
    public void FramesForAnotherRoomNeverTouchTheRoomInPlay()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(new CasinoSignal(SignalType.CasinoEvent, null, new CasinoPayload
        {
            RoomId = "bingo-9",
            Epoch = 1,
            Seq = 6,
            Event = new CasinoRoomEventDto(PlayerCount: 99),
        }), Now);

        Assert.Equal(4, session.State!.Snapshot.PlayerCount);
    }

    [Fact]
    public void TheNextSeqApplies()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 6, new CasinoRoomEventDto(PlayerCount: 7, StakeTotal: 250)), Now);

        var state = session.State;
        Assert.Equal(6, state!.Seq);
        Assert.Equal(7, state.Snapshot.PlayerCount);
        Assert.Equal(250, state.Snapshot.StakeTotal);
        Assert.Single(sent);
    }

    [Fact]
    public void AnEventLeavesEveryFieldItDoesNotCarryAlone()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 6, new CasinoRoomEventDto(PlayerCount: 9)), Now);

        var snapshot = session.State!.Snapshot;
        Assert.Equal(9, snapshot.PlayerCount);
        Assert.Equal(120, snapshot.StakeTotal);
        Assert.Equal("round-1", snapshot.RoundId);
        Assert.Equal(2, snapshot.Phase);
        Assert.Equal(Now + 20_000, snapshot.PhaseEndsAtUnixMs);
    }

    [Fact]
    public void ADuplicateOrOlderSeqIsDropped()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 5, new CasinoRoomEventDto(PlayerCount: 77)), Now);
        session.Receive(Event(epoch: 1, seq: 2, new CasinoRoomEventDto(PlayerCount: 88)), Now);

        Assert.Equal(4, session.State!.Snapshot.PlayerCount);
        Assert.Equal(5, session.State!.Seq);
        Assert.Single(sent);
    }

    [Fact]
    public void AFrameFromAnOlderEpochIsIgnoredWithoutAskingForAnything()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 4, seq: 5), Now);

        session.Receive(Event(epoch: 3, seq: 6, new CasinoRoomEventDto(PlayerCount: 77)), Now);

        Assert.Equal(4, session.State!.Snapshot.PlayerCount);
        Assert.Equal(4, session.State!.Epoch);
        Assert.Single(sent);
        Assert.False(session.AwaitingSnapshot);
    }

    [Fact]
    public void AFrameFromANewerEpochIsNeverApplied()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 2, seq: 1006, new CasinoRoomEventDto(PlayerCount: 77)), Now);

        Assert.Equal(4, session.State!.Snapshot.PlayerCount);
        Assert.Equal(1, session.State!.Epoch);
        Assert.True(session.AwaitingSnapshot);
        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoResync, sent[1].Type);
    }

    [Fact]
    public void AGapAsksForExactlyOneResyncInsideTheRateLimit()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 9, new CasinoRoomEventDto(PlayerCount: 6)), Now);
        session.Receive(Event(epoch: 1, seq: 10, new CasinoRoomEventDto(PlayerCount: 7)), Now + 500);
        session.Receive(Event(epoch: 1, seq: 11, new CasinoRoomEventDto(PlayerCount: 8)), Now + 1_999);

        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoResync, sent[1].Type);
        Assert.Equal(Room, sent[1].Casino!.RoomId);
        Assert.Equal(4, session.State!.Snapshot.PlayerCount);
        Assert.True(session.AwaitingSnapshot);
    }

    [Fact]
    public void AGapThatOutlivesTheRateLimitAsksAgain()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 9, new CasinoRoomEventDto(PlayerCount: 6)), Now);
        session.Receive(Event(epoch: 1, seq: 10, new CasinoRoomEventDto(PlayerCount: 7)), Now + 2_000);

        Assert.Equal(3, sent.Count);
        Assert.Equal(SignalType.CasinoResync, sent[1].Type);
        Assert.Equal(SignalType.CasinoResync, sent[2].Type);
    }

    [Fact]
    public void TheSnapshotSupersedesEveryFrameThatArrivedDuringTheGap()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);
        session.Receive(Event(epoch: 1, seq: 9, new CasinoRoomEventDto(PlayerCount: 6)), Now);
        session.Receive(Event(epoch: 1, seq: 10, new CasinoRoomEventDto(PlayerCount: 7)), Now);

        session.Receive(Snapshot(epoch: 1, seq: 12, playerCount: 31), Now);
        session.Receive(Event(epoch: 1, seq: 11, new CasinoRoomEventDto(PlayerCount: 99)), Now);
        session.Receive(Event(epoch: 1, seq: 13, new CasinoRoomEventDto(PlayerCount: 32)), Now);

        var state = session.State;
        Assert.Equal(13, state!.Seq);
        Assert.Equal(32, state.Snapshot.PlayerCount);
        Assert.False(session.AwaitingSnapshot);
    }

    [Fact]
    public void ASnapshotFromANewerEpochLandsEvenWhenItsSeqRewinds()
    {
        var held = Held(epoch: 1, seq: 900);

        Assert.True(CasinoRoomSession.AcceptsSnapshot(held, epoch: 2, seq: 1));
        Assert.False(CasinoRoomSession.AcceptsSnapshot(held, epoch: 0, seq: 5_000));
        Assert.True(CasinoRoomSession.AcceptsSnapshot(held, epoch: 1, seq: 900));
        Assert.False(CasinoRoomSession.AcceptsSnapshot(held, epoch: 1, seq: 899));
        Assert.True(CasinoRoomSession.AcceptsSnapshot(null, epoch: 0, seq: 0));
    }

    [Fact]
    public void TheApplicationMatrixDecidesEveryFrameTheSameWay()
    {
        var held = Held(epoch: 2, seq: 10);

        Assert.Equal(CasinoRoomApply.Resync, CasinoRoomSession.Decide(null, 2, 11));
        Assert.Equal(CasinoRoomApply.Ignore, CasinoRoomSession.Decide(held, 1, 11));
        Assert.Equal(CasinoRoomApply.Resync, CasinoRoomSession.Decide(held, 3, 11));
        Assert.Equal(CasinoRoomApply.Ignore, CasinoRoomSession.Decide(held, 2, 10));
        Assert.Equal(CasinoRoomApply.Ignore, CasinoRoomSession.Decide(held, 2, 9));
        Assert.Equal(CasinoRoomApply.Apply, CasinoRoomSession.Decide(held, 2, 11));
        Assert.Equal(CasinoRoomApply.Resync, CasinoRoomSession.Decide(held, 2, 12));
    }

    [Fact]
    public void DrawsAccumulateInsideARoundAndStartOverWhenTheRoundMoves()
    {
        Assert.Equal(new[] { 4, 9 }, CasinoRoomSession.MergedNumbers(new[] { 4 }, new[] { 9 }, roundChanged: false));
        Assert.Equal(new[] { 9 }, CasinoRoomSession.MergedNumbers(new[] { 4 }, new[] { 9 }, roundChanged: true));
        Assert.Equal(new[] { 4 }, CasinoRoomSession.MergedNumbers(new[] { 4 }, null, roundChanged: false));
        Assert.Null(CasinoRoomSession.MergedNumbers(new[] { 4 }, null, roundChanged: true));
    }

    [Fact]
    public void ANewRoundDropsTheEntriesTheOldRoundPaidFor()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);
        Assert.NotNull(session.State!.Private);

        session.Receive(Event(epoch: 1, seq: 6, new CasinoRoomEventDto(RoundId: "round-2", Numbers: new[] { 17 })), Now);

        var state = session.State;
        Assert.Null(state!.Private);
        Assert.Equal("round-2", state.Snapshot.RoundId);
        Assert.Equal(new[] { 17 }, state.Snapshot.Numbers);
    }

    [Fact]
    public void ADeclineClosesTheRoomAndStopsTheAttachLoop()
    {
        var session = Session(out var sent);
        session.Enter(Room);

        session.Receive(new CasinoSignal(SignalType.CasinoDeclined, "draining", new CasinoPayload { RoomId = Room }),
            Now);

        Assert.Equal("draining", session.ClosedReason);
        Assert.Equal(string.Empty, session.RoomId);
        Assert.Null(session.State);

        session.OnRealtimeConnected(true);

        Assert.Single(sent);
    }

    [Fact]
    public void AReconnectReattachesTheRoomStillInPlay()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.OnRealtimeConnected(false);
        Assert.False(session.Attached);
        Assert.NotNull(session.State);

        session.OnRealtimeConnected(true);

        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoAttach, sent[1].Type);
        Assert.Equal(Room, sent[1].Casino!.RoomId);
    }

    [Fact]
    public void ThePollingPathFillsTheRoomUnderTheSameVersionRules()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.AbsorbHttpState(Room, new CasinoRoomStateDto(
            Granted: true,
            Epoch: 2,
            Seq: 30,
            ServerNowUnixMs: Now,
            Snapshot: new CasinoRoomSnapshotDto(RoomId: Room, PlayerCount: 12)), Now);

        Assert.Equal(30, session.State!.Seq);
        Assert.Equal(12, session.State!.Snapshot.PlayerCount);
        Assert.False(session.AwaitingSnapshot);

        session.AbsorbHttpState(Room, new CasinoRoomStateDto(
            Granted: true,
            Epoch: 1,
            Seq: 900,
            ServerNowUnixMs: Now,
            Snapshot: new CasinoRoomSnapshotDto(RoomId: Room, PlayerCount: 99)), Now);

        Assert.Equal(12, session.State!.Snapshot.PlayerCount);
    }

    [Fact]
    public void APolledDenialClosesTheRoomToo()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.AbsorbHttpState(Room, new CasinoRoomStateDto(Granted: false, Reason: "room_closed"), Now);

        Assert.Equal("room_closed", session.ClosedReason);
        Assert.Equal(string.Empty, session.RoomId);
    }

    [Fact]
    public void TheFirstServerStampAnchorsTheSkewAndTheRestCrawl()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.Receive(Attached(epoch: 1, seq: 5, serverNowUnixMs: Now + 400), Now);
        Assert.Equal(400, session.SkewMilliseconds);

        session.Receive(Event(epoch: 1, seq: 6, new CasinoRoomEventDto(PlayerCount: 5), Now + 800), Now);
        Assert.Equal(500, session.SkewMilliseconds);
    }

    [Fact]
    public void AClockThatJumpsSnapsInsteadOfCrawling()
    {
        Assert.Equal(100, CasinoRoomSession.SmoothedSkew(0, 400));
        Assert.Equal(-100, CasinoRoomSession.SmoothedSkew(0, -400));
        Assert.Equal(1_249, CasinoRoomSession.SmoothedSkew(0, 4_999));
        Assert.Equal(5_000, CasinoRoomSession.SmoothedSkew(0, 5_000));
        Assert.Equal(90_000, CasinoRoomSession.SmoothedSkew(120, 90_000));
        Assert.Equal(-90_000, CasinoRoomSession.SmoothedSkew(120, -90_000));
    }

    [Fact]
    public void CountdownsRenderFromTheServerClockAndNeverGoNegative()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5, serverNowUnixMs: Now + 3_000), Now);

        Assert.Equal(Now + 3_000, session.ServerNowUnixMs(Now));
        Assert.Equal(17_000, session.RemainingMilliseconds(Now + 20_000, Now));
        Assert.Equal(0, session.RemainingMilliseconds(Now + 1_000, Now));
        Assert.Equal(0, session.RemainingMilliseconds(0, Now));
    }

    [Fact]
    public void ASocketThatIsNotUpNeverSwallowsTheAttach()
    {
        var signals = new RealtimeSignalBus();
        var sent = new List<CallControl>();
        signals.BindSender(sent.Add);
        var session = new CasinoRoomSession(signals);

        session.Enter(Room);

        Assert.Empty(sent);
        Assert.Equal(Room, session.RoomId);
    }

    private static CasinoRoomSession Session(out List<CallControl> sent)
    {
        var captured = new List<CallControl>();
        var signals = new RealtimeSignalBus();
        signals.SetActive(true);
        signals.BindSender(captured.Add);
        sent = captured;
        return new CasinoRoomSession(signals);
    }

    private static CasinoRoomState Held(int epoch, long seq)
    {
        return new CasinoRoomState(Room, epoch, seq, new CasinoRoomSnapshotDto(RoomId: Room), null);
    }

    private static CasinoSignal Attached(int epoch, long seq, long serverNowUnixMs = 0)
    {
        return new CasinoSignal(SignalType.CasinoAttached, null, new CasinoPayload
        {
            RoomId = Room,
            Epoch = epoch,
            Seq = seq,
            ServerNowUnixMs = serverNowUnixMs,
            Snapshot = new CasinoRoomSnapshotDto(
                RoomId: Room,
                GameKind: "casino.wheel",
                Phase: 2,
                RoundId: "round-1",
                PhaseEndsAtUnixMs: Now + 20_000,
                PlayerCount: 4,
                EntryCount: 3,
                StakeTotal: 120),
            Private = new CasinoRoomPrivateDto("round-1", 20,
                new[] { new CasinoRoomEntryDto("entry-1", 1, 20, 7) }),
        });
    }

    private static CasinoSignal Snapshot(int epoch, long seq, int playerCount)
    {
        return new CasinoSignal(SignalType.CasinoSnapshot, null, new CasinoPayload
        {
            RoomId = Room,
            Epoch = epoch,
            Seq = seq,
            Snapshot = new CasinoRoomSnapshotDto(RoomId: Room, RoundId: "round-1", PlayerCount: playerCount),
        });
    }

    private static CasinoSignal Event(int epoch, long seq, CasinoRoomEventDto change, long serverNowUnixMs = 0)
    {
        return new CasinoSignal(SignalType.CasinoEvent, null, new CasinoPayload
        {
            RoomId = Room,
            Epoch = epoch,
            Seq = seq,
            EventKind = "wheel.lock",
            ServerNowUnixMs = serverNowUnixMs,
            Event = change,
        });
    }
}
