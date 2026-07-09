using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.Ui.Tests;

public class CockpitDashboardModelTests
{
    [Fact]
    public void Safe_default_keeps_live_closed_and_no_live_candidates()
    {
        var dash = CockpitDashboardModel.CreateSafeDefault();

        Assert.Equal(LiveLockState.Locked, dash.LiveLock);
        Assert.False(dash.IsLiveTradingVisuallyOpen);
        Assert.True(dash.Snapshot.KillSwitchActive);
        Assert.False(dash.Snapshot.AllowLiveOrders);
        Assert.Equal(OrderMode.DryRun, dash.Snapshot.OrderMode);
        Assert.Contains("실거래 잠김", dash.SafetyHeadline, StringComparison.Ordinal);
        Assert.Empty(dash.OrderCandidates);
        Assert.True(dash.RiskGates.Count >= 1);
        Assert.All(dash.RiskGates.Where(r => r.Code is "kill_switch_active" or "live_orders_not_allowed" or "order_mode_not_live" or "live_lock"),
            r => Assert.False(r.Passed));
    }

    [Fact]
    public void Candidate_rows_never_have_IsLive_true()
    {
        var evaluated = new EvaluatedOrderCandidate(
            Candidate: new OrderCandidate(
                Symbol: "AAPL",
                Side: "BUY",
                OrderType: "LIMIT",
                Quantity: 1m,
                LimitPrice: 100m,
                ClientOrderId: "dry-AAPL-1",
                CreatedAtUtc: DateTimeOffset.UtcNow),
            Signal: new StrategySignal(
                Symbol: "AAPL",
                Side: SignalSide.Buy,
                SuggestedQuantity: 1m,
                ReferencePrice: 100m,
                StrategyName: "test",
                OwnerMessage: "test signal",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                IsActionable: true),
            Risk: RiskDecision.Allow(),
            OwnerStatusMessage: "dry-run 후보 허용 (실주문 아님)");

        var row = CockpitDashboardMapper.FromEvaluated(evaluated);
        Assert.False(row.IsLive);

        var blocked = evaluated with
        {
            Risk = RiskDecision.Block(BlockedReason.MissingData),
            OwnerStatusMessage = "risk 차단: missing_data",
        };
        var blockedRow = CockpitDashboardMapper.FromEvaluated(blocked);
        Assert.False(blockedRow.IsLive);

        var list = CockpitDashboardMapper.MapCandidates(new[] { evaluated, blocked });
        Assert.All(list, r => Assert.False(r.IsLive));
    }

    [Fact]
    public void MapCandidates_null_or_empty_is_empty_never_live()
    {
        Assert.Empty(CockpitDashboardMapper.MapCandidates(null));
        Assert.Empty(CockpitDashboardMapper.MapCandidates(Array.Empty<EvaluatedOrderCandidate>()));
    }

    [Fact]
    public void MapRiskDecision_null_is_fail_closed()
    {
        var rows = CockpitDashboardMapper.MapRiskDecision(null);
        Assert.Single(rows);
        Assert.False(rows[0].Passed);
        Assert.Equal(BlockedReason.UnknownState.Code, rows[0].Code);
    }

    [Fact]
    public void MapRiskDecision_blocks_become_failed_rows_with_owner_message()
    {
        var decision = RiskDecision.Block(
            BlockedReason.KillSwitchActive,
            BlockedReason.StaleMarketData);

        var rows = CockpitDashboardMapper.MapRiskDecision(decision);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.False(r.Passed));
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.OwnerMessage)));
        Assert.Contains(rows, r => r.Code == BlockedReason.KillSwitchActive.Code);
        Assert.Contains(rows, r => r.Code == BlockedReason.StaleMarketData.Code);
    }

    [Fact]
    public void Compose_syncs_candidate_count_and_keeps_live_lock_from_snapshot()
    {
        var snap = CockpitSnapshot.CreateSafeDefault();
        var evaluated = new EvaluatedOrderCandidate(
            Candidate: new OrderCandidate(
                "MSFT", "SELL", "LIMIT", 2m, 50m, "dry-MSFT-1", DateTimeOffset.UtcNow),
            Signal: new StrategySignal(
                "MSFT", SignalSide.Sell, 2m, 50m, "test", "msg", DateTimeOffset.UtcNow, true),
            Risk: RiskDecision.Block(BlockedReason.MarketSessionClosed),
            OwnerStatusMessage: "risk 차단: market_session_closed");

        var dash = CockpitDashboardMapper.Compose(
            snap,
            TradingSafetySettings.CreateSafeDefaults(),
            new[] { evaluated });

        Assert.Equal(LiveLockState.Locked, dash.Snapshot.LiveLock);
        Assert.False(dash.IsLiveTradingVisuallyOpen);
        Assert.Equal(1, dash.Snapshot.OrderCandidateCount);
        Assert.Single(dash.OrderCandidates);
        Assert.Equal("MSFT", dash.OrderCandidates[0].Symbol);
        Assert.Equal("SELL", dash.OrderCandidates[0].Side);
        Assert.Equal(2m, dash.OrderCandidates[0].Quantity);
        Assert.False(dash.OrderCandidates[0].IsLive);
        Assert.Contains(dash.RiskGates, r => r.Code == "live_lock" && !r.Passed);
    }

    [Fact]
    public void FromEvaluated_maps_symbol_side_qty_status()
    {
        var evaluated = new EvaluatedOrderCandidate(
            Candidate: new OrderCandidate(
                "NVDA", "BUY", "LIMIT", 3m, 120.5m, "dry-NVDA-9", DateTimeOffset.UtcNow),
            Signal: new StrategySignal(
                "NVDA", SignalSide.Buy, 3m, 120.5m, "test", "msg", DateTimeOffset.UtcNow, true),
            Risk: RiskDecision.Allow(),
            OwnerStatusMessage: "dry-run 후보 허용 (실주문 아님)");

        var row = CockpitDashboardMapper.FromEvaluated(evaluated);
        Assert.Equal("NVDA", row.Symbol);
        Assert.Equal("BUY", row.Side);
        Assert.Equal(3m, row.Quantity);
        Assert.Equal(120.5m, row.LimitPrice);
        Assert.Equal("dry-NVDA-9", row.ClientOrderId);
        Assert.Equal("dry-run 후보 허용 (실주문 아님)", row.Status);
        Assert.False(row.IsLive);
    }
}
