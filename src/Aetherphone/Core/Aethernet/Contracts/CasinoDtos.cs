namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CasinoSittingDto(
    string Id = "",
    string TableId = "",
    string GameKind = "",
    int State = 0,
    long Stack = 0,
    long ChipsIn = 0,
    long ChipsOut = 0);

internal sealed record CasinoStateDto(
    bool StakesPaused = false,
    bool Draining = false,
    CasinoSittingDto? Sitting = null,
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    long DailyBuyInCap = 0,
    long LossLimit = 0,
    long LossHeadroom = 0,
    long? SelfLossLimit = null,
    long? PendingRaiseLimit = null,
    long? PendingRaiseAtUnix = null,
    long NetLossToday = 0,
    long AtRisk = 0,
    long BuyInToday = 0,
    long Balance = 0);

internal sealed record CasinoOpenSittingRequest(
    string ClientSittingId,
    string ClientActionId,
    string GameKind,
    int TableKind,
    long Amount);

internal sealed record CasinoTopUpRequest(string SittingId, string ClientActionId, long Amount);

internal sealed record CasinoCloseSittingRequest(string SittingId);

internal sealed record CasinoSittingResultDto(
    bool Granted = false,
    string Reason = "",
    CasinoSittingDto? Sitting = null,
    long Balance = 0);

internal sealed record CasinoLimitRequest(long? SelfLossLimit);

internal sealed record CasinoLimitsDto(
    long LossLimit = 0,
    long? SelfLossLimit = null,
    long? PendingRaiseLimit = null,
    long? PendingRaiseAtUnix = null);

internal sealed record CasinoSlotsSpinRequest(string SittingId, string ClientRoundId, long Stake);

internal sealed record CasinoSlotsLineWinDto(int Line = 0, int Symbol = 0, int Count = 0, long Pay = 0);

internal sealed record CasinoSlotsSpinResultDto(
    int[]? Grid = null,
    CasinoSlotsLineWinDto[]? LineWins = null,
    int ScatterCount = 0,
    long ScatterPay = 0,
    long Win = 0,
    int SpinsAdded = 0);

internal sealed record CasinoSlotsSpinDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    long Stake = 0,
    CasinoSlotsSpinResultDto? BaseSpin = null,
    CasinoSlotsSpinResultDto[]? FreeSpins = null,
    long TotalWin = 0,
    bool CapApplied = false,
    string NextSeedHash = "",
    long Stack = 0);

internal sealed record CasinoScratchBuyRequest(string SittingId, string ClientRoundId, int Tier);

internal sealed record CasinoScratchCardDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    int Tier = 0,
    int[]? Cells = null,
    long Prize = 0,
    string NextSeedHash = "",
    long Stack = 0);

internal sealed record CasinoBarkeepStartRequest(string SittingId, string ClientRoundId);

internal sealed record CasinoBarkeepPatronDto(int ArrivalSecond = 0, int[]? StepKinds = null);

internal sealed record CasinoBarkeepStartDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    CasinoBarkeepPatronDto[]? Patrons = null,
    int MaxScore = 0,
    long StartedAtUnix = 0,
    long ExpiresAtUnix = 0,
    string NextSeedHash = "",
    long Stack = 0);

internal sealed record CasinoBarkeepOrderRequest(int[] StepGrades);

internal sealed record CasinoBarkeepFinishRequest(string RoundId, CasinoBarkeepOrderRequest[] Orders);

internal sealed record CasinoBarkeepFinishDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    int Score = 0,
    long Payout = 0,
    long NetWinToday = 0,
    long Stack = 0);

internal sealed record CasinoRoundVerifyDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    string GameKind = "",
    int State = 0,
    long Stake = 0,
    long Payout = 0,
    string SeedCommitHash = "",
    string SeedRevealed = "",
    string NextSeedHash = "",
    string DrawLog = "");

internal sealed record CasinoRoundHistoryDto(
    string RoundId = "",
    string GameKind = "",
    long Stake = 0,
    long Payout = 0,
    int State = 0,
    long CreatedAtUnix = 0,
    long? SettledAtUnix = null,
    string SeedCommitHash = "",
    bool Revealed = false);

internal sealed record CasinoRoundHistoryPage(
    CasinoRoundHistoryDto[]? Items = null,
    string? NextCursor = null);

internal sealed record CasinoRoomCardDto(
    string RoomId = "",
    string GameKind = "",
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0,
    int PlayerCount = 0,
    long MinStake = 0,
    long MaxStake = 0,
    bool Draining = false);

internal sealed record CasinoRoomDirectoryDto(
    CasinoRoomCardDto[]? Rooms = null,
    long ServerNowUnixMs = 0);

internal sealed record CasinoRoomSnapshotDto(
    string RoomId = "",
    string GameKind = "",
    int Phase = 0,
    string RoundId = "",
    long PhaseEndsAtUnixMs = 0,
    bool Draining = false,
    int PlayerCount = 0,
    int EntryCount = 0,
    long StakeTotal = 0,
    long MinStake = 0,
    long MaxStake = 0,
    int[]? Numbers = null);

// A room event states only what moved, so every field is nullable and an absent one means the
// held snapshot already has the truth. Widening a value type to zero here would silently wipe
// the pot or the deadline every time the server broadcast a player count.
internal sealed record CasinoRoomEventDto(
    int? Phase = null,
    string? RoundId = null,
    long? PhaseEndsAtUnixMs = null,
    int? PlayerCount = null,
    int? EntryCount = null,
    long? StakeTotal = null,
    int[]? Numbers = null,
    string? Reason = null);

internal sealed record CasinoRoomEntryDto(
    string EntryId = "",
    int Kind = 0,
    long Stake = 0,
    int Target = 0,
    long Payout = 0,
    int State = 0);

internal sealed record CasinoRoomPrivateDto(
    string RoundId = "",
    long Staked = 0,
    CasinoRoomEntryDto[]? Entries = null);

internal sealed record CasinoRoomStateDto(
    bool Granted = false,
    string Reason = "",
    int Epoch = 0,
    long Seq = 0,
    long ServerNowUnixMs = 0,
    CasinoRoomSnapshotDto? Snapshot = null,
    CasinoRoomPrivateDto? Private = null);
