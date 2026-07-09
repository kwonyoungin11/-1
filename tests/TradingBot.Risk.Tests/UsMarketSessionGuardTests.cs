using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Risk.Tests;

public class UsMarketSessionGuardTests
{
    private static TradingSafetySettings DryRunSettings() => new()
    {
        AllowLiveOrders = false,
        KillSwitch = true,
        OrderMode = OrderMode.DryRun,
        MaxOrderNotional = 10_000m,
        MarketDataMaxStalenessSeconds = 60,
    };

    private static CandidateRiskContext OkBase(DateTimeOffset now) => new()
    {
        Symbol = "AAPL",
        Quantity = 1,
        LimitPrice = 100m,
        QuoteTimestampUtc = now,
        NowUtc = now,
    };

    [Fact]
    public void Null_snapshot_is_unknown_and_not_open()
    {
        var evaluation = UsMarketSessionGuard.Evaluate(null);

        Assert.False(evaluation.IsKnown);
        Assert.False(evaluation.IsOpenForOrders);
        Assert.Contains("세션", evaluation.OwnerMessage, StringComparison.Ordinal);
        Assert.Equal(UsMarketSessionGuard.UnknownOwnerMessage, evaluation.OwnerMessage);
    }

    [Fact]
    public void Null_snapshot_EvaluateSession_blocks_as_unknown()
    {
        var decision = new RiskGate().EvaluateSession(null);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
        Assert.Contains(decision.Blocks, b => b.Message.Contains("unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Holiday_snapshot_is_known_closed()
    {
        var snapshot = new UsMarketSessionSnapshot(
            "2026-07-04",
            IsHolidayOrClosed: true,
            OwnerMessage: "미국 시장 2026-07-04: 독립기념일 휴장");

        var evaluation = UsMarketSessionGuard.Evaluate(snapshot);

        Assert.True(evaluation.IsKnown);
        Assert.False(evaluation.IsOpenForOrders);
        Assert.Equal(snapshot.OwnerMessage, evaluation.OwnerMessage);
    }

    [Fact]
    public void Holiday_EvaluateSession_blocks()
    {
        var snapshot = new UsMarketSessionSnapshot(
            "2026-12-25",
            IsHolidayOrClosed: true,
            OwnerMessage: "크리스마스 휴장");

        var decision = RiskGate.EvaluateSessionStatic(snapshot);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
        Assert.Contains(decision.Blocks, b => b.Message.Contains("closed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(decision.Blocks, b => b.Message.Contains("크리스마스", StringComparison.Ordinal));
    }

    [Fact]
    public void Open_session_allows_when_other_candidate_fields_ok()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var snapshot = new UsMarketSessionSnapshot(
            "2026-07-09",
            IsHolidayOrClosed: false,
            OwnerMessage: "미국 시장 2026-07-09: 정규장 세션 정보 있음");

        var sessionDecision = new RiskGate().EvaluateSession(snapshot, now);
        Assert.True(sessionDecision.Allowed);

        var ctx = CandidateRiskContext.BuildContextFromUsSnapshot(OkBase(now), snapshot, now);
        Assert.True(ctx.MarketSessionKnown);
        Assert.True(ctx.MarketSessionOpen);
        Assert.Equal(snapshot.OwnerMessage, ctx.MarketSessionOwnerMessage);

        var decision = new RiskGate().EvaluateOrderCandidate(DryRunSettings(), ctx);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void BuildContextFromUsSnapshot_null_sets_closed_flags_and_blocks_candidate()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = CandidateRiskContext.BuildContextFromUsSnapshot(OkBase(now), snapshot: null);

        Assert.False(ctx.MarketSessionKnown);
        Assert.False(ctx.MarketSessionOpen);
        Assert.False(string.IsNullOrWhiteSpace(ctx.MarketSessionOwnerMessage));

        var decision = new RiskGate().EvaluateOrderCandidate(DryRunSettings(), ctx);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
        Assert.Contains(decision.Blocks, b => b.Message.Contains("unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Holiday_context_blocks_candidate_with_enriched_message()
    {
        var now = DateTimeOffset.Parse("2026-07-04T15:00:00Z");
        var snapshot = new UsMarketSessionSnapshot(
            "2026-07-04",
            IsHolidayOrClosed: true,
            OwnerMessage: "독립기념일 휴장");

        var ctx = UsMarketSessionGuard.ApplyToContext(OkBase(now), snapshot);
        var decision = new RiskGate().EvaluateOrderCandidate(DryRunSettings(), ctx);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
        Assert.Contains(decision.Blocks, b => b.Message.Contains("독립기념일", StringComparison.Ordinal));
    }

    [Fact]
    public void Empty_owner_message_uses_guard_fallback()
    {
        var closed = UsMarketSessionGuard.Evaluate(
            new UsMarketSessionSnapshot("2026-01-01", IsHolidayOrClosed: true, OwnerMessage: "  "));
        Assert.Equal(UsMarketSessionGuard.HolidayOwnerMessageFallback, closed.OwnerMessage);

        var open = UsMarketSessionGuard.Evaluate(
            new UsMarketSessionSnapshot("2026-07-09", IsHolidayOrClosed: false, OwnerMessage: ""));
        Assert.Equal(UsMarketSessionGuard.OpenOwnerMessageFallback, open.OwnerMessage);
    }

    // --- edge cases ---

    [Fact]
    public void Wall_clock_does_not_override_calendar_open_flag()
    {
        // wallClockUtc is reserved; holiday/closed calendar flag is authoritative today.
        var overnight = DateTimeOffset.Parse("2026-07-09T03:00:00Z");
        var snapshot = new UsMarketSessionSnapshot(
            "2026-07-09",
            IsHolidayOrClosed: false,
            OwnerMessage: "정규장 캘린더 오픈");

        var evaluation = UsMarketSessionGuard.Evaluate(snapshot, overnight);

        Assert.True(evaluation.IsKnown);
        Assert.True(evaluation.IsOpenForOrders);
        Assert.True(RiskGate.EvaluateSessionStatic(snapshot, overnight).Allowed);
    }

    [Fact]
    public void Wall_clock_does_not_reopen_holiday_session()
    {
        var midDay = DateTimeOffset.Parse("2026-07-04T17:00:00Z");
        var snapshot = new UsMarketSessionSnapshot(
            "2026-07-04",
            IsHolidayOrClosed: true,
            OwnerMessage: "휴장");

        var evaluation = UsMarketSessionGuard.Evaluate(snapshot, midDay);

        Assert.True(evaluation.IsKnown);
        Assert.False(evaluation.IsOpenForOrders);
        Assert.True(RiskGate.EvaluateSessionStatic(snapshot, midDay).IsBlocked);
    }

    [Fact]
    public void ApplyToContext_preserves_non_session_fields()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var baseCtx = OkBase(now) with
        {
            Symbol = "MSFT",
            Quantity = 3,
            LimitPrice = 250m,
            CurrentPositionQuantity = 7m,
            HasApiError = true,
        };

        var snapshot = new UsMarketSessionSnapshot("2026-07-09", IsHolidayOrClosed: false, OwnerMessage: "open");
        var applied = UsMarketSessionGuard.ApplyToContext(baseCtx, snapshot, now);

        Assert.Equal("MSFT", applied.Symbol);
        Assert.Equal(3m, applied.Quantity);
        Assert.Equal(250m, applied.LimitPrice);
        Assert.Equal(7m, applied.CurrentPositionQuantity);
        Assert.True(applied.HasApiError);
        Assert.True(applied.MarketSessionKnown);
        Assert.True(applied.MarketSessionOpen);
        Assert.Equal("open", applied.MarketSessionOwnerMessage);
    }

    [Fact]
    public void ApplyToContext_null_context_throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            UsMarketSessionGuard.ApplyToContext(null!, snapshot: null));
    }

    [Fact]
    public void Unknown_session_flag_blocks_even_when_open_flag_true()
    {
        // Fail-closed: MarketSessionKnown must be true; open alone is not enough.
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkBase(now) with
        {
            MarketSessionKnown = false,
            MarketSessionOpen = true,
            MarketSessionOwnerMessage = "세션 불확실",
        };

        var decision = new RiskGate().EvaluateOrderCandidate(DryRunSettings(), ctx);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
        Assert.Contains(decision.Blocks, b => b.Message.Contains("unknown", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(decision.Blocks, b => b.Message.Contains("세션 불확실", StringComparison.Ordinal));
    }

    [Fact]
    public void Known_closed_without_owner_message_uses_default_block_reason()
    {
        var now = DateTimeOffset.Parse("2026-07-09T15:00:00Z");
        var ctx = OkBase(now) with
        {
            MarketSessionKnown = true,
            MarketSessionOpen = false,
            MarketSessionOwnerMessage = null,
        };

        var decision = new RiskGate().EvaluateOrderCandidate(DryRunSettings(), ctx);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
        Assert.Contains(decision.Blocks, b => b.Message == BlockedReason.MarketSessionClosed.Message);
    }

    [Fact]
    public void EvaluateSession_open_snapshot_returns_allow_with_no_blocks()
    {
        var snapshot = new UsMarketSessionSnapshot(
            "2026-07-09",
            IsHolidayOrClosed: false,
            OwnerMessage: "정규장");

        var decision = new RiskGate().EvaluateSession(snapshot);

        Assert.True(decision.Allowed);
        Assert.False(decision.IsBlocked);
        Assert.Empty(decision.Blocks);
    }

    [Fact]
    public void Whitespace_owner_message_on_null_path_still_unknown_message()
    {
        // Null snapshot never uses caller message; always UnknownOwnerMessage.
        var evaluation = UsMarketSessionGuard.Evaluate(null, DateTimeOffset.UtcNow);
        Assert.Equal(UsMarketSessionGuard.UnknownOwnerMessage, evaluation.OwnerMessage);
        Assert.False(evaluation.IsKnown);
        Assert.False(evaluation.IsOpenForOrders);
    }
}
