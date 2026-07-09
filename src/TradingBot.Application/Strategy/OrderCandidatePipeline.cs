using TradingBot.Domain;
using TradingBot.Risk;

namespace TradingBot.Application;

/// <summary>Signal → risk → order candidate. Never calls Toss order APIs.</summary>
public sealed class OrderCandidatePipeline
{
    private readonly SimpleNasDaqSignalGenerator _signals = new();
    private readonly RiskGate _riskGate = new();

    public IReadOnlyList<EvaluatedOrderCandidate> BuildCandidates(
        IReadOnlyList<QuoteSnapshot> quotes,
        TradingSafetySettings settings,
        decimal defaultOrderQuantity,
        DateTimeOffset nowUtc,
        bool marketSessionOpen = true,
        bool marketSessionKnown = true,
        IReadOnlyDictionary<string, decimal>? positions = null,
        UsMarketSessionSnapshot? usMarket = null)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(settings);
        positions ??= new Dictionary<string, decimal>();

        string? sessionOwnerMessage = null;
        if (usMarket is not null)
        {
            var session = UsMarketSessionGuard.Evaluate(usMarket, nowUtc);
            marketSessionOpen = session.IsOpenForOrders;
            marketSessionKnown = session.IsKnown;
            sessionOwnerMessage = session.OwnerMessage;
        }

        var results = new List<EvaluatedOrderCandidate>();

        foreach (var quote in quotes)
        {
            var signal = _signals.Generate(quote, defaultOrderQuantity, nowUtc);
            if (!signal.IsActionable || signal.Side is not (SignalSide.Buy or SignalSide.Sell))
            {
                continue;
            }

            var qty = signal.SuggestedQuantity ?? 0m;
            var price = signal.ReferencePrice;
            var candidate = new OrderCandidate(
                Symbol: signal.Symbol,
                Side: signal.Side == SignalSide.Buy ? "BUY" : "SELL",
                OrderType: "LIMIT",
                Quantity: qty,
                LimitPrice: price,
                ClientOrderId: $"cand-{signal.Symbol}-{nowUtc.ToUnixTimeMilliseconds()}",
                CreatedAtUtc: nowUtc);

            positions.TryGetValue(signal.Symbol, out var pos);

            var ctx = new CandidateRiskContext
            {
                Symbol = signal.Symbol,
                Quantity = qty,
                LimitPrice = price,
                CurrentPositionQuantity = pos,
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
