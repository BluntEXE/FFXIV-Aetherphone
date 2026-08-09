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
    private const uint BarkeepJitterBound = 3;
    private const uint BarkeepStepCountBound = 3;

    private static readonly uint ScratchWinnerBagSize = (ScratchRules.SymbolCount - 1) * 2;
    private static readonly uint ScratchLoserBagSize = ScratchRules.SymbolCount * 2;

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

    internal static bool ReplaysDrawLog(byte[] seed, string roundId, string drawLog)
    {
        if (drawLog.Length == 0)
        {
            return true;
        }

        var stream = new DrawStream(seed, roundId);
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

            if (!TryBoundFor(purpose, out var bound) || loggedValue >= bound)
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

    // The purpose vocabulary is the union of the three game engines: slots stops s{spin}r{reel},
    // scratch prize roll plus winner/loser shuffles w{pick}/g{cell}/l{pick}, and the barkeep
    // script draws patrons/a{patron}/n{patron}/k{patron}.{step}. Every bound comes from the
    // mirrored rules tables, so a new purpose on the wire fails closed as a mismatch.
    internal static bool TryBoundFor(ReadOnlySpan<char> purpose, out uint bound)
    {
        bound = 0;
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
