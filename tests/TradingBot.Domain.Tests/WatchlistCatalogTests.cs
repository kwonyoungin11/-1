using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class WatchlistCatalogTests
{
    [Fact]
    public void AllKinds_is_spacex_only()
    {
        Assert.Single(WatchlistCatalog.AllKinds);
        Assert.Equal(StockMarketKind.스페이스X, WatchlistCatalog.AllKinds[0]);
    }

    [Fact]
    public void ResolveSymbols_is_spcx_only()
    {
        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.스페이스X);
        Assert.Single(symbols);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, symbols[0]);
    }

    [Fact]
    public void KindLabels_match_AllKinds_count()
    {
        Assert.Equal(WatchlistCatalog.AllKinds.Count, WatchlistCatalog.KindLabels.Count);
        Assert.Contains("스페이스X", WatchlistCatalog.KindLabels);
    }

    [Fact]
    public void Describe_mentions_spcx()
    {
        var description = WatchlistCatalog.Describe(StockMarketKind.스페이스X);
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Contains("SPCX", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChartSeedPrice_is_positive_for_spcx()
    {
        Assert.True(WatchlistCatalog.ChartSeedPrice("SPCX") > 0);
        Assert.True(WatchlistCatalog.ChartSeedPrice("OTHER") > 0);
    }
}
