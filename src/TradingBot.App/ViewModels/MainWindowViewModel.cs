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
/// 상단 차트 2/3 + 하단 자동매매 조작 1/3.
/// 종목: 스페이스X(SPCX) 전용. 시간봉 + 전략 보조지표. 토스 실데이터 읽기.
/// 실주문은 게이트 잠금 유지 (오너 해제 전).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private const string SafetyHeadlineText =
        "토스증권 실데이터 · SPCX 전용 · 실주문은 게이트 잠금 · 투자 조언 아님";

    private static readonly SKColor[] IndicatorColors =
    [
        SKColor.Parse("#38BDF8"), // sky
        SKColor.Parse("#FBBF24"), // amber
        SKColor.Parse("#A78BFA"), // violet
        SKColor.Parse("#34D399"), // emerald
        SKColor.Parse("#F472B6"), // pink
    ];

    private readonly AppHarness _harness;
    private bool _suppressSelectionEcho;

    public MainWindowViewModel()
        : this(AppHarness.CreateDefault())
    {
    }

    public MainWindowViewModel(AppHarness harness)
    {
        _harness = harness ?? throw new ArgumentNullException(nameof(harness));
        StockKindOptions = new ObservableCollection<string>(WatchlistCatalog.KindLabels);
        StrategyOptions = new ObservableCollection<string>(StrategyCatalog.Labels);
        SymbolOptions = new ObservableCollection<string> { WatchlistCatalog.SpaceXSymbol };
        TimeframeOptions = new ObservableCollection<string>(ChartTimeframeCatalog.Labels);

        SelectedStockKind = StockMarketKind.스페이스X.ToString();
        _harness.SetStockKind(StockMarketKind.스페이스X);
        SelectedStrategy = TradingStrategyKind.추세추종.ToString();
        _harness.SetStrategy(TradingStrategyKind.추세추종);
        SelectedSymbol = WatchlistCatalog.SpaceXSymbol;
        _harness.SetFocusSymbol(WatchlistCatalog.SpaceXSymbol);
        SelectedTimeframe = ChartTimeframe.분봉1.ToString();
        _harness.SetTimeframe(ChartTimeframe.분봉1);

        BuildEmptyChart();
        ApplyPanel(_harness.GetAutoTradePanel());
        ConnectionLabel = _harness.ConnectionLabel;
        ConnectionPill = ShortConnectionPill(_harness.ConnectionModeLabel);
        SafetyHeadline = SafetyHeadlineText;
        ChartSubtitle = "SPCX · 시간봉 선택 · 전략 보조지표 · 버블=체결 규모";
    }

    public ObservableCollection<string> StockKindOptions { get; }
    public ObservableCollection<string> StrategyOptions { get; }
    public ObservableCollection<string> SymbolOptions { get; }
    public ObservableCollection<string> TimeframeOptions { get; }

    [ObservableProperty] private string _title = "토스 · 스페이스X 자동매매";
    [ObservableProperty] private string _safetyHeadline = string.Empty;
    [ObservableProperty] private string _statusLine = "대기";
    [ObservableProperty] private string _sessionStatusLabel = "중지";
    [ObservableProperty] private string _watchSymbolsText = WatchlistCatalog.SpaceXSymbol;
    [ObservableProperty] private string _balanceLabel = string.Empty;
    [ObservableProperty] private string _returnRateLabel = string.Empty;
    [ObservableProperty] private string _safetyNote = string.Empty;
    [ObservableProperty] private string _selectedStockKind = "스페이스X";
    [ObservableProperty] private string _selectedStrategy = "추세추종";
    [ObservableProperty] private string _selectedSymbol = WatchlistCatalog.SpaceXSymbol;
    [ObservableProperty] private string _selectedTimeframe = "분봉1";
    [ObservableProperty] private string _stockKindDescription = string.Empty;
    [ObservableProperty] private string _strategyDescription = string.Empty;
    [ObservableProperty] private string _chartTitle = "SPCX";
    [ObservableProperty] private string _chartSubtitle = string.Empty;
    [ObservableProperty] private string _indicatorLegend = string.Empty;
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

        _harness.SetStockKind(StockMarketKind.스페이스X);
        RefreshSymbolOptions();
        ApplyPanel(_harness.GetAutoTradePanel());
        RebuildChart();
        StatusLine = "대상 · 스페이스X (SPCX) · 토스 실데이터";
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
            StatusLine = $"전략 · {s} · 보조지표 갱신";
            RebuildChart();
        }
    }

    partial void OnSelectedSymbolChanged(string value)
    {
        if (_suppressSelectionEcho)
        {
            return;
        }

        _harness.SetFocusSymbol(WatchlistCatalog.SpaceXSymbol);
        ChartTitle = $"SPCX · {SelectedTimeframe}";
        RebuildChart();
    }

    partial void OnSelectedTimeframeChanged(string value)
    {
        if (_suppressSelectionEcho)
        {
            return;
        }

        if (ChartTimeframeCatalog.TryParse(value, out var tf))
        {
            _harness.SetTimeframe(tf);
            ChartTitle = $"SPCX · {tf}";
            ChartSubtitle = ChartTimeframeCatalog.Describe(tf);
            StatusLine = $"시간봉 · {tf} · 차트 갱신 필요 시 새로고침";
            RebuildChart();
        }
    }

    private void RefreshSymbolOptions()
    {
        SymbolOptions.Clear();
        SymbolOptions.Add(WatchlistCatalog.SpaceXSymbol);
        _suppressSelectionEcho = true;
        SelectedSymbol = WatchlistCatalog.SpaceXSymbol;
        _suppressSelectionEcho = false;
        _harness.SetFocusSymbol(WatchlistCatalog.SpaceXSymbol);
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
            StatusLine = "토스 실데이터 불러오는 중…";
            _ = await _harness.GetDashboardAsync().ConfigureAwait(true);
            RefreshSymbolOptions();
            ApplyPanel(_harness.GetAutoTradePanel());
            ConnectionLabel = _harness.ConnectionLabel;
            ConnectionPill = ShortConnectionPill(_harness.ConnectionModeLabel, _harness.ConnectionLabel);
            RebuildChart();
            StatusLine = $"갱신 완료 · {ConnectionPill} · SPCX · 실주문 잠금";
        }
        catch (Exception ex)
        {
            StatusLine = "오류 · 실주문 없음";
            ConnectionLabel = $"읽기 연결 오류 — {ex.GetType().Name}";
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
        WatchSymbolsText = WatchlistCatalog.SpaceXSymbol;
        BalanceLabel = p.BalanceLabel;
        ReturnRateLabel = p.ReturnRateLabel;
        SafetyNote = p.SafetyNote;
        CanStart = p.CanStart;
        CanStop = p.CanStop;
        SelectedStockKind = StockMarketKind.스페이스X.ToString();
        SelectedStrategy = p.StrategyLabel;
        StockKindDescription = WatchlistCatalog.Describe(StockMarketKind.스페이스X);
        StrategyDescription = p.StrategyDescription;
        SelectedSymbol = WatchlistCatalog.SpaceXSymbol;
        ChartTitle = $"SPCX · {SelectedTimeframe}";
        SafetyHeadline = SafetyHeadlineText;
        if (!SafetyNote.Contains("실주문", StringComparison.Ordinal))
        {
            SafetyNote = string.IsNullOrWhiteSpace(SafetyNote)
                ? "토스 실데이터 · SPCX · 실주문 게이트 잠금."
                : $"{SafetyNote} 실주문 게이트 잠금.";
        }

        _suppressSelectionEcho = false;
    }

    private void BuildEmptyChart() => RebuildChart();

    private static string ShortConnectionPill(string modeLabel, string? ownerLabel = null)
    {
        if (!string.IsNullOrWhiteSpace(ownerLabel)
            && (ownerLabel.Contains("오류", StringComparison.Ordinal)
                || ownerLabel.Contains("실패", StringComparison.Ordinal)))
        {
            return "오류";
        }

        if (modeLabel.Contains("실 HTTP", StringComparison.Ordinal)
            || modeLabel.Contains("토스", StringComparison.Ordinal))
        {
            return "토스 실연결";
        }

        if (modeLabel.Contains("오류", StringComparison.Ordinal))
        {
            return "오류";
        }

        return "mock";
    }

    private void RebuildChart()
    {
        var (candles, markers, indicators) = _harness.GetChartData();
        var financial = candles
            .Select(c => new FinancialPoint(c.Time.UtcDateTime, c.High, c.Open, c.Close, c.Low))
            .ToArray();

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

        var seriesList = new List<ISeries>
        {
            new CandlesticksSeries<FinancialPoint>
            {
                Name = "SPCX",
                Values = financial,
                UpFill = new SolidColorPaint(SKColor.Parse("#26A69A")),
                UpStroke = new SolidColorPaint(SKColor.Parse("#26A69A")) { StrokeThickness = 1 },
                DownFill = new SolidColorPaint(SKColor.Parse("#EF5350")),
                DownStroke = new SolidColorPaint(SKColor.Parse("#EF5350")) { StrokeThickness = 1 },
                MaxBarWidth = 5,
                ZIndex = 0,
            },
        };

        // 전략 보조지표 라인
        var legendParts = new List<string>();
        for (var i = 0; i < indicators.Count; i++)
        {
            var line = indicators[i];
            var color = IndicatorColors[i % IndicatorColors.Length];
            var points = new List<DateTimePoint>(candles.Count);
            for (var j = 0; j < candles.Count; j++)
            {
                var v = j < line.Values.Count ? line.Values[j] : null;
                if (v is double d && !double.IsNaN(d))
                {
                    points.Add(new DateTimePoint(candles[j].Time.UtcDateTime, d));
                }
            }

            seriesList.Add(new LineSeries<DateTimePoint>
            {
                Name = line.Name,
                Values = points,
                GeometrySize = 0,
                LineSmoothness = 0,
                Fill = null,
                Stroke = new SolidColorPaint(color) { StrokeThickness = 1.6f },
                ZIndex = 5 + i,
            });
            legendParts.Add(line.Name);
        }

        seriesList.Add(new ScatterSeries<WeightedPoint>
        {
            Name = "매수규모",
            Values = buyBubbles,
            MinGeometrySize = 10,
            GeometrySize = 52,
            Fill = buyFill,
            Stroke = buyStroke,
            ZIndex = 10,
        });
        seriesList.Add(new ScatterSeries<WeightedPoint>
        {
            Name = "매도규모",
            Values = sellBubbles,
            MinGeometrySize = 10,
            GeometrySize = 52,
            Fill = sellFill,
            Stroke = sellStroke,
            ZIndex = 10,
        });

        Series = seriesList.ToArray();
        IndicatorLegend = legendParts.Count == 0
            ? "보조지표 없음"
            : $"보조지표: {string.Join(" · ", legendParts)} ({SelectedStrategy})";

        var isDaily = SelectedTimeframe.Contains("일", StringComparison.Ordinal);
        XAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#1A1F2E")) { StrokeThickness = 1 },
                Labeler = value => value > 0
                    ? new DateTime((long)value).ToString(isDaily ? "MM-dd" : "HH:mm")
                    : string.Empty,
                UnitWidth = isDaily
                    ? TimeSpan.FromDays(1).Ticks
                    : TimeSpan.FromMinutes(1).Ticks,
                MinStep = isDaily
                    ? TimeSpan.FromDays(1).Ticks
                    : TimeSpan.FromMinutes(5).Ticks,
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
                Labeler = value => value.ToString("N2"),
            },
        ];

        DrawMarginFrame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(SKColor.Parse("#0A0C10")),
            Stroke = new SolidColorPaint(SKColor.Parse("#1E293B")) { StrokeThickness = 1 },
        };
    }
}
