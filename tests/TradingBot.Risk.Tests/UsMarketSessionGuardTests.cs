using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Risk.Tests;

public class UsMarketSessionGuardTests
{
    // 2026-07-09 is a Thursday. America/New_York is EDT (UTC-4):
    // RTH open 09:30 EDT = 13:30Z, close 16:00 EDT = 20:00Z.
    // Default entry window: [13:35Z, 19:45Z).
    private static readonly DateTimeOffset SessionMidUtc = DateTimeOffset.Parse("2026-07-09T16:00:00Z");
    private static readonly DateTimeOffset SessionOpenUtc = DateTimeOffset.Parse("2026-07-09T13:30:00Z");
    private static readonly DateTimeOffset SessionCloseUtc = DateTimeOffset.Parse("2026-07-09T20:00:00Z");

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

    private static UsMarketSessionSnapshot OpenDay(string date = "2026-07-09", string msg = "정규장")
        => new(date, IsHolidayOrClosed: false, OwnerMessage: msg);

    private static TradingSessionWindow FixedWindow(
        int afterOpen = TradingSessionWindow.DefaultBlockAfterOpenMinutes,
        int beforeClose = TradingSessionWindow.DefaultBlockBeforeCloseMinutes)
        => TradingSessionWindow.Create(SessionOpenUtc, SessionCloseUtc, afterOpen, beforeClose);

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

        var evaluation = UsMarketSessionGuard.Evaluate(snapshot, SessionMidUtc);

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

        var decision = RiskGate.EvaluateSessionStatic(snapshot, SessionMidUtc);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
        Assert.Contains(decision.Blocks, b => b.Message.Contains("closed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(decision.Blocks, b => b.Message.Contains("크리스마스", StringComparison.Ordinal));
    }

    [Fact]
    public void Open_session_allows_when_other_candidate_fields_ok()
    {
        var now = SessionMidUtc;
        var snapshot = OpenDay(msg: "미국 시장 2026-07-09: 정규장 세션 정보 있음");

        var sessionDecision = new RiskGate().EvaluateSession(snapshot, now);
        Assert.True(sessionDecision.Allowed);

        var ctx = CandidateRiskContext.BuildContextFromUsSnapshot(OkBase(now), snapshot, now);
        Assert.True(ctx.MarketSessionKnown);
        Assert.True(ctx.MarketSessionOpen);
        Assert.Contains(snapshot.OwnerMessage, ctx.MarketSessionOwnerMessage, StringComparison.Ordinal);

        var decision = new RiskGate().EvaluateOrderCandidate(DryRunSettings(), ctx);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void BuildContextFromUsSnapshot_null_sets_closed_flags_and_blocks_candidate()
    {
        var now = SessionMidUtc;
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

        var ctx = UsMarketSessionGuard.ApplyToContext(OkBase(now), snapshot, now);
        var decision = new RiskGate().EvaluateOrderCandidate(DryRunSettings(), ctx);

        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
        Assert.Contains(decision.Blocks, b => b.Message.Contains("독립기념일", StringComparison.Ordinal));
    }

    [Fact]
    public void Empty_owner_message_uses_guard_fallback()
    {
        var closed = UsMarketSessionGuard.Evaluate(
            new UsMarketSessionSnapshot("2026-01-01", IsHolidayOrClosed: true, OwnerMessage: "  "),
            SessionMidUtc);
        Assert.Equal(UsMarketSessionGuard.HolidayOwnerMessageFallback, closed.OwnerMessage);

        var open = UsMarketSessionGuard.Evaluate(
            new UsMarketSessionSnapshot("2026-07-09", IsHolidayOrClosed: false, OwnerMessage: ""),
            SessionMidUtc);
        Assert.True(open.IsOpenForOrders);
        Assert.Contains("진입", open.OwnerMessage, StringComparison.Ordinal);
    }

    // --- session window / wall clock ---

    [Fact]
    public void Wall_clock_outside_rth_blocks_open_calendar_day()
    {
        var overnight = DateTimeOffset.Parse("2026-07-09T03:00:00Z");
        var snapshot = OpenDay(msg: "정규장 캘린더 오픈");

        var evaluation = UsMarketSessionGuard.Evaluate(snapshot, overnight);

        Assert.True(evaluation.IsKnown);
        Assert.False(evaluation.IsOpenForOrders);
        Assert.Contains("개장 전", evaluation.OwnerMessage, StringComparison.Ordinal);
        Assert.True(RiskGate.EvaluateSessionStatic(snapshot, overnight).IsBlocked);
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
        var now = SessionMidUtc;
        var baseCtx = OkBase(now) with
        {
            Symbol = "MSFT",
            Quantity = 3,
            LimitPrice = 250m,
            CurrentPositionQuantity = 7m,
            HasApiError = true,
        };

        var snapshot = OpenDay(msg: "open");
        var applied = UsMarketSessionGuard.ApplyToContext(baseCtx, snapshot, now);

        Assert.Equal("MSFT", applied.Symbol);
        Assert.Equal(3m, applied.Quantity);
        Assert.Equal(250m, applied.LimitPrice);
        Assert.Equal(7m, applied.CurrentPositionQuantity);
        Assert.True(applied.HasApiError);
        Assert.True(applied.MarketSessionKnown);
        Assert.True(applied.MarketSessionOpen);
        Assert.Contains("open", applied.MarketSessionOwnerMessage, StringComparison.Ordinal);
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
        var now = SessionMidUtc;
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
        var now = SessionMidUtc;
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
    public void EvaluateSession_open_snapshot_mid_session_allows()
    {
        var snapshot = OpenDay(msg: "정규장");

        var decision = new RiskGate().EvaluateSession(snapshot, SessionMidUtc);

        Assert.True(decision.Allowed);
        Assert.False(decision.IsBlocked);
        Assert.Empty(decision.Blocks);
    }

    [Fact]
    public void Open_calendar_without_wall_clock_is_fail_closed()
    {
        var evaluation = UsMarketSessionGuard.Evaluate(OpenDay());

        Assert.False(evaluation.IsKnown);
        Assert.False(evaluation.IsOpenForOrders);
        Assert.Equal(TradingSessionWindow.MissingWallClockOwnerMessage, evaluation.OwnerMessage);
    }

    [Fact]
    public void Unparseable_session_date_is_unknown_open_close_fail_closed()
    {
        var snapshot = new UsMarketSessionSnapshot(
            "not-a-date",
            IsHolidayOrClosed: false,
            OwnerMessage: "bad date");

        var evaluation = UsMarketSessionGuard.Evaluate(snapshot, SessionMidUtc);

        Assert.False(evaluation.IsKnown);
        Assert.False(evaluation.IsOpenForOrders);
        Assert.Equal(TradingSessionWindow.UnknownOpenCloseOwnerMessage, evaluation.OwnerMessage);
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

    // --- fixed-time entry window cases ---

    [Theory]
    [InlineData("2026-07-09T13:30:00Z", false)] // exact open → still in after-open buffer
    [InlineData("2026-07-09T13:34:59Z", false)] // last second of after-open buffer
    [InlineData("2026-07-09T13:35:00Z", true)]  // first allowed instant (default +5m)
    [InlineData("2026-07-09T16:00:00Z", true)]  // mid session
    [InlineData("2026-07-09T19:44:59Z", true)]  // last allowed second before close buffer
    [InlineData("2026-07-09T19:45:00Z", false)] // start of before-close buffer (default 15m)
    [InlineData("2026-07-09T20:00:00Z", false)] // exact close
    [InlineData("2026-07-09T20:30:00Z", false)] // after close
    [InlineData("2026-07-09T12:00:00Z", false)] // before open
    public void Fixed_times_default_buffers_on_standard_us_rth(string nowIso, bool expectOpen)
    {
        var now = DateTimeOffset.Parse(nowIso);
        var evaluation = UsMarketSessionGuard.Evaluate(OpenDay(), now, FixedWindow());

        Assert.True(evaluation.IsKnown);
        Assert.Equal(expectOpen, evaluation.IsOpenForOrders);
    }

    [Fact]
    public void First_five_minutes_after_open_blocks_new_entries()
    {
        var atOpenPlus2 = SessionOpenUtc.AddMinutes(2);
        var evaluation = UsMarketSessionGuard.Evaluate(OpenDay(), atOpenPlus2, FixedWindow());

        Assert.True(evaluation.IsKnown);
        Assert.False(evaluation.IsOpenForOrders);
        Assert.Contains("개장", evaluation.OwnerMessage, StringComparison.Ordinal);
        Assert.True(RiskGate.EvaluateSessionStatic(OpenDay(), atOpenPlus2).IsBlocked);
    }

    [Fact]
    public void Last_fifteen_minutes_before_close_blocks_new_entries()
    {
        var tenBeforeClose = SessionCloseUtc.AddMinutes(-10);
        var evaluation = UsMarketSessionGuard.Evaluate(OpenDay(), tenBeforeClose, FixedWindow());

        Assert.True(evaluation.IsKnown);
        Assert.False(evaluation.IsOpenForOrders);
        Assert.Contains("마감", evaluation.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_buffers_change_allowed_window()
    {
        // afterOpen=0, beforeClose=0 → full RTH open..close
        var window = FixedWindow(afterOpen: 0, beforeClose: 0);

        Assert.True(UsMarketSessionGuard.Evaluate(OpenDay(), SessionOpenUtc, window).IsOpenForOrders);
        Assert.False(UsMarketSessionGuard.Evaluate(OpenDay(), SessionCloseUtc, window).IsOpenForOrders);

        // afterOpen=30, beforeClose=30 → mid still ok; 14:00Z (30m after open) ok; 13:40 not
        var tight = FixedWindow(afterOpen: 30, beforeClose: 30);
        Assert.False(UsMarketSessionGuard.Evaluate(OpenDay(), SessionOpenUtc.AddMinutes(20), tight).IsOpenForOrders);
        Assert.True(UsMarketSessionGuard.Evaluate(OpenDay(), SessionOpenUtc.AddMinutes(30), tight).IsOpenForOrders);
        Assert.False(UsMarketSessionGuard.Evaluate(OpenDay(), SessionCloseUtc.AddMinutes(-30), tight).IsOpenForOrders);
        Assert.True(UsMarketSessionGuard.Evaluate(OpenDay(), SessionCloseUtc.AddMinutes(-31), tight).IsOpenForOrders);
    }

    [Fact]
    public void Explicit_window_overrides_date_derived_hours()
    {
        // Snapshot date is unparseable, but explicit window is known → use window.
        var snapshot = new UsMarketSessionSnapshot("bad", IsHolidayOrClosed: false, OwnerMessage: "x");
        var window = FixedWindow();

        var allowed = UsMarketSessionGuard.Evaluate(snapshot, SessionMidUtc, window);
        Assert.True(allowed.IsKnown);
        Assert.True(allowed.IsOpenForOrders);

        var blocked = UsMarketSessionGuard.Evaluate(snapshot, SessionOpenUtc, window);
        Assert.True(blocked.IsKnown);
        Assert.False(blocked.IsOpenForOrders);
    }

    [Fact]
    public void ApplyToContext_with_open_buffer_blocks_candidate()
    {
        var now = SessionOpenUtc.AddMinutes(1);
        var ctx = UsMarketSessionGuard.ApplyToContext(OkBase(now), OpenDay(), now, FixedWindow());
        var decision = new RiskGate().EvaluateOrderCandidate(DryRunSettings(), ctx);

        Assert.False(ctx.MarketSessionOpen);
        Assert.True(decision.IsBlocked);
        Assert.Contains(decision.Blocks, b => b.Code == BlockedReason.MarketSessionClosed.Code);
    }

    [Fact]
    public void TryCreateUsRegularHours_matches_fixed_eastern_instants()
    {
        Assert.True(TradingSessionWindow.TryCreateUsRegularHours("2026-07-09", out var window));
        Assert.NotNull(window);
        Assert.Equal(SessionOpenUtc, window!.RegularOpenUtc);
        Assert.Equal(SessionCloseUtc, window.RegularCloseUtc);
        Assert.Equal(TradingSessionWindow.DefaultBlockAfterOpenMinutes, window.BlockAfterOpenMinutes);
        Assert.Equal(TradingSessionWindow.DefaultBlockBeforeCloseMinutes, window.BlockBeforeCloseMinutes);
    }

    [Fact]
    public void Empty_entry_window_from_large_buffers_fail_closed()
    {
        // 6.5h RTH; buffers 200+200 minutes leave empty/inverted allowed window.
        var window = FixedWindow(afterOpen: 200, beforeClose: 200);
        var evaluation = window.EvaluateNewEntry(SessionMidUtc);

        Assert.True(evaluation.IsKnown);
        Assert.False(evaluation.AllowsNewEntry);
        Assert.Equal(TradingSessionWindow.EmptyEntryWindowOwnerMessage, evaluation.OwnerMessage);
    }

    [Fact]
    public void Create_rejects_inverted_open_close()
    {
        Assert.Throws<ArgumentException>(() =>
            TradingSessionWindow.Create(SessionCloseUtc, SessionOpenUtc));
    }
}
