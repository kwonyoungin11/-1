using TradingBot.Domain;
using TradingBot.Observability;

namespace TradingBot.Observability.Tests;

public class AuditMessageRedactorTests
{
    [Fact]
    public void Redact_scrubs_full_bearer_token_beyond_domain_keyword_rewrite()
    {
        var input = "Authorization: Bearer abcdefghijklmnop";
        // Domain only rewrites the "Bearer " keyword and leaves the token value;
        // Observability must not retain the token material in audit text.
        Assert.Contains("abcdefghijklmnop", SecretRedactor.RedactMessage(input), StringComparison.Ordinal);

        var redacted = AuditMessageRedactor.Redact(input);
        Assert.Equal("Authorization: Bearer [REDACTED]", redacted);
        Assert.DoesNotContain("abcdefghijklmnop", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_null_or_empty_is_safe()
    {
        Assert.Equal(string.Empty, AuditMessageRedactor.Redact(null));
        Assert.Equal(string.Empty, AuditMessageRedactor.Redact(string.Empty));
    }

    [Fact]
    public void MaskToken_and_MaskAccount_delegate_to_domain()
    {
        Assert.Equal(SecretRedactor.MaskToken("secret-token-value"), AuditMessageRedactor.MaskToken("secret-token-value"));
        Assert.Equal(SecretRedactor.MaskAccount("1234567890"), AuditMessageRedactor.MaskAccount("1234567890"));
        Assert.DoesNotContain("secret-token-value", AuditMessageRedactor.MaskToken("secret-token-value"), StringComparison.Ordinal);
        Assert.Contains("****", AuditMessageRedactor.MaskAccount("1234567890"), StringComparison.Ordinal);
    }
}
