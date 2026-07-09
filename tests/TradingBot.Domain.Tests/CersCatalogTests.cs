using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class CersCatalogTests
{
    [Fact]
    public void Cers_strategy_is_in_catalog_with_description_and_positive_base_quantity()
    {
        Assert.Contains(TradingStrategyKind.CERS비용회귀, StrategyCatalog.All);

        var description = StrategyCatalog.Describe(TradingStrategyKind.CERS비용회귀);
        Assert.False(string.IsNullOrWhiteSpace(description));

        var qty = StrategyCatalog.BaseQuantity(TradingStrategyKind.CERS비용회귀);
        Assert.True(qty > 0m, "BaseQuantity must be > 0 for CERS");
    }
}
