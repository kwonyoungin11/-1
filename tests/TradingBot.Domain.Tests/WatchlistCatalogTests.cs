using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class WatchlistCatalogTests
{
    [Fact]
    public void AllKinds_is_non_empty_and_covers_known_markets()
    {
        Assert.NotEmpty(WatchlistCatalog.AllKinds);
        Assert.True(WatchlistCatalog.AllKinds.Count >= 6);
        Assert.Contains(StockMarketKind.나스닥, WatchlistCatalog.AllKinds);
        Assert.Contains(StockMarketKind.나스닥테크, WatchlistCatalog.AllKinds);
        Assert.Contains(StockMarketKind.나스닥코어3, WatchlistCatalog.AllKinds);
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
    [InlineData(StockMarketKind.나스닥코어3, "QQQ")]
    [InlineData(StockMarketKind.미국ETF, "SPY")]
    [InlineData(StockMarketKind.국내주식, "005930")]
    public void ResolveSymbols_includes_expected_seed_symbol(StockMarketKind kind, string expected)
    {
        Assert.Contains(expected, WatchlistCatalog.ResolveSymbols(kind));
    }

    [Fact]
    public void 나스닥코어3_ResolveSymbols_returns_exactly_QQQ_NVDA_AAPL()
    {
        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.나스닥코어3);
        Assert.Equal(3, symbols.Count);
        Assert.Equal(["QQQ", "NVDA", "AAPL"], symbols);
    }

    [Fact]
    public void 나스닥코어3_labels_and_describe_work()
    {
        Assert.Contains(StockMarketKind.나스닥코어3.ToString(), WatchlistCatalog.KindLabels);
        var description = WatchlistCatalog.Describe(StockMarketKind.나스닥코어3);
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Contains("코어", description);
        Assert.Contains("QQQ", description);
    }

    [Fact]
    public void 나스닥코어3_ChartSeedPrice_is_positive_for_all_three()
    {
        foreach (var symbol in WatchlistCatalog.ResolveSymbols(StockMarketKind.나스닥코어3))
        {
            var seed = WatchlistCatalog.ChartSeedPrice(symbol);
            Assert.True(seed > 0, $"ChartSeedPrice({symbol}) must be > 0, was {seed}");
        }

        // QQQ explicit seed (ETF basket component of core3)
        Assert.Equal(470, WatchlistCatalog.ChartSeedPrice("QQQ"));
    }
}
