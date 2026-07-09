using TradingBot.App.Services;
using TradingBot.App.ViewModels;
using TradingBot.Domain;

namespace TradingBot.App.Tests;

/// <summary>
/// CERS cockpit metrics on MainWindowViewModel (practice UI · live stays locked).
/// </summary>
public class MainWindowViewModelCersTests
{
    private static AppHarness CreateTestHarness()
    {
        Environment.SetEnvironmentVariable("ALLOW_LIVE_ORDERS", "false");
        Environment.SetEnvironmentVariable("KILL_SWITCH", "true");
        Environment.SetEnvironmentVariable("ORDER_MODE", "dry_run");
        Environment.SetEnvironmentVariable("TOSS_ALLOW_LIVE_HTTP", "false");
        return AppHarness.CreateDefault();
    }

    [Fact]
    public void SetStrategy_CERS_sets_IsCersStrategy_and_threshold_text()
    {
        var harness = CreateTestHarness();
        harness.SetStrategy(TradingStrategyKind.CERS비용회귀);

        var vm = new MainWindowViewModel(harness);

        Assert.True(vm.IsCersStrategy);
        Assert.Equal("0.0060", vm.CersThresholdText);
        Assert.Equal(CersPreset.Strategy.ToString(), vm.SelectedStrategy);
        Assert.Contains("CERS", vm.OfficialStrategyLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CERS", vm.StrategyDescription, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(vm.CersDetailText));
        Assert.Contains("max 40", vm.CersDetailText, StringComparison.Ordinal);
        Assert.Contains("1m", vm.CersDetailText, StringComparison.Ordinal);
        Assert.True(
            vm.CersStateText is "관망" or "진입가능" or "보유중",
            $"unexpected CersStateText: {vm.CersStateText}");
        Assert.False(string.IsNullOrWhiteSpace(vm.CersExpectedText));
        Assert.Contains("CERS", vm.IndicatorLegend, StringComparison.OrdinalIgnoreCase);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void NonCers_strategy_hides_cers_metrics()
    {
        var harness = CreateTestHarness();
        // Ctor defaults non-live to CERS; switch strategy after bind to hide metrics.
        var vm = new MainWindowViewModel(harness);
        Assert.True(vm.IsCersStrategy);

        vm.SelectedStrategy = TradingStrategyKind.일분분할스캘프.ToString();

        Assert.False(vm.IsCersStrategy);
        Assert.Equal(TradingStrategyKind.일분분할스캘프.ToString(), vm.SelectedStrategy);
        Assert.Equal(TradingStrategyKind.일분분할스캘프, harness.Session.Strategy);
    }

    [Fact]
    public void StrategyOptions_include_CERS비용회귀()
    {
        var harness = CreateTestHarness();
        var vm = new MainWindowViewModel(harness);
        Assert.Contains(TradingStrategyKind.CERS비용회귀.ToString(), vm.StrategyOptions);
    }
}
