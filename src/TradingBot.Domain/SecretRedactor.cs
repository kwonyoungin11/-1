namespace TradingBot.Domain;

/// <summary>Masks secrets and account identifiers for logs, snapshots, and UI.</summary>
public static class SecretRedactor
{
    public static string MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "[empty]";
        }

        if (token.Length <= 8)
        {
            return "****";
        }

        return token[..2] + new string('*', Math.Min(token.Length - 4, 24)) + token[^2..];
    }

    public static string MaskAccount(string? account)
    {
        if (string.IsNullOrEmpty(account))
        {
            return "[empty]";
        }

        if (account.Length <= 4)
        {
            return "****";
        }

        return new string('*', Math.Max(account.Length - 4, 4)) + account[^4..];
    }

    public static string RedactMessage(string message)
    {
        // Conservative placeholder redaction for common secret-looking substrings.
        // Full pattern set will grow with logging middleware.
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        return message
            .Replace("Bearer ", "Bearer [REDACTED] ", StringComparison.OrdinalIgnoreCase);
    }
}
