using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class WatchlistCatalogTests
{
    [Fact]
    public void AllKinds_includes_spacex_and_vmar()
    {
        Assert.Equal(2, WatchlistCatalog.AllKinds.Count);
        Assert.Contains(StockMarketKind.스페이스X, WatchlistCatalog.AllKinds);
        Assert.Contains(StockMarketKind.비전마린, WatchlistCatalog.AllKinds);
    }

    [Fact]
    public void ResolveSymbols_spacex_is_spcx_only()
    {
        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.스페이스X);
        Assert.Single(symbols);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, symbols[0]);
        Assert.Equal("SPCX", symbols[0]);
    }

    [Fact]
    public void ResolveSymbols_vmar_is_vmar_only()
    {
        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.비전마린);
        Assert.Single(symbols);
        Assert.Equal(WatchlistCatalog.VmarSymbol, symbols[0]);
        Assert.Equal("VMAR", symbols[0]);
    }

    [Fact]
    public void KindLabels_match_AllKinds_count()
    {
        Assert.Equal(WatchlistCatalog.AllKinds.Count, WatchlistCatalog.KindLabels.Count);
        Assert.Contains("스페이스X", WatchlistCatalog.KindLabels);
        Assert.Contains("비전마린", WatchlistCatalog.KindLabels);
    }

    [Fact]
    public void Describe_mentions_spcx()
    {
        var description = WatchlistCatalog.Describe(StockMarketKind.스페이스X);
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Contains("SPCX", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_vmar_is_practice_not_advice()
    {
        var description = WatchlistCatalog.Describe(StockMarketKind.비전마린);
        Assert.Contains("VMAR", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("연습", description, StringComparison.Ordinal);
        Assert.Contains("투자 조언 아님", description, StringComparison.Ordinal);
        Assert.DoesNotContain("보장", description, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartSeedPrice_is_positive_for_known_and_unknown()
    {
        Assert.True(WatchlistCatalog.ChartSeedPrice("SPCX") > 0);
        Assert.True(WatchlistCatalog.ChartSeedPrice("VMAR") > 0);
        Assert.True(WatchlistCatalog.ChartSeedPrice("OTHER") > 0);
        // VMAR small-cap seed around ~3.5
        Assert.InRange(WatchlistCatalog.ChartSeedPrice("VMAR"), 1.0, 20.0);
    }

    [Fact]
    public void IsKnownSymbol_true_for_spcx_and_vmar_case_insensitive()
    {
        Assert.True(WatchlistCatalog.IsKnownSymbol("SPCX"));
        Assert.True(WatchlistCatalog.IsKnownSymbol("spcx"));
        Assert.True(WatchlistCatalog.IsKnownSymbol("VMAR"));
        Assert.True(WatchlistCatalog.IsKnownSymbol("vmar"));
        Assert.False(WatchlistCatalog.IsKnownSymbol("AAPL"));
        Assert.False(WatchlistCatalog.IsKnownSymbol(""));
        Assert.False(WatchlistCatalog.IsKnownSymbol(null));
    }

    [Fact]
    public void PrimarySymbol_remains_spcx_for_backward_compat()
    {
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, WatchlistCatalog.PrimarySymbol);
    }

    [Fact]
    public void NormalizeKnownSymbol_maps_case_and_rejects_unknown()
    {
        Assert.Equal("SPCX", WatchlistCatalog.NormalizeKnownSymbol("spcx"));
        Assert.Equal("VMAR", WatchlistCatalog.NormalizeKnownSymbol("vmar"));
        Assert.Null(WatchlistCatalog.NormalizeKnownSymbol("AAPL"));
        Assert.Null(WatchlistCatalog.NormalizeKnownSymbol(null));
        Assert.Null(WatchlistCatalog.NormalizeKnownSymbol(""));
    }
}
