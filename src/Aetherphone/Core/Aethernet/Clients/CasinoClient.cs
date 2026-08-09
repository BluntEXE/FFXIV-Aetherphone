using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class CasinoClient
{
    internal const string StatePath = "/casino";
    internal const string OpenSittingPath = "/casino/sittings";
    internal const string TopUpPath = "/casino/sittings/topup";
    internal const string CloseSittingPath = "/casino/sittings/close";
    internal const string LimitsPath = "/casino/limits";
    internal const string SpinSlotsPath = "/casino/slots/spin";
    internal const string BuyScratchPath = "/casino/scratch/buy";
    internal const string StartBarkeepPath = "/casino/barkeep/start";
    internal const string FinishBarkeepPath = "/casino/barkeep/finish";

    internal const string RoundsPath = "/casino/rounds";
    internal const string RoomsPath = "/casino/rooms";
    internal const string DailySpinPath = "/casino/dailyspin";
    internal const string WheelBetPath = "/casino/wheel/bet";
    internal const string BingoCardsPath = "/casino/bingo/cards";
    internal const string BlackjackSitPath = "/casino/blackjack/sit";
    internal const string BlackjackLeavePath = "/casino/blackjack/leave";
    internal const string BlackjackWagerPath = "/casino/blackjack/wager";
    internal const string BlackjackBetPath = "/casino/blackjack/bet";
    internal const string BlackjackActionPath = "/casino/blackjack/act";

    internal const string TablesPath = "/casino/tables";
    internal const string QuickSeatPath = "/casino/tables/quickseat";

    internal static string RoomPath(string roomId)
    {
        return string.Concat(RoomsPath, "/", Uri.EscapeDataString(roomId));
    }

    internal static string TablesPagePath(string gameKind)
    {
        return gameKind.Length == 0
            ? TablesPath
            : string.Concat(TablesPath, "?game=", Uri.EscapeDataString(gameKind));
    }

    internal static string TablePath(string roomId, string leaf)
    {
        return string.Concat(TablesPath, "/", Uri.EscapeDataString(roomId), "/", leaf);
    }

    internal static string WheelBetsPath(string roomId)
    {
        return string.Concat("/casino/wheel/", Uri.EscapeDataString(roomId), "/bets");
    }

    internal static string BingoMyCardsPath(string roomId)
    {
        return string.Concat("/casino/bingo/", Uri.EscapeDataString(roomId), "/cards");
    }

    internal static string BlackjackMyHandPath(string roomId)
    {
        return string.Concat("/casino/blackjack/", Uri.EscapeDataString(roomId), "/hand");
    }

    internal static string VerifyRoundPath(string roundId)
    {
        return string.Concat("/casino/rounds/", roundId, "/verify");
    }

    internal static string RoundsPagePath(string? cursor)
    {
        return cursor is null || cursor.Length == 0
            ? RoundsPath
            : string.Concat(RoundsPath, "?cursor=", Uri.EscapeDataString(cursor));
    }

    internal const int SoloTableKind = 0;

    private readonly AethernetTransport net;

    public CasinoClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<CasinoStateDto?> GetStateAsync(CancellationToken token)
    {
        return net.GetAsync(StatePath, AethernetJsonContext.Default.CasinoStateDto, token);
    }

    public Task<CasinoSittingResultDto?> OpenSittingAsync(string clientSittingId, string clientActionId,
        string gameKind, long amount, CancellationToken token)
    {
        return net.PostAsync(OpenSittingPath,
            new CasinoOpenSittingRequest(clientSittingId, clientActionId, gameKind, SoloTableKind, amount),
            AethernetJsonContext.Default.CasinoOpenSittingRequest,
            AethernetJsonContext.Default.CasinoSittingResultDto, token);
    }

    public Task<CasinoSittingResultDto?> TopUpAsync(string sittingId, string clientActionId, long amount,
        CancellationToken token)
    {
        return net.PostAsync(TopUpPath, new CasinoTopUpRequest(sittingId, clientActionId, amount),
            AethernetJsonContext.Default.CasinoTopUpRequest,
            AethernetJsonContext.Default.CasinoSittingResultDto, token);
    }

    public Task<CasinoSittingResultDto?> CloseSittingAsync(string sittingId, CancellationToken token)
    {
        return net.PostAsync(CloseSittingPath, new CasinoCloseSittingRequest(sittingId),
            AethernetJsonContext.Default.CasinoCloseSittingRequest,
            AethernetJsonContext.Default.CasinoSittingResultDto, token);
    }

    public Task<CasinoLimitsDto?> SetLimitsAsync(long? selfLossLimit, CancellationToken token)
    {
        return net.PostAsync(LimitsPath, new CasinoLimitRequest(selfLossLimit),
            AethernetJsonContext.Default.CasinoLimitRequest,
            AethernetJsonContext.Default.CasinoLimitsDto, token);
    }

    public Task<CasinoSlotsSpinDto?> SpinSlotsAsync(string sittingId, string clientRoundId, long stake,
        CancellationToken token)
    {
        return net.PostAsync(SpinSlotsPath, new CasinoSlotsSpinRequest(sittingId, clientRoundId, stake),
            AethernetJsonContext.Default.CasinoSlotsSpinRequest,
            AethernetJsonContext.Default.CasinoSlotsSpinDto, token);
    }

    public Task<CasinoScratchCardDto?> BuyScratchAsync(string sittingId, string clientRoundId, int tier,
        CancellationToken token)
    {
        return net.PostAsync(BuyScratchPath, new CasinoScratchBuyRequest(sittingId, clientRoundId, tier),
            AethernetJsonContext.Default.CasinoScratchBuyRequest,
            AethernetJsonContext.Default.CasinoScratchCardDto, token);
    }

    public Task<CasinoBarkeepStartDto?> StartBarkeepAsync(string sittingId, string clientRoundId,
        CancellationToken token)
    {
        return net.PostAsync(StartBarkeepPath, new CasinoBarkeepStartRequest(sittingId, clientRoundId),
            AethernetJsonContext.Default.CasinoBarkeepStartRequest,
            AethernetJsonContext.Default.CasinoBarkeepStartDto, token);
    }

    public Task<CasinoBarkeepFinishDto?> FinishBarkeepAsync(string roundId, CasinoBarkeepOrderRequest[] orders,
        CancellationToken token)
    {
        return net.PostAsync(FinishBarkeepPath, new CasinoBarkeepFinishRequest(roundId, orders),
            AethernetJsonContext.Default.CasinoBarkeepFinishRequest,
            AethernetJsonContext.Default.CasinoBarkeepFinishDto, token);
    }

    public Task<CasinoRoundVerifyDto?> VerifyRoundAsync(string roundId, CancellationToken token)
    {
        return net.GetAsync(VerifyRoundPath(roundId), AethernetJsonContext.Default.CasinoRoundVerifyDto, token);
    }

    public Task<CasinoRoundHistoryPage?> RoundsPageAsync(string? cursor, CancellationToken token)
    {
        return net.GetAsync(RoundsPagePath(cursor), AethernetJsonContext.Default.CasinoRoundHistoryPage, token);
    }

    public Task<CasinoRoomListDto?> RoomsAsync(CancellationToken token)
    {
        return net.GetAsync(RoomsPath, AethernetJsonContext.Default.CasinoRoomListDto, token);
    }

    public Task<CasinoRoomSnapshotDto?> RoomStateAsync(string roomId, Action<int> onStatus,
        CancellationToken token)
    {
        return net.GetAsync(RoomPath(roomId), AethernetJsonContext.Default.CasinoRoomSnapshotDto, token,
            onStatus);
    }

    public Task<CasinoWheelBetDto?> PlaceWheelBetAsync(string roomId, long roundIndex, string clientRoundId,
        string clientBetId, int spot, long amount, CancellationToken token)
    {
        return net.PostAsync(WheelBetPath,
            new CasinoWheelBetRequest(roomId, roundIndex, clientRoundId, clientBetId, spot, amount),
            AethernetJsonContext.Default.CasinoWheelBetRequest,
            AethernetJsonContext.Default.CasinoWheelBetDto, token);
    }

    public Task<CasinoWheelBetsDto?> MyWheelBetsAsync(string roomId, CancellationToken token)
    {
        return net.GetAsync(WheelBetsPath(roomId), AethernetJsonContext.Default.CasinoWheelBetsDto, token);
    }

    public Task<CasinoBingoCardsDto?> BuyBingoCardsAsync(string roomId, long roundIndex, string clientRoundId,
        int cardCount, CancellationToken token)
    {
        return net.PostAsync(BingoCardsPath,
            new CasinoBingoCardsRequest(roomId, roundIndex, clientRoundId, cardCount),
            AethernetJsonContext.Default.CasinoBingoCardsRequest,
            AethernetJsonContext.Default.CasinoBingoCardsDto, token);
    }

    public Task<CasinoBlackjackBetDto?> PlaceBlackjackBetAsync(string roomId, long roundIndex, string clientRoundId,
        string clientBetId, long amount, CancellationToken token)
    {
        return net.PostAsync(BlackjackBetPath,
            new CasinoBlackjackBetRequest(roomId, roundIndex, clientRoundId, clientBetId, amount),
            AethernetJsonContext.Default.CasinoBlackjackBetRequest,
            AethernetJsonContext.Default.CasinoBlackjackBetDto, token);
    }

    public Task<CasinoBlackjackActionDto?> SendBlackjackActionAsync(string roomId, string handId, long roundIndex,
        int splitIndex, int action, long actionSeq, string clientActionId, CancellationToken token)
    {
        return net.PostAsync(BlackjackActionPath,
            new CasinoBlackjackActionRequest(roomId, handId, roundIndex, splitIndex, action, actionSeq,
                clientActionId),
            AethernetJsonContext.Default.CasinoBlackjackActionRequest,
            AethernetJsonContext.Default.CasinoBlackjackActionDto, token);
    }

    public Task<CasinoTableListDto?> TablesAsync(string gameKind, CancellationToken token)
    {
        return net.GetAsync(TablesPagePath(gameKind), AethernetJsonContext.Default.CasinoTableListDto, token);
    }

    public Task<CasinoQuickSeatDto?> QuickSeatAsync(string gameKind, int stakeTier, CancellationToken token)
    {
        return net.PostAsync(QuickSeatPath, new CasinoQuickSeatRequest(gameKind, stakeTier),
            AethernetJsonContext.Default.CasinoQuickSeatRequest,
            AethernetJsonContext.Default.CasinoQuickSeatDto, token);
    }

    public Task<CasinoTableDto?> CreateTableAsync(string clientTableId, string gameKind, int stakeTier,
        CancellationToken token)
    {
        return net.PostAsync(TablesPath, new CasinoCreateTableRequest(clientTableId, gameKind, stakeTier),
            AethernetJsonContext.Default.CasinoCreateTableRequest,
            AethernetJsonContext.Default.CasinoTableDto, token);
    }

    public Task<CasinoTableDto?> TableAsync(string roomId, Action<int> onStatus, CancellationToken token)
    {
        return net.GetAsync(string.Concat(TablesPath, "/", Uri.EscapeDataString(roomId)),
            AethernetJsonContext.Default.CasinoTableDto, token, onStatus);
    }

    public Task<CasinoTableDoorDto?> TableDoorAsync(string roomId, CancellationToken token)
    {
        return net.GetAsync(TablePath(roomId, "door"), AethernetJsonContext.Default.CasinoTableDoorDto, token);
    }

    public Task<CasinoDoorResultDto?> KnockAsync(string roomId, CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, TablePath(roomId, "knock"),
            AethernetJsonContext.Default.CasinoDoorResultDto, token);
    }

    public Task<CasinoDoorResultDto?> AnswerKnockAsync(string roomId, string userId, bool approve,
        CancellationToken token)
    {
        return net.PostAsync(TablePath(roomId, "door"), new CasinoDoorRequest(userId, approve),
            AethernetJsonContext.Default.CasinoDoorRequest,
            AethernetJsonContext.Default.CasinoDoorResultDto, token);
    }

    public Task<CasinoDoorResultDto?> KickAsync(string roomId, string userId, CancellationToken token)
    {
        return net.PostAsync(TablePath(roomId, "kick"), new CasinoDoorRequest(userId, false),
            AethernetJsonContext.Default.CasinoDoorRequest,
            AethernetJsonContext.Default.CasinoDoorResultDto, token);
    }

    public Task<CasinoSeatDto?> SitAsync(string roomId, int seatIndex, string clientSittingId,
        string clientActionId, long buyIn, CancellationToken token)
    {
        return net.PostAsync(BlackjackSitPath,
            new CasinoSitRequest(roomId, seatIndex, clientSittingId, clientActionId, buyIn),
            AethernetJsonContext.Default.CasinoSitRequest,
            AethernetJsonContext.Default.CasinoSeatDto, token);
    }

    public Task<CasinoStandDto?> StandAsync(string roomId, string clientStandId, CancellationToken token)
    {
        return net.PostAsync(BlackjackLeavePath, new CasinoStandRequest(roomId),
            AethernetJsonContext.Default.CasinoStandRequest,
            AethernetJsonContext.Default.CasinoStandDto, token);
    }

    public Task<CasinoSeatDto?> ClaimSeatAsync(string roomId, string clientClaimId, CancellationToken token)
    {
        return net.PostAsync(TablePath(roomId, "claim"), new CasinoClaimRequest(clientClaimId),
            AethernetJsonContext.Default.CasinoClaimRequest,
            AethernetJsonContext.Default.CasinoSeatDto, token);
    }

    public Task<CasinoBingoCardsDto?> MyBingoCardsAsync(string roomId, CancellationToken token)
    {
        return net.GetAsync(BingoMyCardsPath(roomId), AethernetJsonContext.Default.CasinoBingoCardsDto, token);
    }

    public Task<CasinoBlackjackHandReadDto?> MyBlackjackHandAsync(string roomId, CancellationToken token)
    {
        return net.GetAsync(BlackjackMyHandPath(roomId), AethernetJsonContext.Default.CasinoBlackjackHandReadDto,
            token);
    }

    public Task<CasinoDailySpinDto?> ClaimDailySpinAsync(CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, DailySpinPath,
            AethernetJsonContext.Default.CasinoDailySpinDto, token);
    }
}
