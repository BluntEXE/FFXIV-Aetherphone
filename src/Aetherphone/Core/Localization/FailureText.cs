using Aetherphone.Core.Net;

namespace Aetherphone.Core.Localization;

internal static class FailureCodes
{
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string RateLimited = "rate_limited";
    public const string ServerError = "server_error";
    public const string Suspended = "suspended";
    public const string PostEmpty = "post_empty";
    public const string PostTooLong = "post_too_long";
    public const string PostTooManyImages = "post_too_many_images";
    public const string PostQuoteMissing = "post_quote_missing";
    public const string PostQuoteNotChirp = "post_quote_not_chirp";
    public const string PostQuoteBlocked = "post_quote_blocked";
    public const string PostCooldown = "post_cooldown";
    public const string MediaInvalidReference = "media_invalid_reference";
    public const string MediaInvalidImage = "media_invalid_image";
    public const string MediaInvalidAudio = "media_invalid_audio";
}

internal sealed class FailureSlot
{
    private AepFailure failure;
    private string? cachedText;
    private LanguageInfo? cachedLanguage;

    public bool Failed => failure.Failed;

    public AepFailure Failure => failure;

    public void Set(AepFailure value)
    {
        if (cachedText is not null && failure == value)
        {
            return;
        }

        failure = value;
        cachedText = null;
        cachedLanguage = null;
    }

    public void Clear()
    {
        Set(AepFailure.None);
    }

    public string Text()
    {
        if (cachedText is not null && ReferenceEquals(cachedLanguage, Loc.Current))
        {
            return cachedText;
        }

        cachedText = FailureText.Resolve(failure);
        cachedLanguage = Loc.Current;
        return cachedText;
    }
}

internal static class FailureText
{
    public static string Resolve(AepFailure failure)
    {
        switch (failure.Kind)
        {
            case AepFailureKind.None:
                return string.Empty;
            case AepFailureKind.Offline:
                return Loc.T(L.Failure.Offline);
            case AepFailureKind.Timeout:
                return Loc.T(L.Failure.Timeout);
            case AepFailureKind.RateLimitPaused:
                return Loc.T(L.Failure.RateLimitPaused);
            case AepFailureKind.SignedOut:
                return Loc.T(L.Failure.SignedOut);
            case AepFailureKind.Cancelled:
                return string.Empty;
            case AepFailureKind.BadResponse:
                return Loc.T(L.Failure.BadResponse);
            default:
                return FromServer(failure);
        }
    }

    private static string FromServer(AepFailure failure)
    {
        switch (failure.Code)
        {
            case FailureCodes.PostEmpty:
                return Loc.T(L.Failure.PostEmpty);
            case FailureCodes.PostTooLong:
                return Valued(L.Failure.PostTooLong, failure);
            case FailureCodes.PostTooManyImages:
                return Valued(L.Failure.PostTooManyImages, failure);
            case FailureCodes.PostQuoteMissing:
                return Loc.T(L.Failure.PostQuoteMissing);
            case FailureCodes.PostQuoteNotChirp:
                return Loc.T(L.Failure.PostQuoteNotChirp);
            case FailureCodes.PostQuoteBlocked:
                return Loc.T(L.Failure.PostQuoteBlocked);
            case FailureCodes.PostCooldown:
                return Valued(L.Failure.PostCooldown, failure);
            case FailureCodes.MediaInvalidImage:
                return Loc.T(L.Failure.MediaInvalidImage);
            case FailureCodes.MediaInvalidAudio:
                return Loc.T(L.Failure.MediaInvalidAudio);
            case FailureCodes.MediaInvalidReference:
                return Loc.T(L.Failure.MediaInvalidReference);
            case FailureCodes.Suspended:
                return Loc.T(L.Failure.Suspended);
            case FailureCodes.Unauthorized:
                return Loc.T(L.Failure.Unauthorized);
            case FailureCodes.Forbidden:
                return Loc.T(L.Failure.Forbidden);
            case FailureCodes.NotFound:
                return Loc.T(L.Failure.NotFound);
            case FailureCodes.RateLimited:
                return Loc.T(L.Failure.RateLimited);
            default:
                return FromStatus(failure);
        }
    }

    private static string FromStatus(AepFailure failure)
    {
        switch (failure.StatusCode)
        {
            case 401:
                return Loc.T(L.Failure.Unauthorized);
            case 403:
                return Loc.T(L.Failure.Forbidden);
            case 404:
                return Loc.T(L.Failure.NotFound);
            case 429:
                return Loc.T(L.Failure.RateLimited);
            default:
                return failure.StatusCode >= 500
                    ? Loc.T(L.Failure.ServerError, failure.Reference())
                    : Loc.T(L.Failure.Unknown, failure.Reference());
        }
    }

    private static string Valued(LocString entry, AepFailure failure)
    {
        return string.IsNullOrEmpty(failure.Value)
            ? Loc.T(L.Failure.Unknown, failure.Reference())
            : Loc.T(entry, failure.Value);
    }
}
