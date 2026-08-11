namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CoinWalletDto(
    long Balance,
    long LifetimeEarned,
    long LifetimeSpent,
    long EarnedToday,
    long DailyCap,
    long ResetsAtUnix,
    int StreakDays,
    bool CheckInAvailable,
    bool Paused,
    string FeaturedGameId,
    int GameMinSeconds,
    int GameDeepSeconds,
    CoinRuleStatusDto[] Rules,
    long? FrozenUntilUnix = null,
    string FreezeReason = "");

internal sealed record CoinRuleStatusDto(
    string RuleId,
    string App,
    long Amount,
    long PeriodCap,
    long EarnedThisPeriod,
    int Awards,
    int PeriodLimit,
    bool Weekly);

internal sealed record CoinLedgerEntryDto(
    string Id,
    string RuleId,
    long Amount,
    long BalanceAfter,
    string App,
    string SourceId,
    string Detail,
    long CreatedAtUnix);

internal sealed record CoinLedgerPage(CoinLedgerEntryDto[] Items, string? NextCursor = null);

internal sealed record CoinSkuDto(
    string Id,
    string Kind,
    string Payload,
    string Name,
    long Price,
    int SortOrder,
    bool Owned,
    long? AvailableUntilUnix = null,
    CoinTranslationDto[]? Translations = null);

internal sealed record CoinTranslationDto(string Language, string Name);

internal sealed record CoinCatalogDto(CoinSkuDto[] Skus, long Balance);

internal sealed record CoinAwardDto(bool Granted, long Amount, long Balance, string Reason = "");

internal sealed record CoinGameSessionRequest(string GameId);

internal sealed record CoinGameSessionDto(
    string SessionId,
    string GameId,
    bool Featured,
    int MinSeconds,
    int DeepSeconds,
    long ExpiresAtUnix,
    string Reason = "",
    long StartedAtUnix = 0);

internal sealed record CoinPurchaseRequest(string SkuId, long ExpectedPrice);

internal sealed record CoinPurchaseResult(
    bool Purchased,
    long Balance,
    string SkuId,
    long Price,
    string Reason = "");
