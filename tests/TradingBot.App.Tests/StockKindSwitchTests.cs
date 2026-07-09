using TradingBot.App.Services;
using TradingBot.App.ViewModels;
using TradingBot.Domain;

namespace TradingBot.App.Tests;

/// <summary>
/// Stock kind switch (비전마린/VMAR ↔ 스페이스X/SPCX) + harness presets.
/// Live submission stays locked — no live unlock coverage.
/// </summary>
public class StockKindSwitchTests
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
    public void SetStockKind_비전마린_focus_vmar_cers_preset_live_locked()
    {
        var harness = CreateTestHarness();
        // Force non-default first so the switch itself is exercised.
        harness.SetStockKind(StockMarketKind.스페이스X);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, harness.Session.ResolveFocusSymbol());

        harness.SetStockKind(StockMarketKind.비전마린);

        Assert.Equal(StockMarketKind.비전마린, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(new[] { WatchlistCatalog.VmarSymbol }, harness.Session.ResolveWatchSymbols());
        Assert.Equal(CersPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(CersPreset.Timeframe, harness.Session.Timeframe);
        Assert.Equal(TradingStrategyKind.CERS비용회귀, harness.Session.Strategy);
        Assert.Equal(ChartTimeframe.분봉1, harness.Session.Timeframe);
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
    }

    [Fact]
    public void SetStockKind_스페이스X_focus_spcx_official_preset_live_locked()
    {
        var harness = CreateTestHarness();
        Assert.Equal(StockMarketKind.비전마린, harness.Session.StockKind);

        harness.SetStockKind(StockMarketKind.스페이스X);

        Assert.Equal(StockMarketKind.스페이스X, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(new[] { WatchlistCatalog.SpaceXSymbol }, harness.Session.ResolveWatchSymbols());
        Assert.Equal(SpacexOfficialStrategyPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(SpacexOfficialStrategyPreset.Timeframe, harness.Session.Timeframe);
        Assert.Equal(TradingStrategyKind.추세추종, harness.Session.Strategy);
        Assert.Equal(ChartTimeframe.분봉15, harness.Session.Timeframe);
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
    }

    [Fact]
    public void SetStockKind_switch_twice_is_idempotent_and_stable()
    {
        var harness = CreateTestHarness();

        harness.SetStockKind(StockMarketKind.스페이스X);
        harness.SetStockKind(StockMarketKind.스페이스X);
        Assert.Equal(StockMarketKind.스페이스X, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(SpacexOfficialStrategyPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(SpacexOfficialStrategyPreset.Timeframe, harness.Session.Timeframe);
        Assert.False(harness.IsLiveSubmissionEnabled);

        harness.SetStockKind(StockMarketKind.비전마린);
        harness.SetStockKind(StockMarketKind.비전마린);
        Assert.Equal(StockMarketKind.비전마린, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(CersPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(CersPreset.Timeframe, harness.Session.Timeframe);
        Assert.False(harness.IsLiveSubmissionEnabled);

        // Round-trip VMAR → SPCX → VMAR → SPCX remains stable and live-locked.
        harness.SetStockKind(StockMarketKind.스페이스X);
        harness.SetStockKind(StockMarketKind.비전마린);
        harness.SetStockKind(StockMarketKind.스페이스X);
        Assert.Equal(StockMarketKind.스페이스X, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(new[] { WatchlistCatalog.SpaceXSymbol }, harness.Session.ResolveWatchSymbols());
        Assert.Equal(SpacexOfficialStrategyPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(SpacexOfficialStrategyPreset.Timeframe, harness.Session.Timeframe);
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(harness.GetEvidenceCounts().LiveBlocked);
    }

    [Fact]
    public void Watchlist_KindLabels_include_비전마린_and_스페이스X()
    {
        Assert.Equal(2, WatchlistCatalog.KindLabels.Count);
        Assert.Contains("비전마린", WatchlistCatalog.KindLabels);
        Assert.Contains("스페이스X", WatchlistCatalog.KindLabels);
        Assert.Equal(WatchlistCatalog.AllKinds.Count, WatchlistCatalog.KindLabels.Count);
        Assert.Equal(2, WatchlistCatalog.AllKinds.Count);
        Assert.Contains(StockMarketKind.비전마린, WatchlistCatalog.AllKinds);
        Assert.Contains(StockMarketKind.스페이스X, WatchlistCatalog.AllKinds);
    }

    [Fact]
    public void MainWindowViewModel_stock_kind_options_are_vmar_and_spcx_kinds()
    {
        var harness = CreateTestHarness();
        using var vm = new MainWindowViewModelDisposable(harness);

        Assert.Equal(2, vm.Inner.StockKindOptions.Count);
        Assert.Contains("비전마린", vm.Inner.StockKindOptions);
        Assert.Contains("스페이스X", vm.Inner.StockKindOptions);
        Assert.Equal(2, vm.Inner.StockKindChips.Count);
        Assert.Contains(vm.Inner.StockKindChips, c => c.TickerLabel == WatchlistCatalog.VmarSymbol);
        Assert.Contains(vm.Inner.StockKindChips, c => c.TickerLabel == WatchlistCatalog.SpaceXSymbol);
        Assert.Equal(WatchlistCatalog.VmarSymbol, vm.Inner.FocusSymbolPill);
        Assert.Equal("VMAR 뉴스", vm.Inner.NewsCardTitle);
        Assert.Contains("종목 전환", vm.Inner.StockSwitchHint, StringComparison.Ordinal);
        // Default practice path: VMAR focus, live locked.
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());
        Assert.False(harness.IsLiveSubmissionEnabled);
        Assert.True(vm.Inner.StockKindChips.Single(c => c.TickerLabel == "VMAR").IsSelected);
        Assert.False(vm.Inner.StockKindChips.Single(c => c.TickerLabel == "SPCX").IsSelected);
    }

    [Fact]
    public void MainWindowViewModel_select_spacex_kind_updates_focus_to_spcx()
    {
        var harness = CreateTestHarness();
        using var vm = new MainWindowViewModelDisposable(harness);

        Assert.Equal(StockMarketKind.비전마린, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.VmarSymbol, harness.Session.ResolveFocusSymbol());

        // Chip path: selecting SPCX kind label drives SetStockKind + panel/focus refresh.
        vm.Inner.SelectedStockKind = StockMarketKind.스페이스X.ToString();

        Assert.Equal(StockMarketKind.스페이스X, harness.Session.StockKind);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, harness.Session.ResolveFocusSymbol());
        Assert.Equal(SpacexOfficialStrategyPreset.Strategy, harness.Session.Strategy);
        Assert.Equal(SpacexOfficialStrategyPreset.Timeframe, harness.Session.Timeframe);
        // Focus surface (symbol pill / selected symbol) follows SPCX.
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, vm.Inner.SelectedSymbol);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, vm.Inner.FocusSymbolPill);
        Assert.Equal("SPCX 뉴스", vm.Inner.NewsCardTitle);
        Assert.Contains(WatchlistCatalog.SpaceXSymbol, vm.Inner.WatchSymbolsText, StringComparison.Ordinal);
        Assert.True(vm.Inner.StockKindChips.Single(c => c.TickerLabel == "SPCX").IsSelected);
        Assert.False(vm.Inner.StockKindChips.Single(c => c.TickerLabel == "VMAR").IsSelected);
        Assert.False(harness.IsLiveSubmissionEnabled);
    }

    /// <summary>
    /// Owns ViewModel lifetime for headless tests without Avalonia app host.
    /// Bootstrap fire-and-forget is ignored; we only assert sync selection state.
    /// </summary>
    private sealed class MainWindowViewModelDisposable : IDisposable
    {
        public MainWindowViewModel Inner { get; }

        public MainWindowViewModelDisposable(AppHarness harness)
        {
            Inner = new MainWindowViewModel(harness);
        }

        public void Dispose()
        {
            // No IDisposable on ViewModel; GC is fine for unit tests.
        }
    }
}
