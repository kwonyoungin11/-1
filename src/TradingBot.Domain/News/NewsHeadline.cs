namespace TradingBot.Domain;

/// <summary>
/// Owner-facing news/disclosure headline (display + gate flags only).
/// Not investment advice. Do not auto-trade on sentiment.
/// </summary>
public sealed record NewsHeadline(
    string Id,
    string Title,
    string Source,
    DateTimeOffset PublishedAtUtc,
    string? Url,
    bool IsMaterialEvent,
    string? SymbolHint)
{
    public string PublishedKstLabel =>
        KoreaTime.FormatFull(PublishedAtUtc);
}

/// <summary>Read-only news feed for cockpit (poll/push adapter).</summary>
public interface INewsFeed
{
    Task<IReadOnlyList<NewsHeadline>> GetHeadlinesAsync(
        string symbol,
        int maxCount,
        CancellationToken cancellationToken);
}
