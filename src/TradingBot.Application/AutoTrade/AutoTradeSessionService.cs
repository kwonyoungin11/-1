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
    private decimal _startingBalance = 100_000m;
    private decimal _balance = 100_000m;
    private decimal _realizedPnl;

    public StockMarketKind StockKind
    {
        get { lock (_gate) { return _stockKind; } }
        set { lock (_gate) { _stockKind = value; } }
    }

    public TradingStrategyKind Strategy
    {
        get { lock (_gate) { return _strategy; } }
        set { lock (_gate) { _strategy = value; } }
    }

    public AutoTradeSessionStatus Status
    {
        get { lock (_gate) { return _status; } }
    }

    public decimal Balance
    {
        get { lock (_gate) { return _balance; } }
    }

    public string[] ResolveWatchSymbols()
    {
        lock (_gate)
        {
            return _stockKind switch
            {
                StockMarketKind.나스닥 => ["AAPL", "MSFT", "NVDA"],
                StockMarketKind.미국주식 => ["AAPL", "MSFT", "SPY"],
                StockMarketKind.국내주식 => ["005930"],
                _ => ["AAPL"],
            };
        }
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
            ownerMessage = "자동매매(연습) 시작 · 실주문은 나가지 않습니다.";
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
            return new AutoTradePanelSnapshot
            {
                StockKind = _stockKind,
                Strategy = _strategy,
                SessionStatus = _status,
                StockKindLabel = _stockKind.ToString(),
                StrategyLabel = _strategy.ToString(),
                SessionStatusLabel = _status.ToString(),
                WatchSymbolsText = string.Join(", ", ResolveWatchSymbols()),
                Balance = _balance,
                BalanceLabel = $"잔액 {_balance:N2} (연습)",
                ReturnRatePercent = rate,
                ReturnRateLabel = $"수익률 {rate:N2}%",
                CanStart = _status == AutoTradeSessionStatus.중지,
                CanStop = _status == AutoTradeSessionStatus.실행중,
                SafetyNote = "시작·종료는 연습 세션만 제어합니다. 실주문은 차단됩니다.",
            };
        }
    }
}
