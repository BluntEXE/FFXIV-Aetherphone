namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CasinoStateDto(
    string SittingId = "",
    string TableId = "",
    string GameKind = "",
    long Stack = 0,
    long ChipsIn = 0,
    long NetToday = 0,
    long LossLimit = 0,
    long LossHeadroom = 0,
    long SelfLimit = 0,
    long PendingRaiseLimit = 0,
    long PendingRaiseDay = 0,
    bool StakesPaused = false,
    bool Draining = false,
    long MinBuyIn = 0,
    long MaxBuyIn = 0,
    string Reason = "");

internal sealed record CasinoSittingDto(
    string SittingId = "",
    string TableId = "",
    string GameKind = "",
    long Stack = 0,
    long ChipsIn = 0,
    long ChipsOut = 0,
    long Balance = 0,
    string Reason = "");

internal sealed record CasinoOpenSittingRequest(string ClientSittingId, string GameKind, long Amount);

internal sealed record CasinoTopUpRequest(string ActionId, long Amount);

internal sealed record CasinoCloseSittingRequest(string ActionId);

internal sealed record CasinoLimitsRequest(long SelfLossLimit);
