using Microsoft.Extensions.DependencyInjection;
using TradingBot.Web.Services;

namespace TradingBot.Web.Tests;

public class WebHarnessEvidenceTests
{
    [Fact]
    public async Task GetEvidenceSummaryAsync_reports_live_blocked_from_real_harness()
    {
        var services = new ServiceCollection();
        services.AddTradingBotCockpit();
        await using var sp = services.BuildServiceProvider();
        var harness = sp.GetRequiredService<WebHarness>();

        _ = await harness.GetDashboardAsync();
        var evidence = await harness.GetEvidenceSummaryAsync();

        Assert.True(evidence.LiveBlocked);
        Assert.True(evidence.DryRunCount >= 0);
        Assert.True(evidence.PaperFillCount >= 0);
        Assert.DoesNotContain(
            evidence.LastModes,
            m => m.Equals("Live", StringComparison.OrdinalIgnoreCase));
    }
}
