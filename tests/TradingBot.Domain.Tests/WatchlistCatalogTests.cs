using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class WatchlistCatalogTests
{
    [Fact]
    public void AllKinds_is_non_empty_and_covers_known_markets()
    {
        Assert.NotEmpty(WatchlistCatalog.AllKinds);
        Assert.True(WatchlistCatalog.AllKinds.Count >= 5);
        Assert.Contains(StockMarketKind.나스닥, WatchlistCatalog.AllKinds);
        Assert.Contains(StockMarketKind.나스닥테크, WatchlistCatalog.AllKinds);
        Assert.Contains(StockMarketKind.미국주식, WatchlistCatalog.AllKinds);
        Assert.Contains(StockMarketKind.미국ETF, WatchlistCatalog.AllKinds);
        Assert.Contains(StockMarketKind.국내주식, WatchlistCatalog.AllKinds);
    }

    [Fact]
    public void Every_kind_has_non_empty_symbols()
    {
        foreach (var kind in WatchlistCatalog.AllKinds)
        {
            var symbols = WatchlistCatalog.ResolveSymbols(kind);
            Assert.False(symbols is null, $"ResolveSymbols({kind}) returned null");
            Assert.NotEmpty(symbols);
            foreach (var symbol in symbols)
            {
                Assert.False(string.IsNullOrWhiteSpace(symbol), $"Empty symbol for kind {kind}");
            }
        }
    }

    [Fact]
    public void KindLabels_match_AllKinds_count()
    {
        Assert.Equal(WatchlistCatalog.AllKinds.Count, WatchlistCatalog.KindLabels.Count);
        Assert.All(WatchlistCatalog.KindLabels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
    }

    [Fact]
    public void Describe_is_non_empty_for_every_kind()
    {
        foreach (var kind in WatchlistCatalog.AllKinds)
        {
            var description = WatchlistCatalog.Describe(kind);
            Assert.False(string.IsNullOrWhiteSpace(description), $"Describe({kind}) empty");
        }
    }

    [Fact]
    public void ChartSeedPrice_is_positive_for_catalog_symbols()
    {
        foreach (var kind in WatchlistCatalog.AllKinds)
        {
            foreach (var symbol in WatchlistCatalog.ResolveSymbols(kind))
            {
                var seed = WatchlistCatalog.ChartSeedPrice(symbol);
                Assert.True(seed > 0, $"ChartSeedPrice({symbol}) must be > 0, was {seed}");
            }
        }
    }

    [Theory]
    [InlineData(StockMarketKind.나스닥, "NVDA")]
    [InlineData(StockMarketKind.나스닥테크, "AMD")]
    [InlineData(StockMarketKind.미국ETF, "SPY")]
    [InlineData(StockMarketKind.국내주식, "005930")]
    public void ResolveSymbols_includes_expected_seed_symbol(StockMarketKind kind, string expected)
    {
        Assert.Contains(expected, WatchlistCatalog.ResolveSymbols(kind));
    }
}
