using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Risk.Tests;

public class OrderCandidateRiskTests
{
    private static TradingSafetySettings Settings(
        decimal? maxNotional = null,
        decimal? maxPos = null,
        decimal? maxDailyLoss = null,
        int stale = 5) => new()
    {
        AllowLiveOrders = false,
        KillSwitch = true,
        OrderMode = OrderMode.DryRun,
        MaxOrderNotional = maxNotional,
        MaxPositionSize = maxPos,
        MaxDailyLoss = maxDailyLoss,
        MarketDataMaxStalenessSeconds = stale,
    };

    private static CandidateRiskContext OkContext(DateTimeOffset now) => new()
    {
        Symbol = "AAPL",
        Quantity = 1,
        LimitPrice = 100m,
        QuoteTimestampUtc = now,
        NowUtc = now,
        MarketSessionOpen = true,
        MarketSessionKnown = true,
    };

    [Fact]
    public void Fresh_quote_within_limits_allows_candidate()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxNotional: 1000m), OkContext(now));
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Stale_quote_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { QuoteTimestampUtc = now.AddSeconds(-30) };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(stale: 5), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.StaleMarketData.Code);
    }

    [Fact]
    public void Missing_price_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { LimitPrice = null, HasMissingData = true };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
    }

    [Fact]
    public void Notional_limit_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { Quantity = 10, LimitPrice = 100m }; // 1000
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxNotional: 500m), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MaxOrderNotionalExceeded.Code);
    }

    [Fact]
    public void Closed_market_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { MarketSessionOpen = false };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
    }

    [Fact]
    public void Live_submission_still_blocked_by_defaults()
    {
        var decision = new LiveOrderGate().Evaluate(
            TradingSafetySettings.CreateSafeDefaults(),
            new LiveOrderContext());
        Assert.True(decision.IsBlocked);
    }

    // --- notional edge cases ---

    [Fact]
    public void Notional_exactly_at_max_allows()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { Quantity = 5, LimitPrice = 100m }; // 500
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxNotional: 500m), ctx);
        Assert.True(decision.Allowed);
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.MaxOrderNotionalExceeded.Code);
    }

    [Fact]
    public void Notional_one_tick_over_max_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { Quantity = 5, LimitPrice = 100.01m }; // 500.05
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxNotional: 500m), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MaxOrderNotionalExceeded.Code);
    }

    [Fact]
    public void Null_max_notional_does_not_apply_notional_block()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { Quantity = 1_000_000, LimitPrice = 500m };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxNotional: null), ctx);
        Assert.True(decision.Allowed);
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.MaxOrderNotionalExceeded.Code);
    }

    // --- stale / quote edge cases ---

    [Fact]
    public void Quote_age_exactly_at_max_staleness_allows()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { QuoteTimestampUtc = now.AddSeconds(-5) };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(stale: 5), ctx);
        Assert.True(decision.Allowed);
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.StaleMarketData.Code);
    }

    [Fact]
    public void Quote_age_just_over_max_staleness_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        // 5.001 seconds > 5
        var ctx = OkContext(now) with { QuoteTimestampUtc = now.AddMilliseconds(-5001) };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(stale: 5), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.StaleMarketData.Code);
    }

    [Fact]
    public void Future_quote_timestamp_blocks_as_stale()
    {
        // age < 0 → fail-closed stale (clock skew / bad data)
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { QuoteTimestampUtc = now.AddSeconds(10) };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(stale: 60), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.StaleMarketData.Code);
    }

    [Fact]
    public void Missing_quote_timestamp_blocks_as_missing_data()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { QuoteTimestampUtc = null };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.StaleMarketData.Code);
    }

    // --- missing data edge cases ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_symbol_blocks_as_missing_data(string? symbol)
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { Symbol = symbol! };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Non_positive_quantity_blocks_as_missing_data(decimal qty)
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { Quantity = qty };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.0001)]
    public void Non_positive_limit_price_blocks_as_missing_data(decimal price)
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { LimitPrice = price };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
    }

    [Fact]
    public void Null_limit_price_without_HasMissingData_flag_still_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { LimitPrice = null, HasMissingData = false };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
    }

    [Fact]
    public void HasMissingData_flag_alone_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { HasMissingData = true };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
    }

    [Fact]
    public void Unknown_state_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { HasUnknownState = true };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.UnknownState.Code);
    }

    [Fact]
    public void Api_error_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { HasApiError = true };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.ApiError.Code);
    }

    // --- position size ---

    [Fact]
    public void Max_position_size_exceeded_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with
        {
            Quantity = 6,
            CurrentPositionQuantity = 5m, // projected 11
        };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxPos: 10m), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MaxPositionSizeExceeded.Code);
    }

    [Fact]
    public void Max_position_size_exactly_at_limit_allows()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with
        {
            Quantity = 5,
            CurrentPositionQuantity = 5m, // projected 10
        };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxPos: 10m), ctx);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Null_current_position_treated_as_zero_for_max_position()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with
        {
            Quantity = 11,
            CurrentPositionQuantity = null,
        };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxPos: 10m), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MaxPositionSizeExceeded.Code);
    }

    // --- combined / null guards ---

    [Fact]
    public void Multiple_fail_closed_conditions_accumulate_blocks()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with
        {
            Symbol = "",
            Quantity = 0,
            LimitPrice = null,
            QuoteTimestampUtc = null,
            HasUnknownState = true,
            HasApiError = true,
            MarketSessionOpen = false,
            MarketSessionKnown = false,
        };

        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxNotional: 1m), ctx);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.UnknownState.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MissingData.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.ApiError.Code);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
        // Missing quote → MissingData (already present); no notional when price/qty invalid
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.MaxOrderNotionalExceeded.Code);
    }

    [Fact]
    public void Null_settings_throws()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        Assert.Throws<ArgumentNullException>(() =>
            new RiskGate().EvaluateOrderCandidate(null!, OkContext(now)));
    }

    [Fact]
    public void Null_context_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RiskGate().EvaluateOrderCandidate(Settings(), null!));
    }

    [Fact]
    public void Kill_switch_does_not_block_dry_run_candidate_by_itself()
    {
        // Documented in RiskGate: kill switch does not block dry-run candidates alone.
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = false,
            KillSwitch = true,
            OrderMode = OrderMode.DryRun,
            MaxOrderNotional = 10_000m,
            MarketDataMaxStalenessSeconds = 60,
        };

        var decision = new RiskGate().EvaluateOrderCandidate(settings, OkContext(now));
        Assert.True(decision.Allowed);
    }

    // --- daily loss (optional MaxDailyLoss) ---

    [Fact]
    public void Max_daily_loss_not_configured_skips_daily_loss_check()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        // Equity would breach a 1k limit if configured; without MaxDailyLoss, allow.
        var ctx = OkContext(now) with
        {
            DayStartEquity = 100_000m,
            CurrentEquity = 90_000m,
        };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxDailyLoss: null), ctx);
        Assert.True(decision.Allowed);
        Assert.DoesNotContain(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitExceeded.Code);
    }

    [Fact]
    public void Max_daily_loss_exceeded_blocks_candidate()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with
        {
            DayStartEquity = 100_000m,
            CurrentEquity = 98_500m, // loss 1500
        };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxDailyLoss: 1_000m), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitExceeded.Code);
    }

    [Fact]
    public void Max_daily_loss_below_limit_allows()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with
        {
            DayStartEquity = 100_000m,
            CurrentEquity = 99_500m, // loss 500 < 1000
        };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxDailyLoss: 1_000m), ctx);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Max_daily_loss_configured_without_equity_blocks_fail_closed()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now); // no DayStartEquity / CurrentEquity
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxDailyLoss: 1_000m), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitDataInvalid.Code);
    }

    [Fact]
    public void Max_daily_loss_partial_equity_blocks_fail_closed()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkContext(now) with { DayStartEquity = 100_000m, CurrentEquity = null };
        var decision = new RiskGate().EvaluateOrderCandidate(Settings(maxDailyLoss: 1_000m), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.DailyLossLimitDataInvalid.Code);
    }
}
