using TradingBot.Domain;

namespace TradingBot.Runner.Tests;

public class TradingBotHarnessTests
{
    [Fact]
    public void CreateHarnessSafetySettings_IsFailClosedDryRun()
    {
        var settings = TradingBotHarness.CreateHarnessSafetySettings();

        Assert.False(settings.AllowLiveOrders);
        Assert.True(settings.KillSwitch);
        Assert.Equal(OrderMode.DryRun, settings.OrderMode);
    }

    [Fact]
    public async Task RunOnceAsync_BlocksLiveAndReturnsExitZero()
    {
        var harness = TradingBotHarness.CreateDefault();

        var result = await harness.RunOnceAsync(cancellationToken: CancellationToken.None);

        Assert.True(result.LiveSubmissionBlocked);
        Assert.False(result.IsLiveTradingVisuallyOpen);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.AuditEntryCount >= 1);
        Assert.False(string.IsNullOrWhiteSpace(result.SafetyHeadline));
        Assert.False(string.IsNullOrWhiteSpace(result.ConnectionSummary));
    }

    [Fact]
    public void HarnessRunResult_ExitCode_FailsOpenWhenLiveNotBlocked()
    {
        var open = new HarnessRunResult(
            SafetyHeadline: "x",
            ConnectionSummary: "y",
            CandidateCount: 0,
            DryRunLedgerCount: 0,
            RiskGateRowCount: 0,
            UiCandidateCount: 0,
            AuditEntryCount: 0,
            LiveSubmissionBlocked: false,
            IsLiveTradingVisuallyOpen: false);

        Assert.Equal(1, open.ExitCode);
    }
}
