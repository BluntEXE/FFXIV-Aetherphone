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
// The trailing block is the care surface, and every field on it is a server fact the screen is
// forbidden from inferring: Spectators is the count at the rail, BoundElsewhere says this account is
// playing the seat from another device, SeatHeldUntilUnixMs is the absolute instant the hold lapses
// (never a duration, for the same reason a phase deadline is not), JoinsNextHand marks a seat that
// arrived mid hand, and Draining is the table winding down while the open hand finishes.
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
    int MySeat = -1,
    string TableName = "",
    int Spectators = 0,
    bool BoundElsewhere = false,
    long SeatHeldUntilUnixMs = 0,
    bool JoinsNextHand = false,
    bool Draining = false,
    bool InviteOnly = false,
    bool Owner = false);

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

// One live table as the browser sees it before anybody steps into it. Seats and spectators travel as
// counts rather than a roster because the directory is a public read and a lurker list is an
// identity leak; the room itself is where names appear, and only for the seats. An invite only table
// is never listed, so a row that carries InviteOnly is one this account already belongs to.
internal sealed record CasinoTableRowDto(
    string RoomId = "",
    string GameKind = "",
    string Name = "",
    int StakeTier = 0,
    long MinBet = 0,
    long MaxBet = 0,
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    int SeatCount = 0,
    int SeatsTaken = 0,
    int Spectators = 0,
    bool InviteOnly = false,
    bool Owner = false,
    bool Seated = false,
    bool Draining = false,
    int Phase = 0,
    long PhaseEndsAtUnixMs = 0);

internal sealed record CasinoTableListDto(
    CasinoTableRowDto[]? Tables = null,
    long ServerNowUnixMs = 0);

// Quick seat is the whole floor-tile-to-a-bet path in one post: the server picks the table, so the
// client never races five phones onto the same open seat, and it answers with the buy-in bounds the
// cashier is about to be prefilled from. It reserves nothing; sitting is still a separate intent.
internal sealed record CasinoQuickSeatRequest(string GameKind, int StakeTier);

internal sealed record CasinoQuickSeatDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    string Name = "",
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    long SuggestedBuyIn = 0,
    long MinBet = 0,
    long MaxBet = 0,
    int SeatIndex = -1);

// Creating a private table is idempotent on ClientTableId so a lost response replays the same table
// instead of hosting a second one, which is also what the server's one-live-table-per-owner guard
// answers with when a second create really does arrive.
internal sealed record CasinoCreateTableRequest(string ClientTableId, string GameKind, int StakeTier);

internal sealed record CasinoTableDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    string Name = "",
    string InviteToken = "",
    bool InviteOnly = false,
    bool Owner = false,
    long MinBuyIn = 0,
    long MaxBuyIn = 0);

// The door is the host's half of a private table and it is read separately from the room, because
// the knocks are the host's business and nobody else's: the public snapshot must never carry the
// name of somebody who asked to come in and was not let in.
internal sealed record CasinoKnockerDto(
    string UserId = "",
    string DisplayName = "",
    string Handle = "",
    long KnockedAtUnix = 0);

internal sealed record CasinoTableDoorDto(
    string RoomId = "",
    bool Owner = false,
    string InviteToken = "",
    CasinoKnockerDto[]? Knocks = null,
    CasinoKnockerDto[]? Seated = null);

internal sealed record CasinoDoorRequest(string UserId, bool Approve);

internal sealed record CasinoDoorResultDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    bool Pending = false);

// Sitting is an intent, never a fact: the answer says whether the seat was taken, whether the hand
// in play means the seat waits for the next one, and whether this account is already holding the
// seat from another device. ClientSeatId makes the retry of a lost response free.
internal sealed record CasinoSitRequest(int SeatIndex, string ClientSeatId, long BuyIn);

internal sealed record CasinoSeatDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    int SeatIndex = -1,
    bool JoinsNextHand = false,
    bool BoundElsewhere = false,
    long SeatHeldUntilUnixMs = 0,
    long Stack = 0);

// Standing has to work when the casino app flag is off, which is why it is its own route rather than
// a flavour of cash-out: a killed floor must never strand chips on a table. AtHandEnd is the server
// saying the intent is queued behind the hand the player is still in.
internal sealed record CasinoStandRequest(string ClientStandId);

internal sealed record CasinoStandDto(
    bool Granted = false,
    string Reason = "",
    string RoomId = "",
    bool AtHandEnd = false,
    long Balance = 0);

// A takeover is always a gesture. The client never posts this on its own: the seat sits in the
// "playing on another device" state until somebody presses the button, because the seat sees hole
// cards and spends money and a silent swap would move both without asking.
internal sealed record CasinoClaimRequest(string ClientClaimId);

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
