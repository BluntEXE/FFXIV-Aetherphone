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
    internal const string BlackjackBetPath = "/casino/blackjack/bet";
    internal const string BlackjackActionPath = "/casino/blackjack/action";

    internal static string RoomPath(string roomId)
    {
        return string.Concat(RoomsPath, "/", Uri.EscapeDataString(roomId));
    }

    internal static string WheelBetsPath(string roomId)
    {
        return string.Concat("/casino/wheel/", Uri.EscapeDataString(roomId), "/bets");
    }

    internal static string BingoMyCardsPath(string roomId)
    {
        return string.Concat("/casino/bingo/", Uri.EscapeDataString(roomId), "/cards");
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

    // The room read answers with the bare snapshot or a 404, so the status is the only way to tell
    // a room that is gone from a read that never arrived. A caller that cannot see the difference
    // would either kill a live room on one dropped packet or sit forever on one that closed.
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

    public Task<CasinoBingoCardsDto?> MyBingoCardsAsync(string roomId, CancellationToken token)
    {
        return net.GetAsync(BingoMyCardsPath(roomId), AethernetJsonContext.Default.CasinoBingoCardsDto, token);
    }

    // The free spin has no read route at all: this post either takes today's spin or replays the
    // one already taken, and both answers carry the segment, so asking is the same act as claiming.
    public Task<CasinoDailySpinDto?> ClaimDailySpinAsync(CancellationToken token)
    {
        return net.RequestAsync(HttpMethod.Post, DailySpinPath,
            AethernetJsonContext.Default.CasinoDailySpinDto, token);
    }
}
