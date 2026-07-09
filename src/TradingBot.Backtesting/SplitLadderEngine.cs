using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// Long-only split-buy / split-sell ladder simulator.
/// Entry when CERS expected &gt; threshold; buy limits step down; sell limits step up from avg.
/// Fills when bar low/high touches limit (limit price). Fees + slip per leg.
/// Simulation only — not investment advice.
/// </summary>
public static class SplitLadderEngine
{
    private const string Notes = "split ladder simulation · not investment advice";

    public static BacktestResult Run(
        IReadOnlyList<CandlePoint> candles,
        SplitLadderParams ladder,
        BacktestConfig? config = null,
        double[]? precomputedExpected = null,
        Func<CandlePoint, bool>? includeBar = null)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(ladder);
        ladder.Validate();
        config ??= new BacktestConfig(MaxHoldBars: 0);
        var cost = BacktestCostModel.FromConfig(config);
        var initial = config.InitialCash;
        if (initial <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "InitialCash must be > 0.");
        }

        if (candles.Count == 0)
        {
            return Empty(ladder.Name, initial);
        }

        var expected = precomputedExpected ?? CersMath.ComputeExpectedEdge(candles);
        if (expected.Length != candles.Count)
        {
            throw new ArgumentException("expected series length mismatch.", nameof(precomputedExpected));
        }

        var cash = initial;
        decimal posQty = 0m;
        decimal posCost = 0m; // total cash spent on open shares (incl buy fees)
        decimal avgEntry = 0m;
        var entryBar = -1;
        var earliestArm = 0;
        var cycleId = 0;

        // Pending buy ladder (unfilled)
        var buyLimits = Array.Empty<decimal>();
        var buyQtys = Array.Empty<decimal>();
        var buyFilled = Array.Empty<bool>();
        var buyActive = false;

        // Pending sell ladder (unfilled) after position open
        var sellLimits = Array.Empty<decimal>();
        var sellQtys = Array.Empty<decimal>();
        var sellFilled = Array.Empty<bool>();
        var sellActive = false;

        var trades = new List<BacktestTrade>();
        var equity = new List<EquityPoint>(candles.Count);
        decimal cycleEntryCash = 0m;
        decimal cycleEntryPx = 0m;
        var cycleEntryIdx = -1;
        var cycleReason = string.Empty;

        for (var i = 0; i < candles.Count; i++)
        {
            var bar = candles[i];
            var open = (decimal)bar.Open;
            var high = (decimal)bar.High;
            var low = (decimal)bar.Low;
            var close = (decimal)bar.Close;
            var isLast = i == candles.Count - 1;
            var allowed = includeBar is null || includeBar(bar);

            // --- Fill buy legs (low touches limit) ---
            if (buyActive && !isLast)
            {
                for (var L = 0; L < buyLimits.Length; L++)
                {
                    if (buyFilled[L] || buyQtys[L] <= 0m || buyLimits[L] <= 0m)
                    {
                        continue;
                    }

                    if (low <= buyLimits[L])
                    {
                        // Conservative: fill at limit (not better than limit).
                        var fillPx = cost.ApplyBuySlippage(buyLimits[L]);
                        var need = buyQtys[L] * fillPx * (1m + cost.FeeRatePerSide);
                        if (cash + 1e-12m < need)
                        {
                            // Resize last leg to remaining cash
                            var q = cost.MaxQuantityForCash(cash, fillPx);
                            if (q < BacktestCostModel.MinQuantity)
                            {
                                buyFilled[L] = true; // abandon
                                continue;
                            }

                            buyQtys[L] = q;
                            need = buyQtys[L] * fillPx * (1m + cost.FeeRatePerSide);
                        }

                        var spent = cost.CashOutToOpen(buyQtys[L], fillPx);
                        cash -= spent;
                        posQty += buyQtys[L];
                        posCost += spent;
                        avgEntry = posQty > 0m ? posCost / posQty / (1m + cost.FeeRatePerSide) : 0m;
                        // more accurate avg fill:
                        avgEntry = posQty > 0m ? (posCost / (1m + cost.FeeRatePerSide)) / posQty : 0m;
                        buyFilled[L] = true;
                        if (entryBar < 0)
                        {
                            entryBar = i;
                            cycleEntryIdx = i;
                            cycleEntryPx = fillPx;
                            cycleEntryCash = spent;
                            cycleReason = $"split_buy_L{L}";
                        }
                        else
                        {
                            cycleEntryCash += spent;
                        }
                    }
                }

                if (buyFilled.All(f => f))
                {
                    buyActive = false;
                }

                // Arm sell ladder once we have any position and not yet armed
                if (posQty > 0m && !sellActive && !buyActive)
                {
                    ArmSellLadder(posQty, avgEntry, ladder, out sellLimits, out sellQtys, out sellFilled);
                    sellActive = sellLimits.Length > 0;
                }
                else if (posQty > 0m && !sellActive && buyFilled.Any(f => f))
                {
                    // Partial fills: still allow sell ladder on filled qty
                    ArmSellLadder(posQty, avgEntry, ladder, out sellLimits, out sellQtys, out sellFilled);
                    sellActive = sellLimits.Length > 0;
                }
            }

            // --- Stop-loss on avg entry (intrabar low) ---
            if (posQty > 0m && avgEntry > 0m)
            {
                var stopPx = avgEntry * (1m - (decimal)(ladder.StopLossFromAvgPercent / 100.0));
                if (low <= stopPx)
                {
                    var fillPx = cost.ApplySellSlippage(stopPx);
                    CloseAll(
                        ref cash, ref posQty, ref posCost, ref avgEntry, ref entryBar,
                        ref buyActive, ref sellActive,
                        fillPx, cost, i, bar.Time, cycleEntryIdx, cycleEntryPx, cycleEntryCash,
                        "split_stop", trades, ref cycleId);
                    earliestArm = i + ladder.CooldownBarsAfterExit;
                    MarkEquity(equity, bar, cash, 0m);
                    continue;
                }

                // Take-profit on avg (intrabar high) — flatten remainder
                var tpPx = avgEntry * (1m + (decimal)(ladder.TakeProfitFromAvgPercent / 100.0));
                if (high >= tpPx)
                {
                    var fillPx = cost.ApplySellSlippage(tpPx);
                    CloseAll(
                        ref cash, ref posQty, ref posCost, ref avgEntry, ref entryBar,
                        ref buyActive, ref sellActive,
                        fillPx, cost, i, bar.Time, cycleEntryIdx, cycleEntryPx, cycleEntryCash,
                        "split_tp", trades, ref cycleId);
                    earliestArm = i + ladder.CooldownBarsAfterExit;
                    MarkEquity(equity, bar, cash, 0m);
                    continue;
                }
            }

            // --- Fill sell legs (high touches limit) ---
            if (sellActive && posQty > 0m)
            {
                for (var L = 0; L < sellLimits.Length; L++)
                {
                    if (sellFilled[L] || sellQtys[L] <= 0m)
                    {
                        continue;
                    }

                    if (high >= sellLimits[L])
                    {
                        var q = Math.Min(sellQtys[L], posQty);
                        if (q < BacktestCostModel.MinQuantity)
                        {
                            sellFilled[L] = true;
                            continue;
                        }

                        var fillPx = cost.ApplySellSlippage(sellLimits[L]);
                        var proceeds = cost.CashInOnClose(q, fillPx);
                        cash += proceeds;
                        // pro-rata cost basis
                        var basis = posQty > 0m ? posCost * (q / posQty) : 0m;
                        posCost -= basis;
                        posQty -= q;
                        sellFilled[L] = true;

                        var pnl = proceeds - basis;
                        var retPct = basis > 0m ? pnl / basis * 100m : 0m;
                        trades.Add(new BacktestTrade(
                            EntryIndex: cycleEntryIdx,
                            ExitIndex: i,
                            EntryTime: candles[Math.Max(0, cycleEntryIdx)].Time,
                            ExitTime: bar.Time,
                            EntryPrice: cycleEntryPx > 0m ? cycleEntryPx : avgEntry,
                            ExitPrice: fillPx,
                            Quantity: q,
                            PnLUsd: pnl,
                            ReturnPct: retPct,
                            ExitReason: $"split_sell_L{L}",
                            EntryReason: cycleReason));

                        if (posQty < BacktestCostModel.MinQuantity)
                        {
                            posQty = 0m;
                            posCost = 0m;
                            avgEntry = 0m;
                            entryBar = -1;
                            buyActive = false;
                            sellActive = false;
                            earliestArm = i + ladder.CooldownBarsAfterExit;
                            break;
                        }

                        avgEntry = posQty > 0m ? (posCost / (1m + cost.FeeRatePerSide)) / posQty : 0m;
                    }
                }

                if (sellActive && sellFilled.All(f => f) && posQty > BacktestCostModel.MinQuantity)
                {
                    // All sell legs filled but dust remains — flatten next
                    sellActive = false;
                }
            }

            // --- Max hold flatten ---
            if (posQty > 0m && entryBar >= 0 && (i - entryBar) >= ladder.MaxHoldBars)
            {
                var fillPx = open > 0m ? cost.ApplySellSlippage(open) : cost.ApplySellSlippage(close);
                CloseAll(
                    ref cash, ref posQty, ref posCost, ref avgEntry, ref entryBar,
                    ref buyActive, ref sellActive,
                    fillPx, cost, i, bar.Time, cycleEntryIdx, cycleEntryPx, cycleEntryCash,
                    "split_max_hold", trades, ref cycleId);
                earliestArm = i + ladder.CooldownBarsAfterExit;
            }

            // --- Force flat last bar ---
            if (isLast && posQty > 0m)
            {
                var fillPx = close > 0m ? cost.ApplySellSlippage(close) : 0m;
                if (fillPx > 0m)
                {
                    CloseAll(
                        ref cash, ref posQty, ref posCost, ref avgEntry, ref entryBar,
                        ref buyActive, ref sellActive,
                        fillPx, cost, i, bar.Time, cycleEntryIdx, cycleEntryPx, cycleEntryCash,
                        "force_flat_last_bar", trades, ref cycleId);
                }
            }

            // --- Arm new buy ladder on CERS entry (flat, cooldown, allowed) ---
            if (!buyActive && posQty <= 0m && !isLast && allowed && i >= earliestArm)
            {
                var exp = expected[i];
                if (!double.IsNaN(exp) && !double.IsInfinity(exp) && exp > ladder.EntryThreshold && close > 0m)
                {
                    ArmBuyLadder(cash, close, ladder, cost, out buyLimits, out buyQtys, out buyFilled);
                    buyActive = buyLimits.Length > 0;
                    cycleReason = $"cers_exp={exp:F4}>thr";
                    cycleId++;
                    // Cancel unfilled after max hold from arm? use same max hold from first fill
                }
            }

            // Cancel stale buy ladder with no fills after MaxHoldBars from arm start
            // (tracked via entryBar only after fill — optional: drop buyActive if all unfilled long)
            if (buyActive && posQty <= 0m && buyFilled.All(f => !f))
            {
                // If price ran away above first limit for many bars, cancel — use high > ref*1.02
                // simple: if close > buyLimits[0] * 1.01 for this bar after arm, leave active until touch
            }

            MarkEquity(equity, bar, cash, posQty > 0m && close > 0m ? posQty * close : 0m);
        }

        var final = cash;
        var totalRet = initial > 0m ? (final - initial) / initial * 100m : 0m;
        var mdd = ComputeMdd(equity);
        var sharpe = ComputeSharpe(equity, config.PeriodsPerYear ?? BacktestConfig.DefaultOneMinutePeriodsPerYear);
        var wins = trades.Count(t => t.PnLUsd > 0m);
        var grossWin = trades.Where(t => t.PnLUsd > 0m).Sum(t => t.PnLUsd);
        var grossLoss = Math.Abs(trades.Where(t => t.PnLUsd < 0m).Sum(t => t.PnLUsd));
        var pf = grossLoss > 0m ? (double)(grossWin / grossLoss) : (grossWin > 0m ? 99.0 : 0.0);
        var avgHold = trades.Count > 0
            ? trades.Average(t => (double)(t.ExitIndex - t.EntryIndex))
            : 0.0;

        return new BacktestResult(
            StrategyName: ladder.Name,
            InitialCash: initial,
            FinalEquity: final,
            TotalReturnPct: totalRet,
            MaxDrawdownPct: mdd,
            Sharpe: sharpe,
            TradeCount: trades.Count,
            WinRatePct: trades.Count > 0 ? 100.0 * wins / trades.Count : 0.0,
            ProfitFactor: pf,
            AvgHoldBars: avgHold,
            Trades: trades,
            EquityCurve: equity,
            Notes: Notes);
    }

    private static void ArmBuyLadder(
        decimal cash,
        decimal refClose,
        SplitLadderParams ladder,
        BacktestCostModel cost,
        out decimal[] limits,
        out decimal[] qtys,
        out bool[] filled)
    {
        var n = ladder.BuyLegs;
        limits = new decimal[n];
        qtys = new decimal[n];
        filled = new bool[n];
        var step = (decimal)(ladder.BuyStepPercent / 100.0);
        // Budget all cash across legs (equal notional target)
        var budgetEach = cash / n;
        for (var i = 0; i < n; i++)
        {
            var limit = refClose * (1m - (i * step));
            if (limit <= 0m)
            {
                limits = [];
                qtys = [];
                filled = [];
                return;
            }

            var fillPx = cost.ApplyBuySlippage(limit);
            var q = cost.MaxQuantityForCash(budgetEach, fillPx);
            // whole shares preference for realism with split planner
            q = decimal.Floor(q);
            if (q < 1m)
            {
                q = cost.MaxQuantityForCash(budgetEach, fillPx);
            }

            limits[i] = limit;
            qtys[i] = q;
            filled[i] = q < BacktestCostModel.MinQuantity;
        }

        if (qtys.All(q => q < BacktestCostModel.MinQuantity))
        {
            limits = [];
            qtys = [];
            filled = [];
        }
    }

    private static void ArmSellLadder(
        decimal posQty,
        decimal avgEntry,
        SplitLadderParams ladder,
        out decimal[] limits,
        out decimal[] qtys,
        out bool[] filled)
    {
        var n = ladder.SellLegs;
        limits = new decimal[n];
        qtys = new decimal[n];
        filled = new bool[n];
        if (posQty <= 0m || avgEntry <= 0m)
        {
            limits = [];
            qtys = [];
            filled = [];
            return;
        }

        var step = (decimal)(ladder.SellStepPercent / 100.0);
        var baseQty = decimal.Floor(posQty / n);
        var rem = posQty - (baseQty * n);
        for (var i = 0; i < n; i++)
        {
            // first sell leg slightly above avg, then step up
            limits[i] = avgEntry * (1m + ((i + 1) * step));
            var extra = i == 0 ? rem : 0m;
            qtys[i] = baseQty + extra;
            if (qtys[i] < BacktestCostModel.MinQuantity && i == n - 1)
            {
                qtys[i] = posQty - qtys.Take(i).Sum();
            }

            filled[i] = qtys[i] < BacktestCostModel.MinQuantity;
        }
    }

    private static void CloseAll(
        ref decimal cash,
        ref decimal posQty,
        ref decimal posCost,
        ref decimal avgEntry,
        ref int entryBar,
        ref bool buyActive,
        ref bool sellActive,
        decimal fillPx,
        BacktestCostModel cost,
        int exitIdx,
        DateTimeOffset exitTime,
        int cycleEntryIdx,
        decimal cycleEntryPx,
        decimal cycleEntryCash,
        string reason,
        List<BacktestTrade> trades,
        ref int cycleId)
    {
        if (posQty < BacktestCostModel.MinQuantity || fillPx <= 0m)
        {
            posQty = 0m;
            posCost = 0m;
            avgEntry = 0m;
            entryBar = -1;
            buyActive = false;
            sellActive = false;
            return;
        }

        var proceeds = cost.CashInOnClose(posQty, fillPx);
        cash += proceeds;
        var basis = posCost;
        var pnl = proceeds - basis;
        var retPct = basis > 0m ? pnl / basis * 100m : 0m;
        trades.Add(new BacktestTrade(
            EntryIndex: cycleEntryIdx >= 0 ? cycleEntryIdx : exitIdx,
            ExitIndex: exitIdx,
            EntryTime: exitTime, // caller may fix; engine uses bar times in main loop trades only
            ExitTime: exitTime,
            EntryPrice: cycleEntryPx > 0m ? cycleEntryPx : avgEntry,
            ExitPrice: fillPx,
            Quantity: posQty,
            PnLUsd: pnl,
            ReturnPct: retPct,
            ExitReason: reason,
            EntryReason: "split_cycle"));
        _ = cycleId;
        _ = cycleEntryCash;
        posQty = 0m;
        posCost = 0m;
        avgEntry = 0m;
        entryBar = -1;
        buyActive = false;
        sellActive = false;
    }

    private static void MarkEquity(List<EquityPoint> equity, CandlePoint bar, decimal cash, decimal posValue) =>
        equity.Add(new EquityPoint(bar.Time, cash + posValue));

    private static decimal ComputeMdd(IReadOnlyList<EquityPoint> curve)
    {
        if (curve.Count == 0)
        {
            return 0m;
        }

        decimal peak = curve[0].Equity;
        decimal mdd = 0m;
        foreach (var p in curve)
        {
            if (p.Equity > peak)
            {
                peak = p.Equity;
            }

            if (peak > 0m)
            {
                var dd = (peak - p.Equity) / peak * 100m;
                if (dd > mdd)
                {
                    mdd = dd;
                }
            }
        }

        return mdd;
    }

    private static double ComputeSharpe(IReadOnlyList<EquityPoint> curve, double periodsPerYear)
    {
        if (curve.Count < 3)
        {
            return 0;
        }

        var rets = new List<double>(curve.Count - 1);
        for (var i = 1; i < curve.Count; i++)
        {
            var prev = (double)curve[i - 1].Equity;
            var cur = (double)curve[i].Equity;
            if (prev > 0)
            {
                rets.Add((cur - prev) / prev);
            }
        }

        if (rets.Count < 2)
        {
            return 0;
        }

        var mean = rets.Average();
        var varSum = rets.Sum(r => (r - mean) * (r - mean));
        var std = Math.Sqrt(varSum / (rets.Count - 1));
        if (std < 1e-12)
        {
            return 0;
        }

        return mean / std * Math.Sqrt(periodsPerYear);
    }

    private static BacktestResult Empty(string name, decimal initial) =>
        new(
            StrategyName: name,
            InitialCash: initial,
            FinalEquity: initial,
            TotalReturnPct: 0m,
            MaxDrawdownPct: 0m,
            Sharpe: 0,
            TradeCount: 0,
            WinRatePct: 0,
            ProfitFactor: 0,
            AvgHoldBars: 0,
            Trades: Array.Empty<BacktestTrade>(),
            EquityCurve: Array.Empty<EquityPoint>(),
            Notes: Notes);
}
