using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// Deterministic long-only bar backtest engine with fees and slippage.
/// Signal on bar i close → fill at bar i+1 open. Force-flat on last bar.
/// Simulation only — not investment advice; never places live orders.
/// </summary>
public static class BacktestEngine
{
    private const string SimulationNotes = "simulation · not investment advice";

    public static BacktestResult Run(
        IReadOnlyList<CandlePoint> candles,
        IBarSignalSource strategy,
        BacktestConfig? config = null,
        Func<CandlePoint, bool>? includeBar = null)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(strategy);

        config ??= new BacktestConfig();
        var cost = BacktestCostModel.FromConfig(config);
        var initialCash = config.InitialCash;
        if (initialCash <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "InitialCash must be > 0.");
        }

        if (candles.Count == 0)
        {
            return EmptyResult(strategy.Name, initialCash, "no candles · " + SimulationNotes);
        }

        strategy.Prepare(candles);

        var n = candles.Count;
        var cash = initialCash;
        decimal qty = 0m;
        decimal entryFillPrice = 0m;
        decimal entryCashOut = 0m;
        var entryIndex = -1;
        var entryReason = string.Empty;
        var earliestEntryBar = 0;
        var pendingEnter = false;
        var pendingExit = false;
        var pendingExitReason = string.Empty;
        string? pendingEntryReason = null;

        var trades = new List<BacktestTrade>();
        var equityCurve = new List<EquityPoint>(n);

        for (var i = 0; i < n; i++)
        {
            var bar = candles[i];
            var open = ToDecimal(bar.Open);
            var close = ToDecimal(bar.Close);
            var isLast = i == n - 1;

            // --- Fills at this bar's open (signal from previous close) ---
            if (qty <= 0m && pendingEnter && !isLast && i >= earliestEntryBar && open > 0m)
            {
                var buyPx = cost.ApplyBuySlippage(open);
                var size = cost.MaxQuantityForCash(cash, buyPx);
                if (size >= BacktestCostModel.MinQuantity)
                {
                    var spent = cost.CashOutToOpen(size, buyPx);
                    cash -= spent;
                    qty = size;
                    entryFillPrice = buyPx;
                    entryCashOut = spent;
                    entryIndex = i;
                    entryReason = pendingEntryReason ?? "EnterLong";
                }
            }

            pendingEnter = false;
            pendingEntryReason = null;

            var forceMaxHold =
                qty > 0m
                && config.MaxHoldBars > 0
                && entryIndex >= 0
                && (i - entryIndex) >= config.MaxHoldBars;

            if (qty > 0m && (pendingExit || forceMaxHold || isLast) && open > 0m)
            {
                var sellPx = cost.ApplySellSlippage(open);
                var proceeds = cost.CashInOnClose(qty, sellPx);
                cash += proceeds;

                var pnl = proceeds - entryCashOut;
                var retPct = entryCashOut > 0m ? pnl / entryCashOut * 100m : 0m;
                var reason = isLast && !pendingExit && !forceMaxHold
                    ? "force_flat_last_bar"
                    : forceMaxHold && !pendingExit
                        ? "max_hold"
                        : string.IsNullOrEmpty(pendingExitReason) ? "ExitLong" : pendingExitReason;

                trades.Add(new BacktestTrade(
                    EntryIndex: entryIndex,
                    ExitIndex: i,
                    EntryTime: candles[entryIndex].Time,
                    ExitTime: bar.Time,
                    EntryPrice: entryFillPrice,
                    ExitPrice: sellPx,
                    Quantity: qty,
                    PnLUsd: pnl,
                    ReturnPct: retPct,
                    ExitReason: reason,
                    EntryReason: entryReason));

                qty = 0m;
                entryFillPrice = 0m;
                entryCashOut = 0m;
                entryIndex = -1;
                entryReason = string.Empty;
                earliestEntryBar = i + config.CooldownBarsAfterExit;
            }

            pendingExit = false;
            pendingExitReason = string.Empty;

            // Mark-to-market at close
            var equity = cash + qty * (close > 0m ? close : 0m);
            equityCurve.Add(new EquityPoint(bar.Time, equity));

            // Signal on this close → fill next open (no signal scheduling on last bar)
            if (isLast)
            {
                continue;
            }

            var allowed = includeBar is null || includeBar(bar);
            if (!allowed)
            {
                continue;
            }

            var action = strategy.ActionAt(i);
            switch (action)
            {
                case BarAction.EnterLong when qty <= 0m && (i + 1) >= earliestEntryBar:
                    pendingEnter = true;
                    pendingEntryReason = strategy.EntryReasonAt(i) ?? "EnterLong";
                    break;
                case BarAction.ExitLong when qty > 0m:
                    pendingExit = true;
                    pendingExitReason = "ExitLong";
                    break;
            }
        }

        // Fallback if last open was non-positive: liquidate at last close with sell slip
        if (qty > 0m && n > 0)
        {
            var last = candles[n - 1];
            var lastClose = ToDecimal(last.Close);
            if (lastClose > 0m)
            {
                var sellPx = cost.ApplySellSlippage(lastClose);
                var proceeds = cost.CashInOnClose(qty, sellPx);
                cash += proceeds;
                var pnl = proceeds - entryCashOut;
                var retPct = entryCashOut > 0m ? pnl / entryCashOut * 100m : 0m;
                trades.Add(new BacktestTrade(
                    EntryIndex: entryIndex,
                    ExitIndex: n - 1,
                    EntryTime: candles[entryIndex].Time,
                    ExitTime: last.Time,
                    EntryPrice: entryFillPrice,
                    ExitPrice: sellPx,
                    Quantity: qty,
                    PnLUsd: pnl,
                    ReturnPct: retPct,
                    ExitReason: "force_flat_last_bar",
                    EntryReason: entryReason));
                qty = 0m;
                if (equityCurve.Count > 0)
                {
                    equityCurve[^1] = new EquityPoint(last.Time, cash);
                }
            }
        }

        var finalEquity = cash;
        var totalReturnPct = initialCash > 0m
            ? (finalEquity - initialCash) / initialCash * 100m
            : 0m;

        var periodsPerYear = config.PeriodsPerYear
            ?? EstimatePeriodsPerYear(candles)
            ?? BacktestConfig.DefaultOneMinutePeriodsPerYear;

        return new BacktestResult(
            StrategyName: strategy.Name,
            InitialCash: initialCash,
            FinalEquity: finalEquity,
            TotalReturnPct: totalReturnPct,
            MaxDrawdownPct: ComputeMaxDrawdownPct(equityCurve),
            Sharpe: ComputeSharpe(equityCurve, periodsPerYear),
            TradeCount: trades.Count,
            WinRatePct: ComputeWinRatePct(trades),
            ProfitFactor: ComputeProfitFactor(trades),
            AvgHoldBars: ComputeAvgHoldBars(trades),
            Trades: trades,
            EquityCurve: equityCurve,
            Notes: SimulationNotes);
    }

    private static BacktestResult EmptyResult(string name, decimal initialCash, string notes) =>
        new(
            StrategyName: name,
            InitialCash: initialCash,
            FinalEquity: initialCash,
            TotalReturnPct: 0m,
            MaxDrawdownPct: 0m,
            Sharpe: 0d,
            TradeCount: 0,
            WinRatePct: 0d,
            ProfitFactor: 0d,
            AvgHoldBars: 0d,
            Trades: Array.Empty<BacktestTrade>(),
            EquityCurve: Array.Empty<EquityPoint>(),
            Notes: notes);

    private static decimal ToDecimal(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0m;
        }

        return (decimal)value;
    }

    internal static decimal ComputeMaxDrawdownPct(IReadOnlyList<EquityPoint> curve)
    {
        if (curve.Count == 0)
        {
            return 0m;
        }

        decimal peak = curve[0].Equity;
        decimal maxDd = 0m;

        for (var i = 0; i < curve.Count; i++)
        {
            var eq = curve[i].Equity;
            if (eq > peak)
            {
                peak = eq;
            }

            if (peak <= 0m)
            {
                continue;
            }

            var dd = (peak - eq) / peak * 100m;
            if (dd > maxDd)
            {
                maxDd = dd;
            }
        }

        return maxDd;
    }

    internal static double ComputeSharpe(IReadOnlyList<EquityPoint> curve, double periodsPerYear)
    {
        if (curve.Count < 2 || periodsPerYear <= 0d)
        {
            return 0d;
        }

        var returns = new List<double>(curve.Count - 1);
        for (var i = 1; i < curve.Count; i++)
        {
            var prev = (double)curve[i - 1].Equity;
            var cur = (double)curve[i].Equity;
            if (prev <= 0d)
            {
                continue;
            }

            returns.Add(cur / prev - 1d);
        }

        if (returns.Count == 0)
        {
            return 0d;
        }

        var mean = 0d;
        for (var i = 0; i < returns.Count; i++)
        {
            mean += returns[i];
        }

        mean /= returns.Count;

        var variance = 0d;
        for (var i = 0; i < returns.Count; i++)
        {
            var d = returns[i] - mean;
            variance += d * d;
        }

        variance /= returns.Count;
        if (variance <= 0d)
        {
            return 0d;
        }

        var std = Math.Sqrt(variance);
        if (std <= 0d)
        {
            return 0d;
        }

        return mean / std * Math.Sqrt(periodsPerYear);
    }

    internal static double ComputeWinRatePct(IReadOnlyList<BacktestTrade> trades)
    {
        if (trades.Count == 0)
        {
            return 0d;
        }

        var wins = 0;
        for (var i = 0; i < trades.Count; i++)
        {
            if (trades[i].PnLUsd > 0m)
            {
                wins++;
            }
        }

        return 100d * wins / trades.Count;
    }

    /// <summary>
    /// Gross profit / gross loss. Returns 0 when there are no losing trades (no denominator).
    /// </summary>
    internal static double ComputeProfitFactor(IReadOnlyList<BacktestTrade> trades)
    {
        decimal grossProfit = 0m;
        decimal grossLoss = 0m;
        for (var i = 0; i < trades.Count; i++)
        {
            var pnl = trades[i].PnLUsd;
            if (pnl > 0m)
            {
                grossProfit += pnl;
            }
            else if (pnl < 0m)
            {
                grossLoss += -pnl;
            }
        }

        if (grossLoss <= 0m)
        {
            return 0d;
        }

        return (double)(grossProfit / grossLoss);
    }

    internal static double ComputeAvgHoldBars(IReadOnlyList<BacktestTrade> trades)
    {
        if (trades.Count == 0)
        {
            return 0d;
        }

        var sum = 0d;
        for (var i = 0; i < trades.Count; i++)
        {
            sum += trades[i].ExitIndex - trades[i].EntryIndex;
        }

        return sum / trades.Count;
    }

    /// <summary>
    /// Estimate annualization factor from median bar spacing.
    /// ~1m → 98_280; ~1d → 252; otherwise 252 trading days * bars-per-day.
    /// </summary>
    internal static double? EstimatePeriodsPerYear(IReadOnlyList<CandlePoint> candles)
    {
        if (candles.Count < 3)
        {
            return null;
        }

        var gaps = new List<double>(candles.Count - 1);
        for (var i = 1; i < candles.Count; i++)
        {
            var sec = (candles[i].Time - candles[i - 1].Time).TotalSeconds;
            if (sec > 0d && sec < 7d * 24 * 3600)
            {
                gaps.Add(sec);
            }
        }

        if (gaps.Count == 0)
        {
            return null;
        }

        gaps.Sort();
        var median = gaps[gaps.Count / 2];

        // ~1 minute
        if (median is >= 30 and <= 90)
        {
            return BacktestConfig.DefaultOneMinutePeriodsPerYear;
        }

        // ~1 day
        if (median is >= 20 * 3600 and <= 28 * 3600)
        {
            return BacktestConfig.DefaultDailyPeriodsPerYear;
        }

        // Generic: 252 trading days * (session-day seconds / median bar)
        const double tradingDaySeconds = 6.5 * 3600; // US RTH
        var barsPerDay = tradingDaySeconds / median;
        if (barsPerDay <= 0d)
        {
            return null;
        }

        return 252d * barsPerDay;
    }
}
