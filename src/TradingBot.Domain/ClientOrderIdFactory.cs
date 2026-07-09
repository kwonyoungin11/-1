namespace TradingBot.Domain;

/// <summary>
/// Stable client order id helper for order candidates used as idempotency keys.
/// Aligns with Toss OpenAPI <c>clientOrderId</c>: max 36 chars, pattern <c>^[a-zA-Z0-9\-_]+$</c>.
/// Candidates never submit live by themselves — ids only identify proposals / dry-run / paper paths.
/// </summary>
public static class ClientOrderIdFactory
{
    /// <summary>Toss OpenAPI maxLength for clientOrderId.</summary>
    public const int MaxLength = 36;

    /// <summary>Readable prefix for structured candidate ids (fits length budget with ticker + time).</summary>
    public const string Prefix = "cand";

    /// <summary>
    /// Creates a deterministic-ish id from symbol, side, and UTC time.
    /// Same inputs produce the same id (retry / idempotency friendly within a generation window).
    /// Always non-empty and format-valid.
    /// </summary>
    public static string Create(string symbol, string side, DateTimeOffset utc)
    {
        var sym = SanitizeSymbol(symbol, maxLen: 8);
        var s = SanitizeSide(side);
        var ms = utc.ToUnixTimeMilliseconds();
        // e.g. cand-AAPL-B-1720533600000 (well under 36 for normal tickers)
        var id = $"{Prefix}-{sym}-{s}-{ms}";
        return EnsureFits(id, deterministicSeed: id);
    }

    /// <summary>
    /// Creates a unique, always non-empty, Toss-format-compliant client order id (GUID "N" form).
    /// </summary>
    public static string CreateUnique()
    {
        // 32 hex chars — matches pattern and MaxLength.
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Creates a unique id with human-readable symbol/side/time context when space allows.
    /// Always non-empty; successive calls never collide under normal conditions.
    /// </summary>
    public static string CreateUnique(string? symbol, string? side, DateTimeOffset utc)
    {
        var sym = SanitizeSymbol(symbol, maxLen: 6);
        var s = SanitizeSide(side);
        var ms = utc.ToUnixTimeMilliseconds();
        // Short entropy so same symbol/side/ms still unique (candidate batches).
        var entropy = Guid.NewGuid().ToString("N")[..6];
        var id = $"{Prefix}-{sym}-{s}-{ms}-{entropy}";
        if (id.Length <= MaxLength && MatchesAllowedPattern(id))
        {
            return id;
        }

        return CreateUnique();
    }

    /// <summary>True when non-blank, ≤ <see cref="MaxLength"/>, and Toss pattern-compliant.</summary>
    public static bool IsValid(string? clientOrderId)
    {
        if (string.IsNullOrWhiteSpace(clientOrderId))
        {
            return false;
        }

        if (clientOrderId.Length > MaxLength)
        {
            return false;
        }

        return MatchesAllowedPattern(clientOrderId);
    }

    /// <summary>Rejects null, empty, or whitespace-only ids (idempotency key must be present).</summary>
    public static void ValidateNonEmpty(string? clientOrderId)
    {
        if (string.IsNullOrWhiteSpace(clientOrderId))
        {
            throw new ArgumentException(
                "ClientOrderId must be non-empty (required for idempotency).",
                nameof(clientOrderId));
        }
    }

    /// <summary>
    /// Rejects blank and format violations (length / allowed characters).
    /// </summary>
    public static void Validate(string? clientOrderId)
    {
        ValidateNonEmpty(clientOrderId);

        // ValidateNonEmpty guarantees non-null non-whitespace.
        var id = clientOrderId!;

        if (id.Length > MaxLength)
        {
            throw new ArgumentException(
                $"ClientOrderId must be at most {MaxLength} characters (Toss OpenAPI).",
                nameof(clientOrderId));
        }

        if (!MatchesAllowedPattern(id))
        {
            throw new ArgumentException(
                "ClientOrderId must match ^[a-zA-Z0-9\\-_]+$ (Toss OpenAPI).",
                nameof(clientOrderId));
        }
    }

    /// <summary>
    /// Returns true and the original value when valid; otherwise false and null.
    /// </summary>
    public static bool TryValidate(
        string? clientOrderId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? normalized)
    {
        if (clientOrderId is null || !IsValid(clientOrderId))
        {
            normalized = null;
            return false;
        }

        normalized = clientOrderId;
        return true;
    }

    private static string SanitizeSymbol(string? symbol, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return "UNK";
        }

        maxLen = Math.Clamp(maxLen, 1, 12);
        Span<char> buf = stackalloc char[12];
        var n = 0;
        foreach (var ch in symbol.AsSpan().Trim())
        {
            if (n >= maxLen)
            {
                break;
            }

            if (char.IsAsciiLetterOrDigit(ch))
            {
                buf[n++] = char.ToUpperInvariant(ch);
            }
        }

        return n == 0 ? "UNK" : new string(buf[..n]);
    }

    private static string SanitizeSide(string? side)
    {
        if (string.IsNullOrWhiteSpace(side))
        {
            return "X";
        }

        var t = side.Trim();
        if (t.Equals("BUY", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("B", StringComparison.OrdinalIgnoreCase))
        {
            return "B";
        }

        if (t.Equals("SELL", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("S", StringComparison.OrdinalIgnoreCase))
        {
            return "S";
        }

        var c = char.ToUpperInvariant(t[0]);
        return char.IsAsciiLetterOrDigit(c) ? c.ToString() : "X";
    }

    private static string EnsureFits(string id, string deterministicSeed)
    {
        if (id.Length <= MaxLength && MatchesAllowedPattern(id))
        {
            return id;
        }

        // Deterministic compact fallback (SHA256 hex) so Create stays pure.
        var hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(deterministicSeed)))
            .ToLowerInvariant();
        var compact = Prefix + hash;
        return compact.Length <= MaxLength ? compact : compact[..MaxLength];
    }

    private static bool MatchesAllowedPattern(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
