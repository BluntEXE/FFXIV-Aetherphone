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

// One shape serves the socket and the poll: GET /casino/rooms/{id} answers with exactly the
// snapshot casino.attached and casino.snapshot carry, so a player whose socket is dead plays the
// same room from the same numbers. Deadlines ship as absolute server milliseconds beside
// ServerNowUnixMs, never as durations, because a message delayed in a send queue must not hand the
// receiver phantom seconds. GameState is the per-game blob as a JSON string: the registry owns the
// room and the games own their contents, so a new game kind ships without widening this envelope.
internal sealed record CasinoRoomSnapshotDto(
    string RoomId = "",
    string GameKind = "",
    int Kind = 0,
    int State = 0,
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0,
    long RoundIndex = 0,
    string GameState = "",
    int Occupancy = 0,
    bool Attached = false,
    int Epoch = 0,
    long Seq = 0,
    long ServerNowUnixMs = 0);

// A room event states the whole room, not a delta: every field is the live value and GameState is
// the same blob a snapshot carries. Nothing here accumulates on the client, which is what lets a
// phone that missed a frame land on the same board as one that watched every one of them.
internal sealed record CasinoRoomEventDto(
    int State = 0,
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0,
    long RoundIndex = 0,
    string GameState = "",
    int Occupancy = 0);

internal sealed record CasinoPrivateDto(string EventKind = "", string Payload = "");

internal sealed record CasinoRoomListItemDto(
    string RoomId = "",
    string GameKind = "",
    int Kind = 0,
    int State = 0,
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0,
    long RoundIndex = 0,
    int Occupancy = 0);

internal sealed record CasinoRoomListDto(
    CasinoRoomListItemDto[]? Rooms = null,
    long ServerNowUnixMs = 0);

// One row per bet spot, carrying the whole room's money on it. Occupancy is half the game at a
// communal wheel, so the totals ride the public game state instead of a private payload: everyone
// at the rail reads the same board. Segment and Spot stay -1 until the window has closed and the
// draw has been made, which is what stops a rim from landing before the money stopped moving.
internal sealed record CasinoWheelSpotDto(
    int Spot = 0,
    int Multiplier = 0,
    int Segments = 0,
    int ReturnBasisPoints = 0,
    long Amount = 0,
    int Bettors = 0);

internal sealed record CasinoWheelRoomStateDto(
    long RoundIndex = 0,
    string Commit = "",
    string NextCommit = "",
    string Seed = "",
    int Segment = -1,
    int Spot = -1,
    CasinoWheelSpotDto[]? Spots = null,
    long Staked = 0,
    long Paid = 0,
    int[]? Recent = null,
    long MinBet = 0,
    long MaxBetPerSpot = 0,
    long MaxBetPerRound = 0,
    long MaxWin = 0);

// A stage the hall has already awarded, in the order it was won. Ball names the call that closed
// it and Winners how many cards shared it, so the hall can show a full house that landed on ball
// forty one and say how many people are splitting nothing (every winner is paid in full).
internal sealed record CasinoBingoStageDto(
    int Stage = 0,
    int Ball = 0,
    long Prize = 0,
    int Winners = 0,
    long Paid = 0);

internal sealed record CasinoBingoRoomStateDto(
    long RoundIndex = 0,
    string Commit = "",
    string NextCommit = "",
    string Seed = "",
    int Cards = 0,
    int Players = 0,
    long[]? Prizes = null,
    int PrizeCardCap = 0,
    int BallIndex = 0,
    int[]? Balls = null,
    long NextBallAtUnixMs = 0,
    CasinoBingoStageDto[]? Stages = null,
    bool Ended = false,
    bool Cancelled = false,
    long CardPrice = 0,
    int MaxCards = 0,
    long MaxWin = 0);

// The bet is the money path and it never touches the socket. RoundIndex is sent so a bet composed
// against a round that closed while the request was in flight is refused by the server rather than
// landing on the next one, and ClientBetId makes the retry of a lost response free: the same id is
// the same bet, never a second one. ClientRoundId is the chip round every bet of one spin shares,
// which is why it is minted once per room round rather than once per tap.
internal sealed record CasinoWheelBetRequest(
    string RoomId,
    long RoundIndex,
    string ClientRoundId,
    string ClientBetId,
    int Spot,
    long Amount);

internal sealed record CasinoWheelBetDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    long RoundIndex = 0,
    string RoundId = "",
    int Spot = 0,
    long Amount = 0,
    long MyStake = 0,
    long Stack = 0);

internal sealed record CasinoWheelMyBetDto(int Spot = 0, long Amount = 0);

// The personal half of a wheel round, read over HTTP because the socket carries nothing private.
// Only accepted bets come back, so a refused or refunded bet can never be painted as live money.
internal sealed record CasinoWheelBetsDto(
    string RoomId = "",
    long RoundIndex = 0,
    int Phase = 0,
    string RoundId = "",
    CasinoWheelMyBetDto[]? Bets = null,
    long MyStake = 0,
    long Stack = 0);

// One seat as everybody at the rail sees it. Cards are the shared playing card integers 0..51 and
// -1 is a card whose existence is public but whose face is not, which is the dealer's hole card
// before the reveal and any seat the table is keeping closed. A concealed card is never sent as a
// value the client could uncover: the server leaves the placeholder in and the face simply does not
// travel.
internal sealed record CasinoBlackjackHandDto(
    int SplitIndex = 0,
    int[]? Cards = null,
    int Total = 0,
    bool Soft = false,
    long Bet = 0,
    int Outcome = 0,
    long Delta = 0);

internal sealed record CasinoBlackjackSeatDto(
    int SeatIndex = 0,
    string DisplayName = "",
    string Handle = "",
    long Stack = 0,
    long Bet = 0,
    int State = 0,
    bool Mine = false,
    bool Connected = true,
    bool Split = false,
    CasinoBlackjackHandDto[]? Hands = null);

// The per-game half of a blackjack room, riding the snapshot's GameState blob like every other
// communal game. ActionsMask states what the table will accept from the active hand right now, and
// it is the only thing the action bar is allowed to consult: a client that decided legality for
// itself would offer a double the server refuses and read as broken the moment the shoe disagreed.
internal sealed record CasinoBlackjackRoomStateDto(
    long RoundIndex = 0,
    string HandId = "",
    string Commit = "",
    string NextCommit = "",
    string Seed = "",
    CasinoBlackjackSeatDto[]? Seats = null,
    int[]? DealerCards = null,
    int DealerTotal = 0,
    bool DealerSoft = false,
    int ActiveSeat = -1,
    int ActiveSplit = -1,
    int ActionsMask = 0,
    long ActionCount = 0,
    long DeadlineUnixMs = 0,
    int WindowSeconds = 0,
    long MinBet = 0,
    long MaxBet = 0,
    int MySeat = -1);

// The seat scoped half, delivered on casino.private to one recipient. It exists so a table can deal
// a closed card without the public blob ever carrying it: the projection lays these faces over the
// placeholders for my seat only, and every other seat keeps its backs because there is nothing to
// lay over them.
internal sealed record CasinoBlackjackPrivateDto(
    long RoundIndex = 0,
    int SeatIndex = -1,
    int[][]? Hands = null);

internal sealed record CasinoBlackjackBetRequest(
    string RoomId,
    long RoundIndex,
    string ClientRoundId,
    string ClientBetId,
    long Amount);

internal sealed record CasinoBlackjackBetDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    long RoundIndex = 0,
    string RoundId = "",
    long Amount = 0,
    long Stack = 0);

// An action is guarded by the hand it was composed against and the action count the client had seen,
// so a post that raced the table loses rather than landing on the next decision. A refusal answers
// with the reason and the client resyncs instead of guessing.
internal sealed record CasinoBlackjackActionRequest(
    string RoomId,
    string HandId,
    long RoundIndex,
    int SplitIndex,
    int Action,
    long ActionSeq,
    string ClientActionId);

internal sealed record CasinoBlackjackActionDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    string HandId = "",
    long RoundIndex = 0,
    int Action = 0,
    long Stack = 0);

internal sealed record CasinoBingoCardsRequest(
    string RoomId,
    long RoundIndex,
    string ClientRoundId,
    int CardCount);

// One purchase is one game, so this answers both the buy and the read: Cards holds every printed
// card the player owns this round and Payout is what the hall settled onto them, which is the only
// figure the summary is ever allowed to speak.
internal sealed record CasinoBingoCardsDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    long RoundIndex = 0,
    string RoundId = "",
    int[][]? Cards = null,
    long Stake = 0,
    long Payout = 0,
    int RoundState = 0,
    string SeedCommitHash = "",
    string NextSeedHash = "",
    long Stack = 0);

// The daily spin is the one game with no sitting and no chips: it mints coins straight into the
// wallet, so its answer carries Balance where every other game carries Stack. There is no read
// route, only this one idempotent post: the day's spin either happens here or is replayed here,
// and a replay comes back refused with the segment it already landed on and what it already paid.
// SegmentAward is what the wedge is worth and Amount is what reached the wallet, so a spin clipped
// by the account's daily cap can still show the number it stopped on.
internal sealed record CasinoDailySpinDto(
    bool Granted = false,
    string Reason = "",
    string RoundId = "",
    int Segment = -1,
    long SegmentAward = 0,
    long Amount = 0,
    long Balance = 0,
    long NextSpinAtUnix = 0,
    string SeedCommitHash = "",
    string NextSeedHash = "");
