using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TradingBot.App.Services;
using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.App.ViewModels;

/// <summary>
/// 상단 차트 2/3 + 하단 자동매매 필수 조작 1/3.
/// 버블 크기 = 체결 규모(수량×가격 · 거래대금). 연습 전용 · 투자 조언 아님 · 실주문 차단.
/// 대상 주식 콤보 = <see cref="WatchlistCatalog.KindLabels"/> (Domain 코어3 병합 시 자동 노출).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// Domain <c>StockMarketKind.나스닥코어3</c> 이름 (병합 전에도 문자열로 탐지).
    /// 심볼 목록은 Domain <see cref="WatchlistCatalog"/> 가 소유 (QQQ·NVDA·AAPL).
    /// </summary>
    public const string Core3KindName = "나스닥코어3";

    private const string PracticeSafetyHeadline =
        "실거래 잠김 · 실제 주문은 나가지 않습니다 · 연습 전용 · 투자 조언 아님";

    private readonly AppHarness _harness;
    private bool _suppressSelectionEcho;

    public MainWindowViewModel()
        : this(AppHarness.CreateDefault())
    {
    }

    public MainWindowViewModel(AppHarness harness)
    {
        _harness = harness ?? throw new ArgumentNullException(nameof(harness));
        // KindLabels는 AllKinds 기반 — Domain이 나스닥코어3를 추가하면 콤보에 자동 반영.
        StockKindOptions = new ObservableCollection<string>(WatchlistCatalog.KindLabels);
        StrategyOptions = new ObservableCollection<string>(StrategyCatalog.Labels);
        SymbolOptions = new ObservableCollection<string>();
        var defaultKind = ResolveDefaultStockKind();
        SelectedStockKind = defaultKind.ToString();
        _harness.SetStockKind(defaultKind);
        SelectedStrategy = TradingStrategyKind.단순연습전략.ToString();
        RefreshSymbolOptions();
        BuildEmptyChart();
        ApplyPanel(_harness.GetAutoTradePanel());
        ConnectionLabel = _harness.ConnectionLabel;
        ConnectionPill = ShortConnectionPill(_harness.ConnectionModeLabel);
        SafetyHeadline = PracticeSafetyHeadline;
    }

    /// <summary>
    /// Domain에 나스닥코어3가 있고 KindLabels에 있으면 기본 선택; 없으면 나스닥.
    /// </summary>
    public static StockMarketKind ResolveDefaultStockKind()
    {
        if (TryGetCore3Kind(out var core3)
            && WatchlistCatalog.KindLabels.Contains(core3.ToString(), StringComparer.Ordinal))
        {
            return core3;
        }

        return StockMarketKind.나스닥;
    }

    /// <summary>
    /// 병합 전·후 모두 컴파일 가능한 코어3 탐지 (enum 멤버 직접 참조 금지).
    /// </summary>
    public static bool TryGetCore3Kind(out StockMarketKind kind) =>
        Enum.TryParse(Core3KindName, ignoreCase: false, out kind)
        && Enum.IsDefined(kind);

    public ObservableCollection<string> StockKindOptions { get; }
    public ObservableCollection<string> StrategyOptions { get; }
    public ObservableCollection<string> SymbolOptions { get; }

    [ObservableProperty] private string _title = "자동매매 콕핏";
    [ObservableProperty] private string _safetyHeadline = string.Empty;
    [ObservableProperty] private string _statusLine = "대기";
    [ObservableProperty] private string _sessionStatusLabel = "중지";
    [ObservableProperty] private string _watchSymbolsText = string.Empty;
    [ObservableProperty] private string _balanceLabel = string.Empty;
    [ObservableProperty] private string _returnRateLabel = string.Empty;
    [ObservableProperty] private string _safetyNote = string.Empty;
    [ObservableProperty] private string _selectedStockKind = "나스닥";
    [ObservableProperty] private string _selectedStrategy = "단순연습전략";
    [ObservableProperty] private string _selectedSymbol = "AAPL";
    [ObservableProperty] private string _stockKindDescription = string.Empty;
    [ObservableProperty] private string _strategyDescription = string.Empty;
    [ObservableProperty] private string _chartTitle = "차트";
    [ObservableProperty] private string _connectionLabel = "연결 확인 전";
    [ObservableProperty] private string _connectionPill = "mock";
    [ObservableProperty] private bool _canStart = true;
    [ObservableProperty] private bool _canStop;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private ISeries[] _series = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();
    [ObservableProperty] private DrawMarginFrame? _drawMarginFrame;

    partial void OnSelectedStockKindChanged(string value)
    {
        if (_suppressSelectionEcho)
        {
            return;
        }

        if (Enum.TryParse<StockMarketKind>(value, out var kind))
        {
            _harness.SetStockKind(kind);
            RefreshSymbolOptions();
            var panel = _harness.GetAutoTradePanel();
            ApplyPanel(panel);
            RebuildChart();
            StatusLine = kind.ToString().Equals(Core3KindName, StringComparison.Ordinal)
                ? $"대상 · {kind} (코어3 연습 유니버스) · 연습 전용 · 투자 조언 아님"
                : $"대상 · {kind} · 연습 전용 · 투자 조언 아님";
        }
    }

    partial void OnSelectedStrategyChanged(string value)
    {
        if (_suppressSelectionEcho)
        {
            return;
        }

        if (Enum.TryParse<TradingStrategyKind>(value, out var s))
        {
            _harness.SetStrategy(s);
            StrategyDescription = StrategyCatalog.Describe(s);
            StatusLine = $"전략 · {s} · {StrategyDescription}";
        }
    }

    partial void OnSelectedSymbolChanged(string value)
    {
        if (_suppressSelectionEcho || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _harness.SetFocusSymbol(value);
        ChartTitle = $"{SelectedStockKind} · {value}";
        RebuildChart();
    }

    private void RefreshSymbolOptions()
    {
        // Session may carry live holdings∪catalog watch after real bind; prefer session list.
        var symbols = _harness.Session.ResolveWatchSymbols();
        if (symbols.Length == 0)
        {
            if (!Enum.TryParse<StockMarketKind>(SelectedStockKind, out var kind))
            {
                kind = StockMarketKind.나스닥;
            }

            symbols = WatchlistCatalog.ResolveSymbols(kind).ToArray();
        }

        SymbolOptions.Clear();
        foreach (var s in symbols)
        {
            SymbolOptions.Add(s);
        }

        var focus = _harness.GetAutoTradePanel().FocusSymbol;
        _suppressSelectionEcho = true;
        SelectedSymbol = symbols.Contains(focus, StringComparer.OrdinalIgnoreCase)
            ? focus
            : symbols[0];
        _suppressSelectionEcho = false;
        _harness.SetFocusSymbol(SelectedSymbol);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusLine = "불러오는 중…";
            _ = await _harness.GetDashboardAsync().ConfigureAwait(true);
            // Live 읽기 바인딩 후 워치·잔액 라벨 반영
            RefreshSymbolOptions();
            ApplyPanel(_harness.GetAutoTradePanel());
            ConnectionLabel = _harness.ConnectionLabel;
            ConnectionPill = ShortConnectionPill(_harness.ConnectionModeLabel);
            RebuildChart();
            StatusLine = $"갱신 완료 · {ConnectionPill} · 실주문 없음 · 연습 전용 · 투자 조언 아님";
        }
        catch
        {
            StatusLine = "오류 · 실주문은 하지 않습니다 · 연습 전용 · 투자 조언 아님";
            ConnectionLabel = "읽기 연결 오류";
            ConnectionPill = "오류";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        StatusLine = _harness.StartAutoTrade();
        ApplyPanel(_harness.GetAutoTradePanel());
        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        StatusLine = _harness.StopAutoTrade();
        ApplyPanel(_harness.GetAutoTradePanel());
        await RefreshAsync().ConfigureAwait(true);
    }

    private void ApplyPanel(AutoTradePanelSnapshot p)
    {
        _suppressSelectionEcho = true;
        SessionStatusLabel = p.SessionStatusLabel;
        WatchSymbolsText = p.WatchSymbolsText;
        BalanceLabel = p.BalanceLabel;
        ReturnRateLabel = p.ReturnRateLabel;
        SafetyNote = p.SafetyNote;
        CanStart = p.CanStart;
        CanStop = p.CanStop;
        SelectedStockKind = p.StockKindLabel;
        SelectedStrategy = p.StrategyLabel;
        StockKindDescription = p.StockKindDescription;
        StrategyDescription = p.StrategyDescription;
        if (SymbolOptions.Contains(p.FocusSymbol))
        {
            SelectedSymbol = p.FocusSymbol;
        }

        ChartTitle = $"{p.StockKindLabel} · {p.FocusSymbol}";
        SafetyHeadline = PracticeSafetyHeadline;
        // Session SafetyNote에 투자 조언 고지가 없으면 ViewModel에서 보강 (Domain 병합 전 대비).
        if (!SafetyNote.Contains("투자 조언", StringComparison.Ordinal)
            && !SafetyNote.Contains("투자 권유", StringComparison.Ordinal))
        {
            SafetyNote = string.IsNullOrWhiteSpace(SafetyNote)
                ? "연습 세션만 제어합니다. 실주문 차단. 투자 조언 아님."
                : $"{SafetyNote} 투자 조언 아님.";
        }

        _suppressSelectionEcho = false;
    }

    private void BuildEmptyChart() => RebuildChart();

    private static string ShortConnectionPill(string modeLabel)
    {
        if (modeLabel.Contains("실 HTTP", StringComparison.Ordinal))
        {
            return "토스 읽기";
        }

        if (modeLabel.Contains("오류", StringComparison.Ordinal))
        {
            return "오류";
        }

        return "mock";
    }

    private void RebuildChart()
    {
        var (candles, markers) = _harness.GetChartData();
        var financial = candles
            .Select(c => new FinancialPoint(c.Time.UtcDateTime, c.High, c.Open, c.Close, c.Low))
            .ToArray();

        // Weight = 체결 규모 (클수록 큰 버블)
        var buyBubbles = markers
            .Where(m => m.Side == TradeMarkerSide.매수)
            .Select(m => new WeightedPoint(
                m.Time.UtcDateTime.Ticks,
                m.Price,
                Math.Max(0.2, m.SizeWeight)))
            .ToArray();
        var sellBubbles = markers
            .Where(m => m.Side == TradeMarkerSide.매도)
            .Select(m => new WeightedPoint(
                m.Time.UtcDateTime.Ticks,
                m.Price,
                Math.Max(0.2, m.SizeWeight)))
            .ToArray();

        var buyFill = new SolidColorPaint(new SKColor(0x39, 0xFF, 0x14, 0xB8));
        var sellFill = new SolidColorPaint(new SKColor(0xFF, 0x2D, 0x2D, 0xC0));
        var buyStroke = new SolidColorPaint(new SKColor(0x00, 0xE6, 0x76, 0x90)) { StrokeThickness = 1.2f };
        var sellStroke = new SolidColorPaint(new SKColor(0xFF, 0x52, 0x52, 0x90)) { StrokeThickness = 1.2f };

        Series =
        [
            new CandlesticksSeries<FinancialPoint>
            {
                Name = "가격",
                Values = financial,
                UpFill = new SolidColorPaint(SKColor.Parse("#26A69A")),
                UpStroke = new SolidColorPaint(SKColor.Parse("#26A69A")) { StrokeThickness = 1 },
                DownFill = new SolidColorPaint(SKColor.Parse("#EF5350")),
                DownStroke = new SolidColorPaint(SKColor.Parse("#EF5350")) { StrokeThickness = 1 },
                MaxBarWidth = 5,
                ZIndex = 0,
            },
            new ScatterSeries<WeightedPoint>
            {
                Name = "매수규모",
                Values = buyBubbles,
                MinGeometrySize = 10,
                GeometrySize = 52,
                Fill = buyFill,
                Stroke = buyStroke,
                ZIndex = 10,
            },
            new ScatterSeries<WeightedPoint>
            {
                Name = "매도규모",
                Values = sellBubbles,
                MinGeometrySize = 10,
                GeometrySize = 52,
                Fill = sellFill,
                Stroke = sellStroke,
                ZIndex = 10,
            },
        ];

        XAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#1A1F2E")) { StrokeThickness = 1 },
                Labeler = value => value > 0
                    ? new DateTime((long)value).ToString("HH:mm")
                    : string.Empty,
                UnitWidth = TimeSpan.FromMinutes(1).Ticks,
                MinStep = TimeSpan.FromMinutes(5).Ticks,
                TextSize = 11,
            },
        ];

        YAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#1A1F2E")) { StrokeThickness = 1 },
                Position = LiveChartsCore.Measure.AxisPosition.End,
                TextSize = 11,
                Labeler = value => value.ToString("N0"),
            },
        ];

        DrawMarginFrame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(SKColor.Parse("#0A0C10")),
            Stroke = new SolidColorPaint(SKColor.Parse("#1E293B")) { StrokeThickness = 1 },
        };
    }
}
