using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>
/// 자동매매 연습 세션. 시작/종료는 연습 루프만 제어하며 실주문을 열지 않음.
/// </summary>
public sealed class AutoTradeSessionService
{
    private readonly object _gate = new();
    private AutoTradeSessionStatus _status = AutoTradeSessionStatus.중지;
    private StockMarketKind _stockKind = StockMarketKind.나스닥;
    private TradingStrategyKind _strategy = TradingStrategyKind.단순연습전략;
    private string? _focusSymbol;
    private decimal _startingBalance = 100_000m;
    private decimal _balance = 100_000m;
    private decimal _realizedPnl;

    public StockMarketKind StockKind
    {
        get { lock (_gate) { return _stockKind; } }
        set
        {
            lock (_gate)
            {
                _stockKind = value;
                _focusSymbol = null; // 종류 바꾸면 첫 종목으로
            }
        }
    }

    public TradingStrategyKind Strategy
    {
        get { lock (_gate) { return _strategy; } }
        set { lock (_gate) { _strategy = value; } }
    }

    /// <summary>차트·포커스 종목. null이면 워치리스트 첫 종목.</summary>
    public string? FocusSymbol
    {
        get { lock (_gate) { return _focusSymbol; } }
        set
        {
            lock (_gate)
            {
                _focusSymbol = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
            }
        }
    }

    public AutoTradeSessionStatus Status
    {
        get { lock (_gate) { return _status; } }
    }

    public decimal Balance
    {
        get { lock (_gate) { return _balance; } }
    }

    /// <summary>Practice day-start / session starting equity (default 100_000).</summary>
    public decimal StartingBalance
    {
        get { lock (_gate) { return _startingBalance; } }
    }

    public string[] ResolveWatchSymbols()
    {
        lock (_gate)
        {
            return WatchlistCatalog.ResolveSymbols(_stockKind).ToArray();
        }
    }

    public string ResolveFocusSymbol()
    {
        lock (_gate)
        {
            return ResolveFocusSymbolUnlocked();
        }
    }

    private string ResolveFocusSymbolUnlocked()
    {
        var list = WatchlistCatalog.ResolveSymbols(_stockKind);
        if (_focusSymbol is not null
            && list.Any(s => s.Equals(_focusSymbol, StringComparison.OrdinalIgnoreCase)))
        {
            return _focusSymbol;
        }

        return list[0];
    }

    public bool TryStart(out string ownerMessage)
    {
        lock (_gate)
        {
            if (_status == AutoTradeSessionStatus.실행중)
            {
                ownerMessage = "이미 자동매매(연습)가 실행 중입니다.";
                return false;
            }

            _status = AutoTradeSessionStatus.실행중;
            ownerMessage = $"자동매매(연습) 시작 · 전략 {_strategy} · 실주문은 나가지 않습니다.";
            return true;
        }
    }

    public bool TryStop(out string ownerMessage)
    {
        lock (_gate)
        {
            if (_status == AutoTradeSessionStatus.중지)
            {
                ownerMessage = "이미 중지 상태입니다.";
                return false;
            }

            _status = AutoTradeSessionStatus.중지;
            ownerMessage = "자동매매(연습) 종료.";
            return true;
        }
    }

    public void ApplyVirtualFill(string side, decimal quantity, decimal price)
    {
        ArgumentNullException.ThrowIfNull(side);
        lock (_gate)
        {
            var notional = quantity * price;
            if (side.Equals("BUY", StringComparison.OrdinalIgnoreCase))
            {
                _balance -= notional;
            }
            else
            {
                _balance += notional;
                _realizedPnl += notional * 0.001m;
            }
        }
    }

    public void ApplyScaffoldMarkToMarket(decimal markPnlDelta)
    {
        lock (_gate)
        {
            _realizedPnl += markPnlDelta;
            _balance = _startingBalance + _realizedPnl;
        }
    }

    public AutoTradePanelSnapshot ToPanelSnapshot()
    {
        lock (_gate)
        {
            var rate = _startingBalance <= 0
                ? 0m
                : Math.Round((_realizedPnl / _startingBalance) * 100m, 2);
            var symbols = WatchlistCatalog.ResolveSymbols(_stockKind);
            var focus = ResolveFocusSymbolUnlocked();
            return new AutoTradePanelSnapshot
            {
                StockKind = _stockKind,
                Strategy = _strategy,
                SessionStatus = _status,
                StockKindLabel = _stockKind.ToString(),
                StrategyLabel = _strategy.ToString(),
                SessionStatusLabel = _status.ToString(),
                WatchSymbolsText = string.Join(", ", symbols),
                FocusSymbol = focus,
                StockKindDescription = WatchlistCatalog.Describe(_stockKind),
                StrategyDescription = StrategyCatalog.Describe(_strategy),
                Balance = _balance,
                BalanceLabel = $"잔액 {_balance:N2} (연습)",
                ReturnRatePercent = rate,
                ReturnRateLabel = $"수익률 {rate:N2}%",
                CanStart = _status == AutoTradeSessionStatus.중지,
                CanStop = _status == AutoTradeSessionStatus.실행중,
                SafetyNote = "시작·종료는 연습 세션만 제어합니다. 실주문은 차단됩니다. 버블 크기=체결 규모(수량×가격).",
            };
        }
    }
}
