using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class StrategySolidDomainChecksTests
{
    [Fact]
    public void Core3_universe_is_ok()
    {
        Assert.True(StrategySolidDomainChecks.IsCore3UniverseOk());
        var symbols = WatchlistCatalog.ResolveSymbols(StockMarketKind.나스닥코어3);
        Assert.Equal(3, symbols.Count);
        Assert.Contains("QQQ", symbols);
        Assert.Contains("NVDA", symbols);
        Assert.Contains("AAPL", symbols);
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
}
