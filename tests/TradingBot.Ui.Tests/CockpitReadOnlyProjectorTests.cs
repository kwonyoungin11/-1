using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.Ui.Tests;

public class CockpitReadOnlyProjectorTests
{
    [Fact]
    public void Project_mock_snapshot_keeps_live_locked()
    {
        var portfolio = new ReadOnlyPortfolioSnapshot
        {
            ConnectionStatus = ConnectionStatus.MockConnected,
            ConnectionOwnerMessage = "mock ok",
            Accounts = new[] { new AccountSummary("1", "****7890", "위탁") },
            Holdings = Array.Empty<HoldingSummary>(),
            Quotes = Array.Empty<QuoteSnapshot>(),
            UsMarket = new UsMarketSessionSnapshot("2026-07-09", false, "정규장 mock"),
            MarketValueUsdSummary = "100",
            AsOfUtc = DateTimeOffset.UtcNow,
            BlockMessages = new[] { "read-only" },
        };

        var safety = TradingSafetySettings.CreateSafeDefaults();
        var cockpit = CockpitReadOnlyProjector.Project(portfolio, safety);

        Assert.Equal(LiveLockState.Locked, cockpit.LiveLock);
        Assert.False(cockpit.IsLiveTradingVisuallyOpen);
        Assert.Equal(BotLifecycleState.ReadOnlyConnected, cockpit.BotState);
        Assert.Contains("****7890", cockpit.AccountMaskedSummary, StringComparison.Ordinal);
    }
}
