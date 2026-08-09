namespace Aetherphone.Core.Casino;

// An invite token is a reference, never a capability. Pasting one names a table and nothing more:
// the server still runs the whole admission gate on the read and again on the knock, so a token
// scraped out of a chat log buys its finder exactly one knock at a door that may not open. That is
// what lets the token be plain text with no expiry machinery behind it, and why revoking an invite
// is a row on the allowlist rather than a key rotation.
internal static class CasinoShare
{
    public const int MaxIdLength = 64;

    private const string TokenPrefix = "[aep.casino.v1:";
    private const string TokenSuffix = "]";

    public static string Compose(string tableId)
    {
        return string.Concat(TokenPrefix, tableId, TokenSuffix);
    }

    public static bool IsToken(string? body)
    {
        return TryParse(body, out _);
    }

    // A bare table id pasted without its wrapper is accepted too, because a player who copies the
    // middle of the token out of a chat line has still told us exactly which table they mean.
    public static bool TryParse(string? body, out string tableId)
    {
        tableId = string.Empty;
        if (string.IsNullOrEmpty(body))
        {
            return false;
        }

        var text = body.Trim();
        if (text.StartsWith(TokenPrefix, StringComparison.Ordinal)
            && text.EndsWith(TokenSuffix, StringComparison.Ordinal)
            && text.Length > TokenPrefix.Length + TokenSuffix.Length)
        {
            text = text.Substring(TokenPrefix.Length, text.Length - TokenPrefix.Length - TokenSuffix.Length);
        }

        if (text.Length == 0 || text.Length > MaxIdLength || !IsTableId(text))
        {
            return false;
        }

        tableId = text;
        return true;
    }

    private static bool IsTableId(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            var valid = character is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '-';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
