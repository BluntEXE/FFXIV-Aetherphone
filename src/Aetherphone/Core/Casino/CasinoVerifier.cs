using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum CasinoRoundVerdict
{
    Unrevealed,
    Match,
    Mismatch,
}

// A client-side replay of the server fairness recipe in CasinoRng.cs: SHA256 of the revealed
// seed must equal the commit published before the stake was accepted, and every accepted draw
// in the log must fall out of an HMAC-SHA256 counter-mode stream keyed by that seed and bound
// to the round id, read as big-endian 32-bit words with exact rejection sampling. Each draw
// purpose names its own bound, so the replay consumes the stream exactly as the server did and
// a single flipped seed bit shifts every draw.
//
// One purpose name is not enough to fix a bound on its own. Both wheels log their draw as
// "segment" and they are different wheels: the wager rim has fifty segments and the free spin has
// sixteen. The game kind therefore rides into the replay alongside the log, because reading a
// sixteen segment draw against a fifty segment bound consumes the stream differently and would
// accuse the house of cheating on every honest daily spin.
internal static class CasinoVerifier
{
    private const string ScratchPrizePurpose = "prize";
    private const string BarkeepPatronsPurpose = "patrons";
    private const string SegmentPurpose = "segment";
    private const string BingoCardPurpose = "card";
    private const string BingoBallPurpose = "ball";
    private const uint BarkeepJitterBound = 3;
    private const uint BarkeepStepCountBound = 3;

    private static readonly uint ScratchWinnerBagSize = (ScratchRules.SymbolCount - 1) * 2;
    private static readonly uint ScratchLoserBagSize = ScratchRules.SymbolCount * 2;

    // A card is dealt column by column out of a fifteen deep pool that shrinks with every pick,
    // and the middle column takes four picks instead of five because the centre is free. The
    // twenty-four bounds that produces are the same for every card, so the pattern simply repeats.
    private static readonly uint[] BingoCardBounds =
    {
        15, 14, 13, 12, 11,
        15, 14, 13, 12, 11,
        15, 14, 13, 12,
        15, 14, 13, 12, 11,
        15, 14, 13, 12, 11,
    };

    public static CasinoRoundVerdict Verify(CasinoRoundVerifyDto round)
    {
        if (!round.Granted || round.State == CasinoRoundStates.Open || round.SeedRevealed.Length == 0)
        {
            return CasinoRoundVerdict.Unrevealed;
        }

        return Verify(round.GameKind, round.SeedRevealed, round.SeedCommitHash, round.RoundId, round.DrawLog);
    }

    public static CasinoRoundVerdict Verify(string gameKind, string seedHex, string seedCommitHash, string roundId,
        string drawLog)
    {
        byte[] seed;
        try
        {
            seed = Convert.FromHexString(seedHex);
        }
        catch (FormatException)
        {
            return CasinoRoundVerdict.Mismatch;
        }

        var computedCommit = Convert.ToHexStringLower(SHA256.HashData(seed));
        if (!string.Equals(computedCommit, seedCommitHash, StringComparison.OrdinalIgnoreCase))
        {
            return CasinoRoundVerdict.Mismatch;
        }

        return ReplaysDrawLog(gameKind, seed, roundId, drawLog)
            ? CasinoRoundVerdict.Match
            : CasinoRoundVerdict.Mismatch;
    }

    // Sixteen for the free spin, fifty for the wager rim, and nothing at all for a game kind this
    // client does not know: guessing one of the two would give a round the strongest verdict on a
    // coin flip, which is the one thing a verifier must never do.
    internal static bool TrySegmentBound(string gameKind, out uint bound)
    {
        if (string.Equals(gameKind, CasinoWire.DailySpinKind, StringComparison.Ordinal))
        {
            bound = DailySpinRules.SegmentCount;
            return true;
        }

        if (string.Equals(gameKind, CasinoWire.WheelKind, StringComparison.Ordinal))
        {
            bound = WheelRules.SegmentCount;
            return true;
        }

        bound = 0;
        return false;
    }

    // A revealed round that logs no draws is not evidence of fair play, it is the absence of it:
    // the commit alone proves a seed was published, never that this round fell out of it. The
    // verifier is the one component whose job is to distrust the server, so missing evidence
    // fails closed rather than earning the strongest verdict for free.
    internal static bool ReplaysDrawLog(string gameKind, byte[] seed, string roundId, string drawLog)
    {
        if (drawLog.Length == 0)
        {
            return false;
        }

        TrySegmentBound(gameKind, out var segmentBound);
        var stream = new DrawStream(seed, roundId);
        var shuffles = default(ShuffleRun);
        var cursor = 0;
        while (cursor < drawLog.Length)
        {
            var separator = drawLog.IndexOf(';', cursor);
            var end = separator < 0 ? drawLog.Length : separator;
            var pair = drawLog.AsSpan(cursor, end - cursor);
            var colon = pair.IndexOf(':');
            if (colon <= 0 || colon == pair.Length - 1)
            {
                return false;
            }

            var purpose = pair[..colon];
            if (!uint.TryParse(pair[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var loggedValue))
            {
                return false;
            }

            if (!TryBoundFor(purpose, shuffles.Next(purpose), segmentBound, out var bound)
                || loggedValue >= bound)
            {
                return false;
            }

            if (stream.NextBelow(bound) != loggedValue)
            {
                return false;
            }

            cursor = end + 1;
        }

        return true;
    }

    // The purpose vocabulary is the union of every game engine: slots stops s{spin}r{reel}, scratch
    // prize roll plus winner/loser shuffles w{pick}/g{cell}/l{pick}, the barkeep script draws
    // patrons/a{patron}/n{patron}/k{patron}.{step}, the segment both wheels draw, and the two bingo
    // shuffles. Every bound comes from the mirrored rules tables, so a new purpose on the wire fails
    // closed as a mismatch.
    //
    // Two purposes do not carry their bound on the label. The bingo shuffles narrow with every pick
    // and the engine logs each one under the same name, so the bound comes from how many draws of
    // that purpose the log has already spent, which is exactly the number the server had spent when
    // it made this one; a log that runs past the end of a shuffle has no honest bound left. The
    // segment is the other: two different wheels share the name, so its bound arrives from the
    // round's game kind and a zero there means no wheel this client knows, which fails closed.
    internal static bool TryBoundFor(ReadOnlySpan<char> purpose, uint segmentBound, out uint bound)
    {
        return TryBoundFor(purpose, 0, segmentBound, out bound);
    }

    internal static bool TryBoundFor(ReadOnlySpan<char> purpose, int occurrence, uint segmentBound, out uint bound)
    {
        bound = 0;
        if (occurrence < 0)
        {
            return false;
        }

        if (purpose.SequenceEqual(ScratchPrizePurpose))
        {
            bound = ScratchRules.TableScale;
            return true;
        }

        if (purpose.SequenceEqual(BarkeepPatronsPurpose))
        {
            bound = (uint)BarkeepRules.PatronBuckets.Length;
            return true;
        }

        if (purpose.SequenceEqual(SegmentPurpose))
        {
            bound = segmentBound;
            return segmentBound > 0;
        }

        if (purpose.SequenceEqual(BingoCardPurpose))
        {
            bound = BingoCardBounds[occurrence % BingoCardBounds.Length];
            return true;
        }

        if (purpose.SequenceEqual(BingoBallPurpose))
        {
            if (occurrence >= BingoRules.Balls - 1)
            {
                return false;
            }

            bound = (uint)(BingoRules.Balls - occurrence);
            return true;
        }

        if (purpose.Length < 2)
        {
            return false;
        }

        var marker = purpose[0];
        var argument = purpose[1..];
        switch (marker)
        {
            case 's':
            {
                var reelSplit = argument.IndexOf('r');
                if (reelSplit <= 0 || reelSplit == argument.Length - 1
                    || !TryParseIndex(argument[..reelSplit], out var spinIndex)
                    || !TryParseIndex(argument[(reelSplit + 1)..], out var reelIndex)
                    || spinIndex > SlotsRules.FreeSpinCap || reelIndex >= SlotsRules.ReelCount)
                {
                    return false;
                }

                bound = SlotsRules.StopsPerReel;
                return true;
            }
            case 'w':
            {
                if (!TryParseIndex(argument, out var pickIndex)
                    || pickIndex >= ScratchRules.CellCount - ScratchRules.MatchesToWin)
                {
                    return false;
                }

                bound = ScratchWinnerBagSize - (uint)pickIndex;
                return true;
            }
            case 'g':
            {
                if (!TryParseIndex(argument, out var cellIndex)
                    || cellIndex < 1 || cellIndex >= ScratchRules.CellCount)
                {
                    return false;
                }

                bound = (uint)cellIndex + 1;
                return true;
            }
            case 'l':
            {
                if (!TryParseIndex(argument, out var pickIndex) || pickIndex >= ScratchRules.CellCount)
                {
                    return false;
                }

                bound = ScratchLoserBagSize - (uint)pickIndex;
                return true;
            }
            case 'a':
            {
                if (!TryParseIndex(argument, out var patronIndex) || patronIndex >= BarkeepRules.MaxPatrons)
                {
                    return false;
                }

                bound = BarkeepJitterBound;
                return true;
            }
            case 'n':
            {
                if (!TryParseIndex(argument, out var patronIndex) || patronIndex >= BarkeepRules.MaxPatrons)
                {
                    return false;
                }

                bound = BarkeepStepCountBound;
                return true;
            }
            case 'k':
            {
                var stepSplit = argument.IndexOf('.');
                if (stepSplit <= 0 || stepSplit == argument.Length - 1
                    || !TryParseIndex(argument[..stepSplit], out var patronIndex)
                    || !TryParseIndex(argument[(stepSplit + 1)..], out var stepIndex)
                    || patronIndex >= BarkeepRules.MaxPatrons || stepIndex >= BarkeepRules.StepKindCount)
                {
                    return false;
                }

                bound = BarkeepRules.StepKindCount;
                return true;
            }
            default:
                return false;
        }
    }

    private static bool TryParseIndex(ReadOnlySpan<char> text, out int value)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private struct ShuffleRun
    {
        private int cards;

        private int balls;

        public int Next(ReadOnlySpan<char> purpose)
        {
            if (purpose.SequenceEqual(BingoCardPurpose))
            {
                var taken = cards;
                cards++;
                return taken;
            }

            if (purpose.SequenceEqual(BingoBallPurpose))
            {
                var taken = balls;
                balls++;
                return taken;
            }

            return 0;
        }
    }

    internal sealed class DrawStream
    {
        private const int BlockBytes = 32;
        private const int WordBytes = 4;
        private const ulong WordSpace = 0x1_0000_0000UL;

        private readonly byte[] streamKey;

        private readonly byte[] block = new byte[BlockBytes];

        private uint blockCounter;

        private int blockOffset = BlockBytes;

        public DrawStream(byte[] seed, string roundId)
        {
            streamKey = HMACSHA256.HashData(seed, Encoding.UTF8.GetBytes(roundId));
        }

        public uint NextBelow(uint bound)
        {
            var limit = WordSpace / bound * bound;
            while (true)
            {
                var raw = NextUInt32();
                if (raw >= limit)
                {
                    continue;
                }

                return raw % bound;
            }
        }

        private uint NextUInt32()
        {
            if (blockOffset > block.Length - WordBytes)
            {
                Span<byte> counter = stackalloc byte[WordBytes];
                BinaryPrimitives.WriteUInt32BigEndian(counter, blockCounter);
                blockCounter++;
                HMACSHA256.HashData(streamKey, counter, block);
                blockOffset = 0;
            }

            var value = BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(blockOffset, WordBytes));
            blockOffset += WordBytes;
            return value;
        }
    }
}
