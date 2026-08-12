using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum SlotsPlaybackPhase
{
    Idle,
    Spinning,
    Presenting,
    BonusIntro,
    Finished,
}

internal readonly struct SlotsSpinView
{
    public readonly int[] Grid;
    public readonly CasinoSlotsLineWinDto[] LineWins;
    public readonly int ScatterCount;
    public readonly long Win;
    public readonly int SpinsAdded;

    public SlotsSpinView(int[] grid, CasinoSlotsLineWinDto[] lineWins, int scatterCount, long win, int spinsAdded)
    {
        Grid = grid;
        LineWins = lineWins;
        ScatterCount = scatterCount;
        Win = win;
        SpinsAdded = spinsAdded;
    }
}

internal sealed class SlotsRoundPlayback
{
    public const float LossPresentSeconds = 0.45f;
    public const float TurboLossPresentSeconds = 0.12f;
    public const float WinPresentSeconds = 1.5f;
    public const float BonusIntroSeconds = 1.3f;
    public const float FreeSpinFirstStopSeconds = 0.4f;
    public const float FreeSpinStopStaggerSeconds = 0.12f;
    public const long BigWinMultiple = 10;

    private static readonly CasinoSlotsLineWinDto[] NoLineWins = Array.Empty<CasinoSlotsLineWinDto>();

    private readonly SlotsChoreography choreography = new();
    private readonly List<SlotsSpinView> spins = new();

    private SlotsPlaybackPhase phase;
    private int spinIndex;
    private float phaseSeconds;
    private long stake;
    private long totalWin;
    private bool capApplied;
    private long committedWin;
    private long jackpot;
    private bool turbo;

    public bool Turbo
    {
        get => turbo;
        set => turbo = value;
    }

    public SlotsPlaybackPhase Phase => phase;

    public SlotsChoreography Choreography => choreography;

    public int SpinIndex => spinIndex;

    public int SpinCount => spins.Count;

    public int FreeSpinCount => spins.Count > 1 ? spins.Count - 1 : 0;

    public int BonusAwarded => spins.Count > 0 ? spins[0].SpinsAdded : 0;

    public bool InBonus => spinIndex > 0;

    public long Stake => stake;

    public long TotalWin => totalWin;

    public bool CapApplied => capApplied;

    public long CommittedWin => committedWin;

    public long Jackpot => jackpot;

    public bool JackpotLanded => jackpot > 0;

    public float PhaseSeconds => phaseSeconds;

    public bool HasSpins => spins.Count > 0;

    public bool SkipAvailable => phase == SlotsPlaybackPhase.BonusIntro
        || (spinIndex > 0 && (phase == SlotsPlaybackPhase.Spinning || phase == SlotsPlaybackPhase.Presenting));

    public SlotsSpinView CurrentSpin => spins[spinIndex];

    public void Reset()
    {
        spins.Clear();
        phase = SlotsPlaybackPhase.Idle;
        spinIndex = 0;
        phaseSeconds = 0f;
        stake = 0;
        totalWin = 0;
        capApplied = false;
        committedWin = 0;
        jackpot = 0;
    }

    public bool Begin(CasinoSlotsSpinDto round)
    {
        var baseGrid = round.BaseSpin?.Grid;
        if (!round.Granted || baseGrid is null || baseGrid.Length != SlotsRules.CellCount)
        {
            return false;
        }

        Reset();
        stake = round.Stake;
        totalWin = round.TotalWin;
        capApplied = round.CapApplied;
        jackpot = round.Jackpot;
        spins.Add(ViewOf(round.BaseSpin!));
        var freeSpins = round.FreeSpins;
        if (freeSpins is not null)
        {
            for (var freeIndex = 0; freeIndex < freeSpins.Length; freeIndex++)
            {
                var grid = freeSpins[freeIndex].Grid;
                if (grid is null || grid.Length != SlotsRules.CellCount)
                {
                    continue;
                }

                spins.Add(ViewOf(freeSpins[freeIndex]));
            }
        }

        phase = SlotsPlaybackPhase.Spinning;
        choreography.Begin(AnticipationFor(spins[0]), turbo);
        return true;
    }

    public void Update(float deltaSeconds)
    {
        switch (phase)
        {
            case SlotsPlaybackPhase.Spinning:
                choreography.Update(deltaSeconds);
                if (!choreography.Settled)
                {
                    return;
                }

                committedWin = Math.Min(totalWin, committedWin + spins[spinIndex].Win);
                phase = SlotsPlaybackPhase.Presenting;
                phaseSeconds = 0f;
                return;
            case SlotsPlaybackPhase.Presenting:
                phaseSeconds += deltaSeconds;
                var hold = spins[spinIndex].Win > 0
                    ? WinPresentSeconds
                    : (turbo ? TurboLossPresentSeconds : LossPresentSeconds);
                if (phaseSeconds < hold)
                {
                    return;
                }

                AdvanceAfterPresent();
                return;
            case SlotsPlaybackPhase.BonusIntro:
                phaseSeconds += deltaSeconds;
                if (phaseSeconds < BonusIntroSeconds)
                {
                    return;
                }

                StartSpin(1);
                return;
            default:
                return;
        }
    }

    public void Skip()
    {
        if (phase == SlotsPlaybackPhase.Idle || phase == SlotsPlaybackPhase.Finished || spins.Count == 0)
        {
            return;
        }

        spinIndex = spins.Count - 1;
        choreography.Finish();
        committedWin = totalWin;
        phase = SlotsPlaybackPhase.Finished;
        phaseSeconds = 0f;
    }

    private void AdvanceAfterPresent()
    {
        if (spinIndex == 0 && spins.Count > 1)
        {
            phase = SlotsPlaybackPhase.BonusIntro;
            phaseSeconds = 0f;
            return;
        }

        if (spinIndex + 1 < spins.Count)
        {
            StartSpin(spinIndex + 1);
            return;
        }

        phase = SlotsPlaybackPhase.Finished;
        phaseSeconds = 0f;
    }

    private void StartSpin(int index)
    {
        spinIndex = index;
        phase = SlotsPlaybackPhase.Spinning;
        phaseSeconds = 0f;
        if (index == 0)
        {
            choreography.Begin(AnticipationFor(spins[index]), turbo);
            return;
        }

        choreography.Begin(AnticipationFor(spins[index]), FreeSpinFirstStopSeconds, FreeSpinStopStaggerSeconds);
    }

    private bool AnticipationFor(in SlotsSpinView spin)
    {
        return spin.ScatterCount >= 3 || (stake > 0 && spin.Win >= stake * BigWinMultiple);
    }

    private static SlotsSpinView ViewOf(CasinoSlotsSpinResultDto result)
    {
        return new SlotsSpinView(result.Grid!, result.LineWins ?? NoLineWins, result.ScatterCount, result.Win,
            result.SpinsAdded);
    }
}
