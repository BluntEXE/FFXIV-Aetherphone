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
internal static class CasinoVerifier
{
    private const string ScratchPrizePurpose = "prize";
    private const string BarkeepPatronsPurpose = "patrons";
    private const string WheelSegmentPurpose = "segment";
    private const string DailySpinPurpose = "spin";
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

        return Verify(round.SeedRevealed, round.SeedCommitHash, round.RoundId, round.DrawLog);
    }

    public static CasinoRoundVerdict Verify(string seedHex, string seedCommitHash, string roundId, string drawLog)
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

        return ReplaysDrawLog(seed, roundId, drawLog) ? CasinoRoundVerdict.Match : CasinoRoundVerdict.Mismatch;
    }

    // A revealed round that logs no draws is not evidence of fair play, it is the absence of it:
    // the commit alone proves a seed was published, never that this round fell out of it. The
    // verifier is the one component whose job is to distrust the server, so missing evidence
    // fails closed rather than earning the strongest verdict for free.
    internal static bool ReplaysDrawLog(byte[] seed, string roundId, string drawLog)
    {
        if (drawLog.Length == 0)
        {
            return false;
        }

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

            if (!TryBoundFor(purpose, shuffles.Next(purpose), out var bound) || loggedValue >= bound)
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
    // patrons/a{patron}/n{patron}/k{patron}.{step}, the wheel segment, the daily spin segment, and
    // the two bingo shuffles. Every bound comes from the mirrored rules tables, so a new purpose on
    // the wire fails closed as a mismatch.
    //
    // The bingo shuffles are the only purposes whose bound is not written on the label: a Fisher
    // Yates pass narrows its range with every pick, and the engine logs each one under the same
    // name. The bound therefore comes from how many draws of that purpose the log has already
    // spent, which is exactly the number the server had spent when it made this one. A log that
    // runs past the end of a shuffle has no honest bound left and fails closed.
    internal static bool TryBoundFor(ReadOnlySpan<char> purpose, out uint bound)
    {
        return TryBoundFor(purpose, 0, out bound);
    }

    internal static bool TryBoundFor(ReadOnlySpan<char> purpose, int occurrence, out uint bound)
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

        if (purpose.SequenceEqual(WheelSegmentPurpose))
        {
            bound = WheelRules.SegmentCount;
            return true;
        }

        if (purpose.SequenceEqual(DailySpinPurpose))
        {
            bound = DailySpinRules.SegmentCount;
            return true;
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
