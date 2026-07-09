using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using TradingBot.App.Services;
using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.App.ViewModels;

/// <summary>
/// 상단 차트 2/3 + 하단 자동매매 조작 1/3.
/// ChartFanatics 버블(규모=거래대금) + TradingView식 캔들·거래량·보조지표.
/// 종목 SPCX · 토스 실데이터 · 실주문 게이트 잠금.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private const string SafetyHeadlineText =
        "토스증권 실데이터 · SPCX 전용 · 실주문은 게이트 잠금 · 투자 조언 아님";

    private static readonly SKColor[] IndicatorColors =
    [
        SKColor.Parse("#38BDF8"), // sky SMA20
        SKColor.Parse("#FBBF24"), // amber SMA60
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
        ChartSubtitle = "버블 크기 = 거래대금 · 초록=상승 · 빨강=하락 · 하단=거래량";
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
    [ObservableProperty] private string _lastPriceLabel = string.Empty;
    [ObservableProperty] private string _connectionLabel = "연결 확인 전";
    [ObservableProperty] private string _connectionPill = "mock";
    [ObservableProperty] private bool _canStart = true;
    [ObservableProperty] private bool _canStop;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private ISeries[] _series = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();
    [ObservableProperty] private DrawMarginFrame? _drawMarginFrame;
    [ObservableProperty] private LiveChartsCore.Measure.Margin? _drawMargin;

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
            StatusLine = $"전략 · {s} · 보조지표·버블 갱신";
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
            ChartSubtitle = ChartTimeframeCatalog.Describe(tf) + " · 버블=거래대금";
            StatusLine = $"시간봉 · {tf} · 새로고침으로 실봉 갱신";
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
            StatusLine = $"갱신 완료 · {ConnectionPill} · SPCX 버블차트 · 실주문 잠금";
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
        if (candles.Count == 0)
        {
            Series = Array.Empty<ISeries>();
            LastPriceLabel = string.Empty;
            return;
        }

        var isDaily = SelectedTimeframe.Contains("일", StringComparison.Ordinal)
                      || _harness.Timeframe == ChartTimeframe.일봉;

        // ── 캔들 (TradingView식 두꺼운 봉) ──────────────────────────
        var financial = candles
            .Select(c => new FinancialPoint(c.Time.UtcDateTime, c.High, c.Open, c.Close, c.Low))
            .ToArray();

        var maxBarWidth = candles.Count switch
        {
            > 140 => 4.0,
            > 100 => 6.0,
            > 70 => 8.0,
            > 40 => 11.0,
            _ => 14.0,
        };

        var upColor = SKColor.Parse("#26A69A");
        var downColor = SKColor.Parse("#EF5350");

        // ── 버블 (규모 = 거래대금) ───────────────────────────────────
        var buyBubbles = markers
            .Where(m => m.Side == TradeMarkerSide.매수)
            .Select(m => new WeightedPoint(
                m.Time.UtcDateTime.Ticks,
                m.Price,
                Math.Clamp(m.SizeWeight, 0.35, 5.5)))
            .ToArray();
        var sellBubbles = markers
            .Where(m => m.Side == TradeMarkerSide.매도)
            .Select(m => new WeightedPoint(
                m.Time.UtcDateTime.Ticks,
                m.Price,
                Math.Clamp(m.SizeWeight, 0.35, 5.5)))
            .ToArray();

        // 반투명 버블 — 캔들 가리지 않음
        var buyFill = new SolidColorPaint(new SKColor(0x39, 0xFF, 0x14, 0x88));
        var sellFill = new SolidColorPaint(new SKColor(0xFF, 0x2D, 0x2D, 0x90));
        var buyStroke = new SolidColorPaint(new SKColor(0x00, 0xE6, 0x76, 0x55)) { StrokeThickness = 1.0f };
        var sellStroke = new SolidColorPaint(new SKColor(0xFF, 0x52, 0x52, 0x55)) { StrokeThickness = 1.0f };

        // ── 거래량 컬럼 (ScalesYAt = 1) ──────────────────────────────
        var volUp = new List<DateTimePoint>(candles.Count);
        var volDown = new List<DateTimePoint>(candles.Count);
        foreach (var c in candles)
        {
            var pt = new DateTimePoint(c.Time.UtcDateTime, Math.Max(0, c.Volume));
            if (c.Close >= c.Open)
            {
                volUp.Add(pt);
            }
            else
            {
                volDown.Add(pt);
            }
        }

        var seriesList = new List<ISeries>
        {
            new CandlesticksSeries<FinancialPoint>
            {
                Name = "SPCX",
                Values = financial,
                UpFill = new SolidColorPaint(upColor),
                UpStroke = new SolidColorPaint(upColor) { StrokeThickness = 1.2f },
                DownFill = new SolidColorPaint(downColor),
                DownStroke = new SolidColorPaint(downColor) { StrokeThickness = 1.2f },
                MaxBarWidth = maxBarWidth,
                ScalesYAt = 0,
                ZIndex = 2,
            },
        };

        // 전략 보조지표 (가격 축)
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
                Stroke = new SolidColorPaint(color) { StrokeThickness = 1.8f },
                ScalesYAt = 0,
                ZIndex = 4 + i,
            });
            legendParts.Add(line.Name);
        }

        // 버블 — 캔들 위, ChartFanatics
        seriesList.Add(new ScatterSeries<WeightedPoint>
        {
            Name = "상승규모",
            Values = buyBubbles,
            MinGeometrySize = 7,
            GeometrySize = 48,
            Fill = buyFill,
            Stroke = buyStroke,
            ScalesYAt = 0,
            ZIndex = 12,
        });
        seriesList.Add(new ScatterSeries<WeightedPoint>
        {
            Name = "하락규모",
            Values = sellBubbles,
            MinGeometrySize = 7,
            GeometrySize = 48,
            Fill = sellFill,
            Stroke = sellStroke,
            ScalesYAt = 0,
            ZIndex = 12,
        });

        // 거래량 패널 (하단 Y 축)
        seriesList.Add(new ColumnSeries<DateTimePoint>
        {
            Name = "거래량↑",
            Values = volUp,
            Fill = new SolidColorPaint(new SKColor(0x26, 0xA6, 0x9A, 0x99)),
            Stroke = null,
            MaxBarWidth = maxBarWidth,
            ScalesYAt = 1,
            ZIndex = 1,
            Padding = 1,
        });
        seriesList.Add(new ColumnSeries<DateTimePoint>
        {
            Name = "거래량↓",
            Values = volDown,
            Fill = new SolidColorPaint(new SKColor(0xEF, 0x53, 0x50, 0x99)),
            Stroke = null,
            MaxBarWidth = maxBarWidth,
            ScalesYAt = 1,
            ZIndex = 1,
            Padding = 1,
        });

        Series = seriesList.ToArray();

        var last = candles[^1];
        var first = candles[0];
        var chg = first.Close > 0 ? (last.Close - first.Close) / first.Close * 100.0 : 0;
        LastPriceLabel = $"종가 {last.Close:N2} · 구간 {chg:+0.00;-0.00;0}% · 봉 {candles.Count}";

        IndicatorLegend = legendParts.Count == 0
            ? $"버블 차트 · 규모=거래대금 · 봉 {candles.Count}"
            : $"보조지표: {string.Join(" · ", legendParts)} ({SelectedStrategy}) · 버블=거래대금";

        var dashGrid = new SolidColorPaint(SKColor.Parse("#1A2332"))
        {
            StrokeThickness = 1,
            PathEffect = new DashEffect([4, 6]),
        };

        XAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = dashGrid,
                ShowSeparatorLines = true,
                Labeler = value =>
                {
                    if (value <= 0)
                    {
                        return string.Empty;
                    }

                    var dt = new DateTime((long)value);
                    return isDaily ? dt.ToString("MM-dd") : dt.ToString("HH:mm");
                },
                UnitWidth = isDaily
                    ? TimeSpan.FromDays(1).Ticks
                    : TimeSpan.FromMinutes(1).Ticks,
                MinStep = isDaily
                    ? TimeSpan.FromDays(1).Ticks
                    : TimeSpan.FromMinutes(5).Ticks,
                TextSize = 10,
                Padding = new LiveChartsCore.Drawing.Padding(0, 4, 0, 0),
            },
        ];

        // Y0 = 가격 (위 ~72%), Y1 = 거래량 (아래 ~28%) — TradingView 레이아웃
        var priceMin = candles.Min(c => c.Low);
        var priceMax = candles.Max(c => c.High);
        var pad = Math.Max(0.5, (priceMax - priceMin) * 0.04);
        var volMax = candles.Max(c => c.Volume);
        if (volMax <= 0)
        {
            volMax = 1;
        }

        YAxes =
        [
            new Axis
            {
                Name = "가격",
                NamePaint = new SolidColorPaint(SKColor.Parse("#64748B")) { SKTypeface = SKTypeface.Default },
                NameTextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                SeparatorsPaint = dashGrid,
                ShowSeparatorLines = true,
                Position = AxisPosition.End,
                TextSize = 10,
                Labeler = value => value.ToString("N2"),
                MinLimit = priceMin - pad,
                MaxLimit = priceMax + pad,
            },
            new Axis
            {
                Name = "Vol",
                NamePaint = new SolidColorPaint(SKColor.Parse("#475569")),
                NameTextSize = 9,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColors.Transparent),
                ShowSeparatorLines = false,
                Position = AxisPosition.Start,
                TextSize = 9,
                Labeler = value => FormatVolume(value),
                MinLimit = 0,
                MaxLimit = volMax * 4.2, // 거래량을 하단 약 1/4 높이에 압축 표시
            },
        ];

        DrawMarginFrame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(SKColor.Parse("#0B0F14")),
            Stroke = new SolidColorPaint(SKColor.Parse("#1E293B")) { StrokeThickness = 1 },
        };

        // 플롯 여백 최소화 (프로 차트 밀도)
        DrawMargin = new LiveChartsCore.Measure.Margin(48, 12, 56, 28);
    }

    private static string FormatVolume(double v)
    {
        if (v >= 1_000_000)
        {
            return $"{v / 1_000_000:0.#}M";
        }

        if (v >= 1_000)
        {
            return $"{v / 1_000:0.#}K";
        }

        return v.ToString("N0");
    }
}
