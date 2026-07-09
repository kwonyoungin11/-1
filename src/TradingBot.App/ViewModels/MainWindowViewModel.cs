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
/// 매수/매도는 차트 마커만 (실행 주문 버튼 없음 · 실주문 차단).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppHarness _harness;

    public MainWindowViewModel()
        : this(AppHarness.CreateDefault())
    {
    }

    public MainWindowViewModel(AppHarness harness)
    {
        _harness = harness ?? throw new ArgumentNullException(nameof(harness));
        StockKindOptions = new ObservableCollection<string>(["나스닥", "미국주식", "국내주식"]);
        StrategyOptions = new ObservableCollection<string>(["단순연습전략", "관망만"]);
        SelectedStockKind = "나스닥";
        SelectedStrategy = "단순연습전략";
        BuildEmptyChart();
        ApplyPanel(_harness.GetAutoTradePanel());
        SafetyHeadline = "실거래 잠김 · 실제 주문은 나가지 않습니다";
    }

    public ObservableCollection<string> StockKindOptions { get; }
    public ObservableCollection<string> StrategyOptions { get; }

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
    [ObservableProperty] private string _chartTitle = "차트";
    [ObservableProperty] private bool _canStart = true;
    [ObservableProperty] private bool _canStop;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private ISeries[] _series = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();

    partial void OnSelectedStockKindChanged(string value)
    {
        if (Enum.TryParse<StockMarketKind>(value, out var kind))
        {
            _harness.SetStockKind(kind);
            var panel = _harness.GetAutoTradePanel();
            WatchSymbolsText = panel.WatchSymbolsText;
            ChartTitle = $"{value} · {panel.WatchSymbolsText.Split(',').FirstOrDefault()?.Trim() ?? ""}";
        }
    }

    partial void OnSelectedStrategyChanged(string value)
    {
        if (Enum.TryParse<TradingStrategyKind>(value, out var s))
        {
            _harness.SetStrategy(s);
        }
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
            ApplyPanel(_harness.GetAutoTradePanel());
            RebuildChart();
            StatusLine = "갱신 완료 · 실주문 없음";
        }
        catch
        {
            StatusLine = "오류 · 실주문은 하지 않습니다";
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
        SessionStatusLabel = p.SessionStatusLabel;
        WatchSymbolsText = p.WatchSymbolsText;
        BalanceLabel = p.BalanceLabel;
        ReturnRateLabel = p.ReturnRateLabel;
        SafetyNote = p.SafetyNote;
        CanStart = p.CanStart;
        CanStop = p.CanStop;
        SelectedStockKind = p.StockKindLabel;
        SelectedStrategy = p.StrategyLabel;
        ChartTitle = $"{p.StockKindLabel} · {p.WatchSymbolsText.Split(',').FirstOrDefault()?.Trim() ?? "종목"}";
        SafetyHeadline = "실거래 잠김 · 실제 주문은 나가지 않습니다 · 연습 모드";
    }

    private void BuildEmptyChart() => RebuildChart();

    private void RebuildChart()
    {
        var (candles, markers) = _harness.GetChartData();
        var financial = candles
            .Select(c => new FinancialPoint(c.Time.UtcDateTime, c.High, c.Open, c.Close, c.Low))
            .ToArray();

        var buyPoints = markers
            .Where(m => m.Side == TradeMarkerSide.매수)
            .Select(m => new DateTimePoint(m.Time.UtcDateTime, m.Price))
            .ToArray();
        var sellPoints = markers
            .Where(m => m.Side == TradeMarkerSide.매도)
            .Select(m => new DateTimePoint(m.Time.UtcDateTime, m.Price))
            .ToArray();

        // 트레이딩뷰 스타일 다크 캔들 + 매수(초록) / 매도(빨강) 마커
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
                MaxBarWidth = 8,
            },
            new ScatterSeries<DateTimePoint>
            {
                Name = "매수",
                Values = buyPoints,
                GeometrySize = 16,
                Fill = new SolidColorPaint(SKColor.Parse("#00E676")),
                Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 1 },
            },
            new ScatterSeries<DateTimePoint>
            {
                Name = "매도",
                Values = sellPoints,
                GeometrySize = 16,
                Fill = new SolidColorPaint(SKColor.Parse("#FF5252")),
                Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 1 },
            },
        ];

        XAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#1E293B")) { StrokeThickness = 1 },
                Labeler = value => value > 0 ? new DateTime((long)value).ToString("MM-dd HH:mm") : string.Empty,
                UnitWidth = TimeSpan.FromMinutes(5).Ticks,
                MinStep = TimeSpan.FromMinutes(5).Ticks,
            },
        ];

        YAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#1E293B")) { StrokeThickness = 1 },
                Position = LiveChartsCore.Measure.AxisPosition.End,
            },
        ];
    }
}
