using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class StrategySolidDomainChecksTests
{
    [Fact]
    public void Spacex_universe_is_ok()
    {
        Assert.True(StrategySolidDomainChecks.IsSpacexUniverseOk());
        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.스페이스X);
        Assert.Single(symbols);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, symbols[0]);
    }

    [Fact]
    public void Core3_alias_maps_to_spacex()
    {
        Assert.True(StrategySolidDomainChecks.IsCore3UniverseOk());
    }

    [Fact]
    public void PositionRiskSizer_known_vector_is_500()
    {
        Assert.True(StrategySolidDomainChecks.IsPositionRiskSizerOk());
        var result = PositionRiskSizer.Calculate(100_000m, 1m, 2m, 100m);
        Assert.Equal(500m, result.Quantity);
    }

    [Fact]
    public void TrendFollowParameters_safe_defaults_are_ok()
    {
        Assert.True(StrategySolidDomainChecks.IsTrendFollowParametersOk());
        Assert.NotNull(TrendFollowParameters.CreateSafeDefaults());
    }

    [Fact]
    public void AllDomainChecksOk_is_true_on_current_codebase()
    {
        Assert.True(StrategySolidDomainChecks.AllDomainChecksOk());
    }

    [Fact]
    public void ChartIndicator_SMA_and_strategy_overlays()
    {
        var candles = Enumerable.Range(0, 80)
            .Select(i => new CandlePoint(
                DateTimeOffset.UtcNow.AddMinutes(i - 80),
                100 + i * 0.1,
                101 + i * 0.1,
                99 + i * 0.1,
                100.5 + i * 0.1,
                1_000))
            .ToList();

        var trend = ChartIndicatorCalculator.ForStrategy(candles, TradingStrategyKind.추세추종);
        Assert.Equal(2, trend.Count);
        Assert.Contains(trend, l => l.Name == "SMA20");
        Assert.Contains(trend, l => l.Name == "SMA60");

        var mean = ChartIndicatorCalculator.ForStrategy(candles, TradingStrategyKind.평균회귀);
        Assert.Equal(3, mean.Count);

        var sma = ChartIndicatorCalculator.Sma(candles.Select(c => c.Close).ToArray(), 5);
        Assert.Null(sma[3]);
        Assert.NotNull(sma[4]);
    }

    [Fact]
    public void ChartTimeframe_maps_to_toss_source_interval()
    {
        Assert.Equal("1m", ChartTimeframeCatalog.SourceTossInterval(ChartTimeframe.분봉1));
        Assert.Equal("1m", ChartTimeframeCatalog.SourceTossInterval(ChartTimeframe.분봉15));
        Assert.Equal("1d", ChartTimeframeCatalog.SourceTossInterval(ChartTimeframe.일봉));
        Assert.Equal("1d", ChartTimeframeCatalog.SourceTossInterval(ChartTimeframe.주봉));
        Assert.True(ChartTimeframeCatalog.TryParse("1W", out var w));
        Assert.Equal(ChartTimeframe.주봉, w);
    }
}
