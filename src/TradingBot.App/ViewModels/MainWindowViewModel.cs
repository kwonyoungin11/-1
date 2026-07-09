using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using TradingBot.App.Services;
using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.App.ViewModels;

/// <summary>
/// 상단 프리미엄 차트(KST) + 하단 자동매매 조작.
/// 종목 SPCX · 최종 전략 추세추종 · 실주문 게이트 잠금.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private const string SafetyHeadlineText =
        "토스증권 실데이터 · SPCX 전용 · 실주문은 게이트 잠금 · 투자 조언 아님 · 시간 KST";

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
        SelectedSymbol = WatchlistCatalog.SpaceXSymbol;
        _harness.SetFocusSymbol(WatchlistCatalog.SpaceXSymbol);
        // Official final preset
        SelectedTimeframe = ChartTimeframeCatalog.UiLabel(SpacexOfficialStrategyPreset.Timeframe);
        _harness.SetTimeframe(SpacexOfficialStrategyPreset.Timeframe);
        SelectedStrategy = SpacexOfficialStrategyPreset.Strategy.ToString();
        _harness.SetStrategy(SpacexOfficialStrategyPreset.Strategy);
        OfficialStrategyLabel = SpacexOfficialStrategyPreset.OwnerSummary;
        RecommendedStrategyNote = SpacexOfficialStrategyPreset.OwnerSummary;

        BuildEmptyChart();
        ApplyPanel(_harness.GetAutoTradePanel());
        ConnectionLabel = _harness.ConnectionLabel;
        ConnectionPill = ShortConnectionPill(_harness.ConnectionModeLabel);
        SafetyHeadline = SafetyHeadlineText;
        ChartSubtitle = "TradingView 라이트 · 버블=거래대금 · ENTRY/SL/TP · 하단 거래량";
        Title = "SPCX 콕핏 · 최종전략 추세추종";
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
    [ObservableProperty] private string _selectedTimeframe = "15m";
    [ObservableProperty] private string _stockKindDescription = string.Empty;
    [ObservableProperty] private string _strategyDescription = string.Empty;
    [ObservableProperty] private string _chartTitle = "SPCX";
    [ObservableProperty] private string _chartSubtitle = string.Empty;
    [ObservableProperty] private string _indicatorLegend = string.Empty;
    [ObservableProperty] private string _lastPriceLabel = string.Empty;
    [ObservableProperty] private string _bracketSummary = string.Empty;
    [ObservableProperty] private string _entryLimitLabel = "—";
    [ObservableProperty] private string _stopLossLabel = "—";
    [ObservableProperty] private string _takeProfitLabel = "—";
    [ObservableProperty] private string _quantityPlanLabel = "—";
    [ObservableProperty] private string _riskAmountLabel = "—";
    [ObservableProperty] private string _rewardRiskLabel = "—";
    [ObservableProperty] private string _atrLabel = "—";
    [ObservableProperty] private string _commissionLabel = "—";
    [ObservableProperty] private string _netRrLabel = "—";
    [ObservableProperty] private string _orderTypeLabel = "LIMIT · 지정가 계획";
    [ObservableProperty] private string _recommendedStrategyNote =
        "SPCX 권장: 추세추종 · 15m/60m · LIMIT+ATR손절 · 1m 스캘핑 비권장(수수료) · 투자 조언 아님";
    [ObservableProperty] private string _officialStrategyLabel = SpacexOfficialStrategyPreset.OwnerSummary;
    [ObservableProperty] private string _connectionLabel = "연결 확인 전";
    [ObservableProperty] private string _connectionPill = "mock";
    [ObservableProperty] private bool _canStart = true;
    [ObservableProperty] private bool _canStop;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _newsDay;
    [ObservableProperty] private string _contingencyLabel = string.Empty;

    [ObservableProperty] private ISeries[] _series = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _volumeSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _volumeXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _volumeYAxes = Array.Empty<Axis>();
    [ObservableProperty] private RectangularSection[] _sections = Array.Empty<RectangularSection>();
    [ObservableProperty] private DrawMarginFrame? _drawMarginFrame;
    [ObservableProperty] private DrawMarginFrame? _volumeDrawMarginFrame;
    [ObservableProperty] private LiveChartsCore.Measure.Margin? _drawMargin;
    [ObservableProperty] private LiveChartsCore.Measure.Margin? _volumeDrawMargin;

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
            ChartTitle = $"SPCX · {ChartTimeframeCatalog.UiLabel(tf)}";
            ChartSubtitle = ChartTimeframeCatalog.Describe(tf) + " · 버블=거래대금 · TV 스타일";
            StatusLine = ChartTimeframeCatalog.NeedsAggregation(tf)
                ? $"시간봉 · {ChartTimeframeCatalog.UiLabel(tf)} · 집계({ChartTimeframeCatalog.SourceTossInterval(tf)}→{ChartTimeframeCatalog.UiLabel(tf)}) · 불러오는 중…"
                : $"시간봉 · {ChartTimeframeCatalog.UiLabel(tf)} · 토스 {ChartTimeframeCatalog.SourceTossInterval(tf)} · 불러오는 중…";
            RebuildChart();
            // 실봉 로드
            _ = RefreshAsync();
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
            ApplyBracket(_harness.GetActiveBracketPlan());
            RebuildChart();
            StatusLine = $"갱신 완료 · {ConnectionPill} · 지정가 계획 · 실주문 잠금";
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
        ChartSubtitle = ChartTimeframeCatalog.TryParse(SelectedTimeframe, out var tfApply)
            ? ChartTimeframeCatalog.Describe(tfApply) + " · 버블=거래대금"
            : ChartSubtitle;
        SafetyHeadline = SafetyHeadlineText;
        if (!SafetyNote.Contains("실주문", StringComparison.Ordinal))
        {
            SafetyNote = string.IsNullOrWhiteSpace(SafetyNote)
                ? "토스 실데이터 · SPCX · 실주문 게이트 잠금."
                : $"{SafetyNote} 실주문 게이트 잠금.";
        }

        _suppressSelectionEcho = false;
    }

    partial void OnNewsDayChanged(bool value)
    {
        _harness.NewsDay = value;
        ApplyBracket(_harness.GetActiveBracketPlan());
        var gate = _harness.EvaluateEntryGate();
        ContingencyLabel = gate.OwnerMessage;
        StatusLine = value
            ? "뉴스데이 ON · 사이즈 50% · 재호가 억제 · 감성 자동매매 없음"
            : "뉴스데이 OFF · 정상 계획 사이즈";
        RebuildChart();
    }

    private void BuildEmptyChart()
    {
        ApplyBracket(_harness.GetActiveBracketPlan());
        ContingencyLabel = _harness.EvaluateEntryGate().OwnerMessage;
        RebuildChart();
    }

    private void ApplyBracket(TradeBracketPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        OrderTypeLabel = $"{plan.OrderType} · 지정가 계획 · 실주문 잠금";
        EntryLimitLabel = plan.EntryLimit > 0 ? plan.EntryLimit.ToString("N2") : "—";
        StopLossLabel = plan.StopPrice > 0 ? plan.StopPrice.ToString("N2") : "—";
        TakeProfitLabel = plan.TakeProfitPrice > 0 ? plan.TakeProfitPrice.ToString("N2") : "—";
        QuantityPlanLabel = plan.Quantity > 0 ? plan.Quantity.ToString("N0") : "0";
        RiskAmountLabel = plan.RiskAmount > 0 ? $"${plan.RiskAmount:N2}" : "—";
        RewardRiskLabel = plan.RewardRiskRatio > 0 ? $"1 : {plan.RewardRiskRatio:N2}" : "—";
        AtrLabel = plan.Atr is decimal a
            ? $"{a:N4} ({plan.StopSource})"
            : plan.StopSource.ToString();
        CommissionLabel = plan.EstimatedCommissionUsd > 0m
            ? $"≈ ${plan.EstimatedCommissionUsd:N2} (왕복 추정)"
            : plan.IsValid ? "≈ $0 ~ 소액" : "—";
        NetRrLabel = plan.NetRewardRiskRatio > 0m
            ? $"1 : {plan.NetRewardRiskRatio:N2}"
            : "—";
        BracketSummary = plan.OwnerMessage;
    }

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
        var bracket = _harness.GetActiveBracketPlan();
        ApplyBracket(bracket);

        var bundle = ChartPresentationBuilder.Build(
            candles,
            markers,
            indicators,
            bracket,
            _harness.Timeframe);

        Series = bundle.PriceSeries;
        VolumeSeries = bundle.VolumeSeries;
        XAxes = bundle.PriceXAxes;
        YAxes = bundle.PriceYAxes;
        VolumeXAxes = bundle.VolumeXAxes;
        VolumeYAxes = bundle.VolumeYAxes;
        Sections = bundle.PriceSections;
        DrawMarginFrame = bundle.PriceFrame;
        VolumeDrawMarginFrame = bundle.VolumeFrame;
        DrawMargin = bundle.PriceMargin;
        VolumeDrawMargin = bundle.VolumeMargin;
        LastPriceLabel = bundle.LastPriceLabel;
        IndicatorLegend = bundle.IndicatorLegend;
        ChartSubtitle = ChartTimeframeCatalog.Describe(_harness.Timeframe) + " · 한국시간(KST) · 프리미엄 차트";
    }
}
