using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DailySpinTests
{
    [Fact]
    public void TheWheelHasSixteenSegmentsAndEveryOneOfThemPays()
    {
        Assert.Equal(16, DailySpinRules.SegmentCount);
        Assert.Equal(DailySpinRules.SegmentCount, DailySpinRules.Awards.Length);
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            Assert.True(DailySpinRules.AwardOf(segment) > 0);
        }
    }

    [Fact]
    public void TheTableSumsToItsPublishedExpectation()
    {
        var total = 0L;
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            total += DailySpinRules.Awards[segment];
        }

        Assert.Equal(DailySpinRules.TotalAward, total);
        Assert.Equal(DailySpinRules.ExpectedAward, total / DailySpinRules.SegmentCount);
        Assert.Equal(12, DailySpinRules.ExpectedAward);
    }

    // Every other wedge is the small award, which is what keeps a sixteen segment rim readable and
    // stops the good wedges from clumping. The two best sit a half turn apart.
    [Fact]
    public void TheRimAlternatesSoNoTwoGoodWedgesTouch()
    {
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment += 2)
        {
            Assert.Equal(5, DailySpinRules.AwardOf(segment));
        }

        var top = -1;
        var second = -1;
        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            if (DailySpinRules.AwardOf(segment) == DailySpinRules.TopAward)
            {
                top = segment;
            }

            if (DailySpinRules.AwardOf(segment) == 24)
            {
                second = segment;
            }
        }

        Assert.Equal(15, top);
        Assert.Equal(7, second);
        Assert.True(DailySpinRules.IsTopAward(top));
        Assert.False(DailySpinRules.IsTopAward(second));
    }

    [Fact]
    public void ASegmentOutsideTheRimPaysNothing()
    {
        Assert.False(DailySpinRules.IsSegment(-1));
        Assert.False(DailySpinRules.IsSegment(DailySpinRules.SegmentCount));
        Assert.Equal(0, DailySpinRules.AwardOf(-1));
        Assert.Equal(0, DailySpinRules.AwardOf(DailySpinRules.SegmentCount));
    }

    [Fact]
    public void AStateThatHasNotLoadedYetIsUnknownRatherThanUnavailable()
    {
        Assert.Equal(DailySpinClaim.Unknown, DailySpinStatus.Of(false, null));
        Assert.Equal(DailySpinClaim.Unknown, DailySpinStatus.Of(true, null));
        Assert.Equal(DailySpinClaim.Unknown,
            DailySpinStatus.Of(false, new CasinoDailySpinStateDto(Available: true)));
    }

    [Fact]
    public void AnAvailableSpinOffersItself()
    {
        var state = new CasinoDailySpinStateDto(Available: true, NextClaimAtUnix: 1_770_000_000);
        Assert.Equal(DailySpinClaim.Available, DailySpinStatus.Of(true, state));
        Assert.True(DailySpinStatus.CanClaim(true, state, false));
        Assert.False(DailySpinStatus.CanClaim(true, state, true));
        Assert.False(DailySpinStatus.ShowsReset(DailySpinClaim.Available));
    }

    [Fact]
    public void ASpinAlreadyTakenReadsAsClaimedAndShowsItsReset()
    {
        var state = new CasinoDailySpinStateDto(
            Available: false,
            Reason: CasinoReasons.Claimed,
            NextClaimAtUnix: 1_770_000_000,
            Segment: 7,
            Amount: 24);
        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(true, state));
        Assert.False(DailySpinStatus.CanClaim(true, state, false));
        Assert.True(DailySpinStatus.ShowsReset(DailySpinClaim.Claimed));
    }

    // An unavailable spin with no explanation must not render as a dead button. The only benign
    // reading is that today's turn is spent, so that is what the card says.
    [Fact]
    public void AnUnexplainedRefusalFallsBackToClaimedRatherThanADeadPill()
    {
        var state = new CasinoDailySpinStateDto(Available: false, NextClaimAtUnix: 1_770_000_000);
        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(true, state));
        Assert.False(DailySpinStatus.CanClaim(true, state, false));
    }

    [Fact]
    public void EveryDenialTheServerCanSendHasWarmWordsOfItsOwn()
    {
        var reasons = new[]
        {
            CasinoReasons.Frozen,
            CasinoReasons.Paused,
            CasinoReasons.DailyCap,
            CasinoReasons.Claimed,
            CasinoReasons.Cooldown,
            CasinoReasons.Unavailable,
        };

        for (var index = 0; index < reasons.Length; index++)
        {
            var state = new CasinoDailySpinStateDto(Available: false, Reason: reasons[index]);
            var expected = string.Equals(reasons[index], CasinoReasons.Claimed, StringComparison.Ordinal)
                ? DailySpinClaim.Claimed
                : DailySpinClaim.Denied;
            Assert.Equal(expected, DailySpinStatus.Of(true, state));
            Assert.True(CasinoReasons.TryMessage(reasons[index], out var message), reasons[index]);
            Assert.NotEqual(Aetherphone.Core.Localization.L.Casino.ReasonGeneric.Key, message.Key);
        }

        Assert.Equal("claimed", CasinoReasons.Claimed);
        Assert.Equal("paused", CasinoReasons.Paused);
        Assert.Equal("daily_cap", CasinoReasons.DailyCap);
    }

    [Fact]
    public void TheSpinRidesItsOwnEndpointAndNeedsNoSitting()
    {
        Assert.Equal("/casino/spin", CasinoClient.DailySpinPath);
        Assert.Equal("casino.dailyspin", CasinoWire.DailySpinKind);
        Assert.Equal("casino.daily", CasinoLedgerRules.Daily);
    }

    // Coins minted by the spin are a real earn, so unlike buy-ins and cash-outs they are never
    // swallowed by the casino ledger's quiet rule.
    [Fact]
    public void TheSpinsCoinsAreCelebratedWhileTheRestOfTheCasinoLedgerStaysQuiet()
    {
        Assert.False(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.Daily));
        Assert.True(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.BuyIn));
        Assert.True(CasinoLedgerRules.SkipsEarnCelebration(CasinoLedgerRules.CashOut));
    }

    [Fact]
    public void TheClaimRequestSerializesTheBackendShape()
    {
        var request = new CasinoDailySpinRequest("spin1");
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.CasinoDailySpinRequest);
        Assert.Equal("{\"clientRoundId\":\"spin1\"}", json);
    }

    [Fact]
    public void AGrantedClaimCarriesTheWalletBalanceAndTheFairnessChain()
    {
        const string json = "{\"granted\":true,\"reason\":\"\",\"roundId\":\"r9\",\"segment\":15,\"amount\":40,"
            + "\"balance\":1240,\"nextClaimAtUnix\":1770000000,\"seedCommitHash\":\"aa\",\"nextSeedHash\":\"bb\"}";
        var claim = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoDailySpinDto);

        Assert.NotNull(claim);
        Assert.True(claim.Granted);
        Assert.Equal("r9", claim.RoundId);
        Assert.Equal(15, claim.Segment);
        Assert.Equal(40, claim.Amount);
        Assert.Equal(1240, claim.Balance);
        Assert.Equal(DailySpinRules.TopAward, DailySpinRules.AwardOf(claim.Segment));
    }

    // The state endpoint and the claim answer must agree, so a granted claim is folded into the
    // held state directly rather than waiting for the next poll to say the same thing.
    [Fact]
    public void AGrantedClaimSettlesTheHeldStateWithoutAnotherRead()
    {
        var settled = CasinoSpinStore.Settled(new CasinoDailySpinDto(
            Granted: true,
            RoundId: "r9",
            Segment: 3,
            Amount: 20,
            Balance: 1220,
            NextClaimAtUnix: 1_770_000_000));

        Assert.False(settled.Available);
        Assert.Equal(CasinoReasons.Claimed, settled.Reason);
        Assert.Equal(3, settled.Segment);
        Assert.Equal(20, settled.Amount);
        Assert.Equal("r9", settled.RoundId);
        Assert.Equal(1_770_000_000, settled.NextClaimAtUnix);
        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(true, settled));
    }

    [Fact]
    public void ARefusedClaimKeepsTheReasonItNamedInsteadOfInventingOne()
    {
        var settled = CasinoSpinStore.Settled(new CasinoDailySpinDto(
            Granted: false,
            Reason: CasinoReasons.Frozen,
            NextClaimAtUnix: 1_770_000_000));

        Assert.False(settled.Available);
        Assert.Equal(CasinoReasons.Frozen, settled.Reason);
        Assert.Equal(-1, settled.Segment);
        Assert.Equal(DailySpinClaim.Denied, DailySpinStatus.Of(true, settled));

        var nameless = CasinoSpinStore.Settled(new CasinoDailySpinDto(Granted: false));
        Assert.Equal(CasinoReasons.Claimed, nameless.Reason);
        Assert.Equal(DailySpinClaim.Claimed, DailySpinStatus.Of(true, nameless));
    }

    // The rim is staged with the same choreography as the wager wheel, just sixteen wedges wide:
    // the sweep always lands the named segment under the pointer, whatever angle it started from.
    [Fact]
    public void TheSweepLandsTheServersSegmentUnderThePointer()
    {
        var span = Aetherphone.Apps.Casino.Cabinets.WheelChoreography.SpanFor(DailySpinRules.SegmentCount);
        Assert.Equal(Aetherphone.Apps.Casino.Cabinets.WheelChoreography.Tau / 16f, span, 5);

        for (var segment = 0; segment < DailySpinRules.SegmentCount; segment++)
        {
            var from = segment * 0.37f;
            var sweep = Aetherphone.Apps.Casino.Cabinets.WheelChoreography.SweepFor(from, segment,
                DailySpinRules.SegmentCount, 5);
            var landed = Aetherphone.Apps.Casino.Cabinets.WheelChoreography.Normalize(from + sweep);
            var rest = Aetherphone.Apps.Casino.Cabinets.WheelChoreography.RestAngleOf(segment,
                DailySpinRules.SegmentCount);
            Assert.True(MathF.Abs(landed - rest) < 0.0005f
                || MathF.Abs(MathF.Abs(landed - rest) - Aetherphone.Apps.Casino.Cabinets.WheelChoreography.Tau)
                    < 0.0005f);
            Assert.True(sweep >= 5 * Aetherphone.Apps.Casino.Cabinets.WheelChoreography.Tau);
        }
    }
}
