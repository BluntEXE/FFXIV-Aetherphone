using System;
using System.Collections.Generic;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

// The reason table is the client's whole vocabulary for refusal, and a gap in it is invisible until
// a player is looking at "that did not go through" while the server is saying something specific and
// helpful. These pin the mapping as total in both directions: every constant has a message, no two
// constants share one, and no message is a placeholder.
public sealed class CasinoReasonCoverageTests
{
    [Fact]
    public void EveryReasonTheServerCanSendHasItsOwnMessage()
    {
        for (var index = 0; index < CasinoReasons.All.Length; index++)
        {
            var reason = CasinoReasons.All[index];
            Assert.True(CasinoReasons.TryMessage(reason, out var message), reason);
            Assert.NotEqual(L.Casino.ReasonGeneric.Key, message.Key);
            Assert.False(string.IsNullOrWhiteSpace(message.Source), reason);
        }
    }

    [Fact]
    public void NoTwoReasonsShareOneMessage()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < CasinoReasons.All.Length; index++)
        {
            var message = CasinoReasons.MessageFor(CasinoReasons.All[index]);
            Assert.True(seen.Add(message.Key), CasinoReasons.All[index]);
        }
    }

    [Fact]
    public void EveryReasonAppearsExactlyOnceInTheList()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < CasinoReasons.All.Length; index++)
        {
            Assert.True(seen.Add(CasinoReasons.All[index]), CasinoReasons.All[index]);
        }
    }

    [Fact]
    public void TheTableVocabularyIsInTheList()
    {
        Assert.Contains(CasinoReasons.Full, CasinoReasons.All);
        Assert.Contains(CasinoReasons.InviteOnly, CasinoReasons.All);
        Assert.Contains(CasinoReasons.KnockPending, CasinoReasons.All);
        Assert.Contains(CasinoReasons.SeatTaken, CasinoReasons.All);
        Assert.Contains(CasinoReasons.AlreadySeated, CasinoReasons.All);
        Assert.Contains(CasinoReasons.BoundElsewhere, CasinoReasons.All);
        Assert.Contains(CasinoReasons.AtHandEnd, CasinoReasons.All);
        Assert.Contains(CasinoReasons.Kicked, CasinoReasons.All);
    }

    [Fact]
    public void AnUnknownReasonStillSaysSomethingRatherThanNothing()
    {
        Assert.Equal(L.Casino.ReasonGeneric.Key, CasinoReasons.MessageFor("a_reason_from_the_future").Key);
        Assert.Equal(L.Casino.ReasonGeneric.Key, CasinoReasons.MessageFor(string.Empty).Key);
        Assert.False(CasinoReasons.TryMessage("a_reason_from_the_future", out _));
    }

    // An empty refusal is the shape a dropped response has, and painting it as a silent success is
    // exactly the bug the store's naming exists to stop.
    [Fact]
    public void AnEmptyReasonBecomesTheUnreachableOne()
    {
        Assert.Equal(CasinoReasons.Unreachable, CasinoTablesStore.Named(string.Empty));
        Assert.Equal(CasinoReasons.SeatTaken, CasinoTablesStore.Named(CasinoReasons.SeatTaken));
    }
}
