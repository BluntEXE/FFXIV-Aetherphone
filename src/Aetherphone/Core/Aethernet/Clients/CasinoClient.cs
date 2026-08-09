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
    internal const string DailySpinPath = "/casino/spin";

    internal static string RoomPath(string roomId)
    {
        return string.Concat(RoomsPath, "/", Uri.EscapeDataString(roomId));
    }

    internal static string RoomStakePath(string roomId)
    {
        return string.Concat(RoomPath(roomId), "/stake");
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

    public Task<CasinoRoomDirectoryDto?> RoomsAsync(CancellationToken token)
    {
        return net.GetAsync(RoomsPath, AethernetJsonContext.Default.CasinoRoomDirectoryDto, token);
    }

    public Task<CasinoRoomStateDto?> RoomStateAsync(string roomId, CancellationToken token)
    {
        return net.GetAsync(RoomPath(roomId), AethernetJsonContext.Default.CasinoRoomStateDto, token);
    }

    public Task<CasinoRoomStakeDto?> StakeRoomAsync(string roomId, string roundId, string clientEntryId,
        int target, long amount, CancellationToken token)
    {
        return net.PostAsync(RoomStakePath(roomId),
            new CasinoRoomStakeRequest(roomId, roundId, clientEntryId, target, amount),
            AethernetJsonContext.Default.CasinoRoomStakeRequest,
            AethernetJsonContext.Default.CasinoRoomStakeDto, token);
    }

    public Task<CasinoDailySpinStateDto?> DailySpinStateAsync(CancellationToken token)
    {
        return net.GetAsync(DailySpinPath, AethernetJsonContext.Default.CasinoDailySpinStateDto, token);
    }

    public Task<CasinoDailySpinDto?> ClaimDailySpinAsync(string clientRoundId, CancellationToken token)
    {
        return net.PostAsync(DailySpinPath, new CasinoDailySpinRequest(clientRoundId),
            AethernetJsonContext.Default.CasinoDailySpinRequest,
            AethernetJsonContext.Default.CasinoDailySpinDto, token);
    }
}
