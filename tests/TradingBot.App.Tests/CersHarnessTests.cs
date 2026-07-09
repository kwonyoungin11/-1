using TradingBot.App.Services;
using TradingBot.Domain;

namespace TradingBot.App.Tests;

/// <summary>
/// CERS harness wiring: default preset, stock-kind switch, bracket SL 1.2%, practice candles.
/// Practice only · live remains locked. Not investment advice.
/// </summary>
public class CersHarnessTests
{
    /// <summary>Process env overrides repo .env so unit tests stay fail-closed.</summary>
    private static AppHarness CreateTestHarness()
    {
        Environment.SetEnvironmentVariable("ALLOW_LIVE_ORDERS", "false");
        Environment.SetEnvironmentVariable("KILL_SWITCH", "true");
        Environment.SetEnvironmentVariable("ORDER_MODE", "dry_run");
        Environment.SetEnvironmentVariable("TOSS_ALLOW_LIVE_HTTP", "false");
        return AppHarness.CreateDefault();
    }

    [Fact]
    public void CreateDefault_non_live_forces_CERS_and_분봉1()
    {
        var harness = CreateTestHarness();

        Assert.Equal(StockMarketKind.비전마린, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(CersPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(TradingStrategyKind.CERS비용회귀, harness.Session.Strategy);
        Assert.Equal(CersPreset.Timeframe, harness.Session.Timeframe);
        Assert.Equal(ChartTimeframe.분봉1, harness.Session.Timeframe);
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
    }

    [Fact]
    public void SetStrategy_CERS_auto_sets_timeframe_분봉1()
    {
        var harness = CreateTestHarness();
        harness.SetTimeframe(ChartTimeframe.일봉);
        Assert.Equal(ChartTimeframe.일봉, harness.Timeframe);

        harness.SetStrategy(TradingStrategyKind.CERS비용회귀);

        Assert.Equal(TradingStrategyKind.CERS비용회귀, harness.Session.Strategy);
        Assert.Equal(ChartTimeframe.분봉1, harness.Session.Timeframe);
        Assert.Equal(CersPreset.Timeframe, harness.Timeframe);
    }

    [Fact]
    public void SetCersPreset_sets_strategy_and_1m_timeframe()
    {
        var harness = CreateTestHarness();
        harness.SetStockKind(StockMarketKind.스페이스X);
        Assert.Equal(SpacexOfficialStrategyPreset.Strategy, harness.Session.Strategy);

        harness.SetCersPreset();

        Assert.Equal(CersPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(CersPreset.Timeframe, harness.Session.Timeframe);
        Assert.Equal(ChartTimeframe.분봉1, harness.Timeframe);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void SetStockKind_비전마린_applies_CersPreset_not_scalp()
    {
        var harness = CreateTestHarness();
        harness.SetStockKind(StockMarketKind.스페이스X);
        harness.SetStockKind(StockMarketKind.비전마린);

        Assert.Equal(StockMarketKind.비전마린, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(CersPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(CersPreset.Timeframe, harness.Session.Timeframe);
        Assert.NotEqual(TradingStrategyKind.일분분할스캘프, harness.Session.Strategy);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void GetActiveBracketPlan_CERS_stop_is_1_2_percent_of_entry()
    {
        var harness = CreateTestHarness();
        harness.SetCersPreset();

        var plan = harness.GetActiveBracketPlan();

        Assert.Equal(WatchlistCatalog.VmarSymbol, plan.Symbol);
        Assert.Equal("LIMIT", plan.OrderType);
        Assert.True(plan.IsValid, plan.OwnerMessage);
        Assert.True(plan.EntryLimit > 0m);
        // stop = entry * (1 - 0.012) ≈ entry * 0.988
        var expectedStop = plan.EntryLimit * (1m - (decimal)CersPreset.StopLossPct);
        Assert.Equal(expectedStop, plan.StopPrice);
        Assert.True(plan.StopPrice < plan.EntryLimit);
        Assert.True(
            plan.TakeProfitPrice > plan.EntryLimit || !plan.IsValid,
            "TP above entry when plan valid");
        Assert.Equal(BracketStopSource.Percent, plan.StopSource);
        Assert.Contains("실주문", plan.OwnerMessage, StringComparison.Ordinal);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void BuildPracticeContext_includes_chart_candles()
    {
        var harness = CreateTestHarness();
        harness.SetCersPreset();

        var practice = harness.BuildPracticeContext();
        Assert.NotNull(practice.Candles);
        Assert.True(practice.Candles.Count > 0);
        Assert.True(practice.Candles.Count <= AppHarness.ChartDisplayBarCap);
        Assert.True(practice.Candles[^1].Close > 0);

        // Cap and non-empty match GetChartData structural shape (mock series is time-seeded).
        var (chartCandles, _, _) = harness.GetChartData();
        Assert.Equal(practice.Candles.Count, chartCandles.Count);
        Assert.True(chartCandles.Count <= AppHarness.ChartDisplayBarCap);
    }

    [Fact]
    public void BuildPracticeContext_candles_match_GetChartData_cap()
    {
        var harness = CreateTestHarness();
        var practice = harness.BuildPracticeContext();
        var (candles, _, _) = harness.GetChartData();

        Assert.NotNull(practice.Candles);
        Assert.Equal(candles.Count, practice.Candles!.Count);
        Assert.True(practice.Candles.Count <= AppHarness.ChartDisplayBarCap);
        Assert.True(practice.Candles.Count >= 10);
    }
}
