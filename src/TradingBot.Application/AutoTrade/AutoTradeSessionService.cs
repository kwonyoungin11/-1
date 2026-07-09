using TradingBot.Domain;

namespace TradingBot.Application;

/// <summary>
/// 자동매매 연습 세션. 시작/종료는 연습 루프만 제어하며 실주문을 열지 않음.
/// 실계좌 읽기 연결 시 외부 잔액·워치 심볼을 바인딩할 수 있으나 실주문은 차단 유지.
/// </summary>
public sealed class AutoTradeSessionService
{
    /// <summary>CreateDefault / 연습 세션 기본 시작 잔액.</summary>
    public const decimal DefaultPracticeStartingBalance = 100_000m;

    private readonly object _gate = new();
    private AutoTradeSessionStatus _status = AutoTradeSessionStatus.중지;
    private StockMarketKind _stockKind = StockMarketKind.비전마린;
    private TradingStrategyKind _strategy = VmarOneMinuteScalpPreset.Strategy;
    private ChartTimeframe _timeframe = VmarOneMinuteScalpPreset.Timeframe;
    private string? _focusSymbol = WatchlistCatalog.VmarSymbol;
    private decimal _startingBalance = DefaultPracticeStartingBalance;
    private decimal _balance = DefaultPracticeStartingBalance;
    private decimal _realizedPnl;
    private string[]? _externalWatchSymbols;
    private string _dataSourceLabel = "연습";
    private bool _startingBalanceSetFromExternal;

    public StockMarketKind StockKind
    {
        get { lock (_gate) { return _stockKind; } }
        set
        {
            lock (_gate)
            {
                _stockKind = value;
                _focusSymbol = null; // 종류 바꾸면 첫 종목으로
                // 사용자가 카탈로그 종류를 바꾸면 외부 워치 오버라이드 해제
                _externalWatchSymbols = null;
            }
        }
    }

    public TradingStrategyKind Strategy
    {
        get { lock (_gate) { return _strategy; } }
        set { lock (_gate) { _strategy = value; } }
    }

    /// <summary>차트 시간봉 (토스 1m / 1d).</summary>
    public ChartTimeframe Timeframe
    {
        get { lock (_gate) { return _timeframe; } }
        set { lock (_gate) { _timeframe = value; } }
    }

    /// <summary>
    /// 차트·포커스 종목. 알려진 심볼(카탈로그)만 수락·정규화.
    /// 미지 심볼은 거부(이전 값 유지). 실주문 경로 아님.
    /// </summary>
    public string? FocusSymbol
    {
        get { lock (_gate) { return _focusSymbol; } }
        set
        {
            lock (_gate)
            {
                var normalized = WatchlistCatalog.NormalizeKnownSymbol(value);
                if (normalized is not null)
                {
                    _focusSymbol = normalized;
                }
                // unknown → keep previous (fail-closed for free-form tickers)
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

    /// <summary>잔액 라벨 접미 (연습 / 실계좌 읽기). 실주문 아님.</summary>
    public string DataSourceLabel
    {
        get { lock (_gate) { return _dataSourceLabel; } }
    }

    /// <summary>
    /// 외부(실계좌 읽기) 잔액을 세션에 반영. 실주문·전송 없음.
    /// <paramref name="setStartingIfUnset"/> 가 true 이고 시작 잔액이 아직 기본 연습값이면
    /// 한 번만 StartingBalance 를 함께 설정한다.
    /// </summary>
    public void ApplyExternalBalance(decimal balance, bool setStartingIfUnset)
    {
        lock (_gate)
        {
            _balance = balance;
            if (setStartingIfUnset
                && !_startingBalanceSetFromExternal
                && _startingBalance == DefaultPracticeStartingBalance)
            {
                _startingBalance = balance;
                _startingBalanceSetFromExternal = true;
                _realizedPnl = 0m;
            }
        }
    }

    /// <summary>
    /// 외부 워치 심볼(보유 + 카탈로그 합집합 등).
    /// 알려진 심볼이 있으면 그것만 사용; 없으면 현재 stock kind 카탈로그.
    /// </summary>
    public void ApplyExternalWatchSymbols(string[] symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        lock (_gate)
        {
            var known = symbols
                .Select(WatchlistCatalog.NormalizeKnownSymbol)
                .Where(s => s is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (known.Length > 0)
            {
                _externalWatchSymbols = known;
                return;
            }

            var catalog = WatchlistCatalog.ResolveSymbols(_stockKind).ToArray();
            _externalWatchSymbols = catalog.Length > 0
                ? catalog
                : [WatchlistCatalog.VmarSymbol];
        }
    }

    /// <summary>
    /// 패널 잔액 접미 라벨. 예: "연습", "실계좌 읽기".
    /// </summary>
    public void SetDataSourceLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        lock (_gate)
        {
            _dataSourceLabel = string.IsNullOrWhiteSpace(label) ? "연습" : label.Trim();
        }
    }

    public string[] ResolveWatchSymbols()
    {
        lock (_gate)
        {
            return ResolveWatchSymbolsUnlocked();
        }
    }

    private string[] ResolveWatchSymbolsUnlocked()
    {
        if (_externalWatchSymbols is { Length: > 0 })
        {
            return (string[])_externalWatchSymbols.Clone();
        }

        return WatchlistCatalog.ResolveSymbols(_stockKind).ToArray();
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
        if (!string.IsNullOrWhiteSpace(_focusSymbol)
            && WatchlistCatalog.IsKnownSymbol(_focusSymbol))
        {
            return WatchlistCatalog.NormalizeKnownSymbol(_focusSymbol)
                   ?? CatalogPrimaryOrVmar();
        }

        return CatalogPrimaryOrVmar();
    }

    private string CatalogPrimaryOrVmar()
    {
        var catalog = WatchlistCatalog.ResolveSymbols(_stockKind);
        return catalog.Count > 0 ? catalog[0] : WatchlistCatalog.VmarSymbol;
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
            var focus = ResolveFocusSymbolUnlocked();
            ownerMessage =
                $"자동매매 시작 · {focus} · {_strategy} · 시간봉 {_timeframe} · 실주문은 잠금(게이트) 유지.";
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
            ownerMessage = "자동매매 종료 · 실주문 없음.";
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
            var symbols = ResolveWatchSymbolsUnlocked();
            var focus = ResolveFocusSymbolUnlocked();
            var source = _dataSourceLabel;
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
                BalanceLabel = $"잔액 {_balance:N2} ({source})",
                ReturnRatePercent = rate,
                ReturnRateLabel = $"수익률 {rate:N2}%",
                CanStart = _status == AutoTradeSessionStatus.중지,
                CanStop = _status == AutoTradeSessionStatus.실행중,
                SafetyNote =
                    $"토스증권 실데이터 · {focus} · dry-run/연습 모드 · 실주문 차단. 버블=체결 규모.",
            };
        }
    }
}

