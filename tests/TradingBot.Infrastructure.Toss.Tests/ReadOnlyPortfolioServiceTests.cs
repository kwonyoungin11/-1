using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Infrastructure.Toss.Http;

namespace TradingBot.Infrastructure.Toss.Tests;

public class ReadOnlyPortfolioServiceTests
{
    [Fact]
    public async Task Mock_service_returns_connected_snapshot_without_live_orders()
    {
        var svc = ReadOnlyPortfolioService.CreateMock();
        var snap = await svc.GetSnapshotAsync(new[] { "AAPL", "MSFT" }, CancellationToken.None);

        Assert.Equal(ConnectionStatus.MockConnected, snap.ConnectionStatus);
        Assert.NotEmpty(snap.Accounts);
        Assert.NotEmpty(snap.Holdings);
        Assert.Single(snap.Quotes);
        Assert.Equal(WatchlistCatalog.SpaceXSymbol, snap.Quotes[0].Symbol);
        Assert.Equal(3500.50m, snap.CashBuyingPower);
        Assert.Equal("USD", snap.CashCurrency);
        Assert.Equal(1500.25m, snap.MarketValueUsdDecimal);
        Assert.Contains(snap.BlockMessages, m => m.Contains("주문 API 미사용", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mock_service_accepts_vmar_in_watchlist()
    {
        var svc = ReadOnlyPortfolioService.CreateMock();
        var snap = await svc.GetSnapshotAsync(new[] { "VMAR" }, CancellationToken.None);

        Assert.Equal(ConnectionStatus.MockConnected, snap.ConnectionStatus);
        Assert.Single(snap.Quotes);
        Assert.Equal(WatchlistCatalog.VmarSymbol, snap.Quotes[0].Symbol);
        Assert.Contains(snap.BlockMessages, m => m.Contains("주문 API 미사용", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mock_service_accepts_spcx_and_vmar_together()
    {
        var svc = ReadOnlyPortfolioService.CreateMock();
        var snap = await svc.GetSnapshotAsync(new[] { "SPCX", "VMAR", "AAPL" }, CancellationToken.None);

        Assert.Equal(2, snap.Quotes.Count);
        Assert.Contains(snap.Quotes, q => q.Symbol == WatchlistCatalog.SpaceXSymbol);
        Assert.Contains(snap.Quotes, q => q.Symbol == WatchlistCatalog.VmarSymbol);
    }

    [Fact]
    public void Live_http_guard_blocks_when_flag_false()
    {
        var options = new TossOptions { AllowLiveHttp = false };
        Assert.Throws<InvalidOperationException>(() => LiveHttpGuard.EnsureAllowed(options));
    }

    [Fact]
    public void Order_client_never_enables_live_submission()
    {
        ITossOrderClient client = new BlockedTossOrderClient();
        Assert.False(client.IsLiveSubmissionEnabled);
    }

    [Fact]
    public void TossOptions_defaults_disallow_live_http()
    {
        var env = new Dictionary<string, string?>
        {
            ["TOSS_CLIENT_ID"] = "id",
            ["TOSS_CLIENT_SECRET"] = "secret",
        };
        var opt = TossOptions.FromEnvironment(env);
        Assert.False(opt.AllowLiveHttp);
        Assert.True(opt.HasClientCredentials);
    }
}
