using System.Text.RegularExpressions;
using TradingBot.Domain;

namespace TradingBot.Observability;

/// <summary>
/// Observability-facing redaction helpers.
/// Delegates masking rules to <see cref="SecretRedactor"/> and applies extra free-text scrubbing
/// so audit storage does not retain token material after common prefixes.
/// </summary>
public static partial class AuditMessageRedactor
{
    /// <summary>Redact free-text messages before they enter logs or audit storage.</summary>
    public static string Redact(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message ?? string.Empty;
        }

        // Domain baseline (keyword-level rules).
        var redacted = SecretRedactor.RedactMessage(message);

        // Domain currently rewrites "Bearer " → "Bearer [REDACTED] " but leaves the token value.
        // Collapse full Bearer token spans so secret material is not retained in audit entries.
        redacted = BearerTokenPattern().Replace(redacted, "Bearer [REDACTED]");

        return redacted;
    }

    /// <summary>Mask an OAuth / access token for safe display.</summary>
    public static string MaskToken(string? token) => SecretRedactor.MaskToken(token);

    /// <summary>Mask an account identifier for safe display.</summary>
    public static string MaskAccount(string? account) => SecretRedactor.MaskAccount(account);

    [GeneratedRegex(@"Bearer\s+(?:\[REDACTED\]\s+)?\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern();
}
