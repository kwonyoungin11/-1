using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Application;

/// <summary>Signal → risk → order candidate. Never calls Toss order APIs.</summary>
public sealed class OrderCandidatePipeline
{
    private readonly StrategySignalRouter _router;
    private readonly RiskGate _riskGate = new();

    public OrderCandidatePipeline(StrategySignalRouter? router = null)
    {
        _router = router ?? new StrategySignalRouter();
    }

    public IReadOnlyList<EvaluatedOrderCandidate> BuildCandidates(
        IReadOnlyList<QuoteSnapshot> quotes,
        TradingSafetySettings settings,
        decimal defaultOrderQuantity,
        DateTimeOffset nowUtc,
        bool marketSessionOpen = true,
        bool marketSessionKnown = true,
        IReadOnlyDictionary<string, decimal>? positions = null,
        UsMarketSessionSnapshot? usMarket = null,
        TradingStrategyKind strategy = TradingStrategyKind.단순연습전략,
        PracticeStrategyContext? practice = null)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(settings);
        positions ??= new Dictionary<string, decimal>();

        // Practice daily-loss halt (percent of day-start equity). Fail-closed → no candidates.
        if (practice is not null
            && practice.MaxDailyLossPercent > 0m
            && practice.DayStartEquity is decimal dayStart
            && practice.CurrentEquity is decimal currentEquity)
        {
            var daily = DailyLossGuard.Evaluate(dayStart, currentEquity, practice.MaxDailyLossPercent);
            if (daily.IsBlocked)
            {
                return Array.Empty<EvaluatedOrderCandidate>();
            }
        }

        string? sessionOwnerMessage = null;
        if (usMarket is not null)
        {
            // Wall-clock session gate (holiday / closed / entry window).
            var session = UsMarketSessionGuard.Evaluate(usMarket, nowUtc);
            marketSessionOpen = session.IsOpenForOrders;
            marketSessionKnown = session.IsKnown;
            sessionOwnerMessage = session.OwnerMessage;
        }

        var results = new List<EvaluatedOrderCandidate>();

        // When practice sizing will override quantity, generators still need a positive base qty
        // to emit an actionable direction (sizer replaces SuggestedQuantity afterward).
        var signalBaseQuantity = defaultOrderQuantity > 0m
            ? defaultOrderQuantity
            : practice is not null ? 1m : 0m;

        foreach (var quote in quotes)
        {
            var signal = _router.Generate(
                strategy,
                quote,
                signalBaseQuantity,
                nowUtc,
                practice?.TrendFollow);

            if (!signal.IsActionable || signal.Side is not (SignalSide.Buy or SignalSide.Sell))
            {
                continue;
            }

            var qty = signal.SuggestedQuantity ?? 0m;
            var price = signal.ReferencePrice ?? quote.LastPrice;

            // Practice path: position size from equity risk + stop distance (not signal qty).
            if (practice is not null)
            {
                if (price is not decimal sizedPrice || sizedPrice <= 0m)
                {
                    continue;
                }

                var sized = PositionRiskSizer.Calculate(
                    practice.Equity,
                    practice.RiskPercentPerTrade,
                    practice.StopLossPercent,
                    sizedPrice);

                qty = sized.Quantity;
                if (qty <= 0m)
                {
                    continue;
                }
            }

            var side = signal.Side == SignalSide.Buy ? "BUY" : "SELL";
            var clientOrderId = ClientOrderIdFactory.CreateUnique(signal.Symbol, side, nowUtc);
            var candidate = new OrderCandidate(
                Symbol: signal.Symbol,
                Side: side,
                OrderType: "LIMIT",
                Quantity: qty,
                LimitPrice: price,
                ClientOrderId: clientOrderId,
                CreatedAtUtc: nowUtc);

            positions.TryGetValue(signal.Symbol, out var pos);

            var ctx = new CandidateRiskContext
            {
                Symbol = signal.Symbol,
                Quantity = qty,
                LimitPrice = price,
                CurrentPositionQuantity = pos,
                DayStartEquity = practice?.DayStartEquity,
                CurrentEquity = practice?.CurrentEquity,
                QuoteTimestampUtc = quote.TimestampUtc,
                NowUtc = nowUtc,
                HasMissingData = price is null || qty <= 0,
                MarketSessionOpen = marketSessionOpen,
                MarketSessionKnown = marketSessionKnown,
                MarketSessionOwnerMessage = sessionOwnerMessage,
            };

            var risk = _riskGate.EvaluateOrderCandidate(settings, ctx);
            var status = risk.Allowed
                ? "후보 허용 (실주문 아님 — dry-run/paper 가능)"
                : $"risk 차단: {string.Join(", ", risk.Blocks.Select(b => b.Code))}";

            results.Add(new EvaluatedOrderCandidate(candidate, signal, risk, status));
        }

        return results;
    }
}
