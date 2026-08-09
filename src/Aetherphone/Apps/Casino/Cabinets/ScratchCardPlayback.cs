using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum ScratchPhase
{
    Idle,
    Scratching,
    Settled,
}

// The card's outcome is settled the moment the purchase response lands, but nothing outside
// this class may see it early: the prize amount stays sealed until the third matching symbol
// is on the table, the winning-cell highlight stays sealed with it, and the settled stack is
// handed out exactly once when every cell is revealed. The theater can never leak what the
// foil still hides.
internal sealed class ScratchCardPlayback
{
    private readonly int[] cells = new int[ScratchRules.CellCount];
    private readonly float[] foil = new float[ScratchRules.CellCount];
    private readonly bool[] revealed = new bool[ScratchRules.CellCount];

    private ScratchPhase phase;
    private int tier;
    private long sealedPrize;
    private long sealedStack;
    private int sealedWinningSymbol;
    private int revealedMatches;
    private bool winCelebrationPending;
    private bool stackCommitPending;

    public ScratchPhase Phase => phase;

    public int Tier => tier;

    public bool RevealComplete => phase == ScratchPhase.Settled;

    public long PrizeOnceRevealed => MatchOnTheTable ? sealedPrize : 0;

    public int WinningSymbolOnceMatched => MatchOnTheTable ? sealedWinningSymbol : -1;

    private bool MatchOnTheTable => revealedMatches >= ScratchRules.MatchesToWin;

    public bool Begin(CasinoScratchCardDto card)
    {
        var incoming = card.Cells;
        if (!card.Granted || incoming is null || !ScratchRules.AreValidCells(incoming))
        {
            return false;
        }

        var winningSymbol = ScratchRules.WinningSymbol(incoming);
        var consistent = card.Prize > 0 ? winningSymbol >= 0 : winningSymbol < 0;
        if (!consistent)
        {
            return false;
        }

        Array.Copy(incoming, cells, ScratchRules.CellCount);
        for (var cellIndex = 0; cellIndex < ScratchRules.CellCount; cellIndex++)
        {
            foil[cellIndex] = 1f;
            revealed[cellIndex] = false;
        }

        phase = ScratchPhase.Scratching;
        tier = ScratchRules.IsValidTier(card.Tier) ? card.Tier : 0;
        sealedPrize = card.Prize;
        sealedStack = card.Stack;
        sealedWinningSymbol = winningSymbol;
        revealedMatches = 0;
        winCelebrationPending = false;
        stackCommitPending = false;
        return true;
    }

    public int CellSymbol(int cellIndex)
    {
        return cells[cellIndex];
    }

    public bool IsRevealed(int cellIndex)
    {
        return revealed[cellIndex];
    }

    public bool IsMatchedCell(int cellIndex)
    {
        return MatchOnTheTable && revealed[cellIndex] && cells[cellIndex] == sealedWinningSymbol;
    }

    public float FoilRemaining(int cellIndex)
    {
        return foil[cellIndex];
    }

    public void Rub(int cellIndex, float amount)
    {
        if (phase != ScratchPhase.Scratching || revealed[cellIndex] || amount <= 0f)
        {
            return;
        }

        foil[cellIndex] -= amount;
        if (foil[cellIndex] > 0f)
        {
            return;
        }

        foil[cellIndex] = 0f;
        Reveal(cellIndex);
    }

    public void RevealAll()
    {
        if (phase != ScratchPhase.Scratching)
        {
            return;
        }

        for (var cellIndex = 0; cellIndex < ScratchRules.CellCount; cellIndex++)
        {
            if (revealed[cellIndex])
            {
                continue;
            }

            foil[cellIndex] = 0f;
            Reveal(cellIndex);
        }
    }

    public bool TakeWinCelebration(out long prize)
    {
        if (!winCelebrationPending)
        {
            prize = 0;
            return false;
        }

        winCelebrationPending = false;
        prize = sealedPrize;
        return true;
    }

    public bool TakeStackCommit(out long stack)
    {
        if (!stackCommitPending)
        {
            stack = 0;
            return false;
        }

        stackCommitPending = false;
        stack = sealedStack;
        return true;
    }

    public void Clear()
    {
        phase = ScratchPhase.Idle;
        sealedPrize = 0;
        sealedStack = 0;
        sealedWinningSymbol = -1;
        revealedMatches = 0;
        winCelebrationPending = false;
        stackCommitPending = false;
        for (var cellIndex = 0; cellIndex < ScratchRules.CellCount; cellIndex++)
        {
            foil[cellIndex] = 1f;
            revealed[cellIndex] = false;
        }
    }

    private void Reveal(int cellIndex)
    {
        revealed[cellIndex] = true;
        if (cells[cellIndex] == sealedWinningSymbol && sealedWinningSymbol >= 0)
        {
            revealedMatches++;
            if (revealedMatches == ScratchRules.MatchesToWin && sealedPrize > 0)
            {
                winCelebrationPending = true;
            }
        }

        for (var checkIndex = 0; checkIndex < ScratchRules.CellCount; checkIndex++)
        {
            if (!revealed[checkIndex])
            {
                return;
            }
        }

        phase = ScratchPhase.Settled;
        stackCommitPending = true;
    }
}
