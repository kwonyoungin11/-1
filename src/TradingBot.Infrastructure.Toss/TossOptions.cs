namespace TradingBot.Infrastructure.Toss;

/// <summary>
/// Toss Open API settings. AllowLiveHttp defaults false: mock-only until owner enables read-only HTTP.
/// Never logs secret values.
/// </summary>
public sealed class TossOptions
{
    public const string SectionName = "Toss";

    public string BaseUrl { get; init; } = "https://openapi.tossinvest.com";
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? AccountSeq { get; init; }

    /// <summary>When false (default), only mock clients may be used — no outbound Toss HTTP.</summary>
    public bool AllowLiveHttp { get; init; }

    public static TossOptions FromEnvironment(IDictionary<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(env);

        static string? Get(IDictionary<string, string?> e, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (e.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                {
                    return v.Trim();
                }
            }

            return null;
        }

        static bool GetBool(IDictionary<string, string?> e, string key, bool defaultValue)
        {
            if (!e.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
            {
                return defaultValue;
            }

            return v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                   || v.Trim() == "1";
        }

        return new TossOptions
        {
            BaseUrl = Get(env, "TOSS_API_BASE_URL") ?? "https://openapi.tossinvest.com",
            ClientId = Get(env, "TOSS_CLIENT_ID", "TOSSINVEST_CLIENT_ID"),
            ClientSecret = Get(env, "TOSS_CLIENT_SECRET", "TOSSINVEST_CLIENT_SECRET"),
            AccountSeq = Get(env, "TOSS_ACCOUNT_SEQ", "TOSSINVEST_ACCOUNT_SEQ"),
            AllowLiveHttp = GetBool(env, "TOSS_ALLOW_LIVE_HTTP", defaultValue: false),
        };
    }

    public bool HasClientCredentials =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
