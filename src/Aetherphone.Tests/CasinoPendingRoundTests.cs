using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoPendingRoundTests
{
    private const ulong Character = 42;
    private const ulong OtherCharacter = 77;

    [Fact]
    public void RememberingARoundCopiesTheSnapshotAndKeepsOtherCharacters()
    {
        var snapshot = new Dictionary<ulong, PendingCasinoRound>
        {
            [OtherCharacter] = Pending("other-round"),
        };

        var next = CasinoPlayStore.RememberRound(snapshot, Character, Pending("round1"));

        Assert.NotNull(next);
        Assert.NotSame(snapshot, next);
        Assert.Equal(2, next.Count);
        Assert.Equal("round1", next[Character].RoundId);
        Assert.Equal("other-round", next[OtherCharacter].RoundId);
        Assert.False(snapshot.ContainsKey(Character));
    }

    [Fact]
    public void RememberingTheSameRoundAgainIsANoOp()
    {
        var snapshot = new Dictionary<ulong, PendingCasinoRound>
        {
            [Character] = Pending("round1"),
        };

        Assert.Null(CasinoPlayStore.RememberRound(snapshot, Character, Pending("round1")));
    }

    [Fact]
    public void TheRememberedIntentCarriesEverythingTheIdempotentReplayNeeds()
    {
        var pending = Pending("round1");
        var next = CasinoPlayStore.RememberRound(new Dictionary<ulong, PendingCasinoRound>(), Character, pending);

        Assert.NotNull(next);
        var recovered = next[Character];
        Assert.Equal("casino.slots", recovered.GameKind);
        Assert.Equal("sit1", recovered.SittingId);
        Assert.Equal("round1", recovered.RoundId);
        Assert.Equal(2, recovered.Stake);
    }

    [Fact]
    public void ClearingRemovesOnlyTheMatchingRound()
    {
        var snapshot = new Dictionary<ulong, PendingCasinoRound>
        {
            [Character] = Pending("round1"),
            [OtherCharacter] = Pending("other-round"),
        };

        var next = CasinoPlayStore.ClearRound(snapshot, Character, "round1");

        Assert.NotNull(next);
        Assert.NotSame(snapshot, next);
        Assert.False(next.ContainsKey(Character));
        Assert.Equal("other-round", next[OtherCharacter].RoundId);
        Assert.True(snapshot.ContainsKey(Character));
    }

    [Fact]
    public void ClearingADifferentRoundIdLeavesThePendingIntentAlone()
    {
        var snapshot = new Dictionary<ulong, PendingCasinoRound>
        {
            [Character] = Pending("round1"),
        };

        Assert.Null(CasinoPlayStore.ClearRound(snapshot, Character, "round2"));
        Assert.Null(CasinoPlayStore.ClearRound(snapshot, OtherCharacter, "round1"));
    }

    private static PendingCasinoRound Pending(string roundId)
    {
        return new PendingCasinoRound
        {
            GameKind = CasinoWire.SlotsKind,
            SittingId = "sit1",
            RoundId = roundId,
            Stake = 2,
        };
    }
}
