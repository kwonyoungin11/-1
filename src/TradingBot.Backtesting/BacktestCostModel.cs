namespace TradingBot.Backtesting;

/// <summary>
/// Symmetric per-side fee and slippage for bar backtests.
/// Buy fills worse (higher); sell fills worse (lower).
/// </summary>
public sealed class BacktestCostModel
{
    public const decimal MinQuantity = 1e-8m;

    public BacktestCostModel(decimal feeRatePerSide, decimal slippageRatePerSide)
    {
        if (feeRatePerSide < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(feeRatePerSide), "Fee rate must be >= 0.");
        }

        if (slippageRatePerSide < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(slippageRatePerSide), "Slippage rate must be >= 0.");
        }

        FeeRatePerSide = feeRatePerSide;
        SlippageRatePerSide = slippageRatePerSide;
    }

    public static BacktestCostModel FromConfig(BacktestConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new BacktestCostModel(config.FeeRatePerSide, config.SlippageRatePerSide);
    }

    public decimal FeeRatePerSide { get; }

    public decimal SlippageRatePerSide { get; }

    /// <summary>Buy fill price: open * (1 + slip).</summary>
    public decimal ApplyBuySlippage(decimal openPrice)
    {
        EnsurePositivePrice(openPrice);
        return openPrice * (1m + SlippageRatePerSide);
    }

    /// <summary>Sell fill price: open * (1 - slip).</summary>
    public decimal ApplySellSlippage(decimal openPrice)
    {
        EnsurePositivePrice(openPrice);
        return openPrice * (1m - SlippageRatePerSide);
    }

    /// <summary>Fee charged on trade notional (price * quantity).</summary>
    public decimal FeeOnNotional(decimal notional)
    {
        if (notional < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(notional), "Notional must be >= 0.");
        }

        return notional * FeeRatePerSide;
    }

    /// <summary>
    /// All-in long size from cash so notional + fee &lt;= cash.
    /// Returns 0 when fill price is non-positive or cash cannot buy MinQuantity.
    /// </summary>
    public decimal MaxQuantityForCash(decimal cash, decimal buyFillPrice)
    {
        if (cash <= 0m || buyFillPrice <= 0m)
        {
            return 0m;
        }

        // cash = qty * price * (1 + feeRate)  →  qty = cash / (price * (1 + feeRate))
        var divisor = buyFillPrice * (1m + FeeRatePerSide);
        if (divisor <= 0m)
        {
            return 0m;
        }

        var qty = cash / divisor;
        return qty < MinQuantity ? 0m : qty;
    }

    /// <summary>Cash spent to open: notional + fee.</summary>
    public decimal CashOutToOpen(decimal quantity, decimal buyFillPrice)
    {
        if (quantity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var notional = quantity * buyFillPrice;
        return notional + FeeOnNotional(notional);
    }

    /// <summary>Cash received on close: notional − fee.</summary>
    public decimal CashInOnClose(decimal quantity, decimal sellFillPrice)
    {
        if (quantity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var notional = quantity * sellFillPrice;
        return notional - FeeOnNotional(notional);
    }

    private static void EnsurePositivePrice(decimal price)
    {
        if (price <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be > 0.");
        }
    }
}
