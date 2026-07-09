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
/// VMAR 연습 콕핏 · 실주문 게이트 잠금.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private const string SafetyHeadlineText =
        "축: 토스 실데이터(읽기) · 실주문은 게이트·체크리스트 후 · VMAR · KST · 투자 조언 아님";

    private readonly AppHarness _harness;
    private bool _suppressSelectionEcho;
    private IReadOnlyList<CandlePoint> _chartCandles = Array.Empty<CandlePoint>();
    private string _baseOhlcStatusLine = "—";

    public MainWindowViewModel()
        : this(AppHarness.CreateDefault())
    {
    }

    public MainWindowViewModel(AppHarness harness)
    {
        _harness = harness ?? throw new ArgumentNullException(nameof(harness));
        StockKindOptions = new ObservableCollection<string>(WatchlistCatalog.KindLabels);
        StrategyOptions = new ObservableCollection<string>(StrategyCatalog.Labels);
        SymbolOptions = new ObservableCollection<string>
        {
            WatchlistCatalog.VmarSymbol,
        };
        TimeframeOptions = new ObservableCollection<string>(ChartTimeframeCatalog.Labels);
        TimeframeChips = new ObservableCollection<TimeframeChipVm>(
            ChartTimeframeCatalog.Labels.Select(label => new TimeframeChipVm(label, SelectTimeframeCore)));

        if (_harness.IsLiveSubmissionEnabled)
        {
            SelectedStockKind = StockMarketKind.스페이스X.ToString();
            _harness.SetStockKind(StockMarketKind.스페이스X);
            SelectedSymbol = WatchlistCatalog.SpaceXSymbol;
            _harness.SetFocusSymbol(WatchlistCatalog.SpaceXSymbol);
            SelectedTimeframe = ChartTimeframeCatalog.UiLabel(SpacexOfficialStrategyPreset.Timeframe);
            _harness.SetTimeframe(SpacexOfficialStrategyPreset.Timeframe);
            SelectedStrategy = SpacexOfficialStrategyPreset.Strategy.ToString();
            _harness.SetStrategy(SpacexOfficialStrategyPreset.Strategy);
            OfficialStrategyLabel = SpacexOfficialStrategyPreset.OwnerSummary;
            RecommendedStrategyNote = SpacexOfficialStrategyPreset.OwnerSummary;
        }
        else
        {
            SelectedStockKind = StockMarketKind.비전마린.ToString();
            _harness.SetStockKind(StockMarketKind.비전마린);
            SelectedSymbol = WatchlistCatalog.VmarSymbol;
            _harness.SetFocusSymbol(WatchlistCatalog.VmarSymbol);
            SelectedTimeframe = ChartTimeframeCatalog.UiLabel(VmarOneMinuteScalpPreset.Timeframe);
            _harness.SetTimeframe(VmarOneMinuteScalpPreset.Timeframe);
            SelectedStrategy = VmarOneMinuteScalpPreset.Strategy.ToString();
            _harness.SetStrategy(VmarOneMinuteScalpPreset.Strategy);
            OfficialStrategyLabel = VmarOneMinuteScalpPreset.OwnerSummary;
            RecommendedStrategyNote = VmarOneMinuteScalpPreset.OwnerSummary;
        }

        SyncTimeframeChips();

        BuildEmptyChart();
        ApplyPanel(_harness.GetAutoTradePanel());
        ConnectionLabel = _harness.ConnectionLabel;
        ApplyConnectionAndDataPills();
        ApplySafetyPills();
        ApplyLiveUiLabels();
        SafetyHeadline = ResolveSafetyHeadline();
        ChartSubtitle = "토스 실봉 로딩 중… · 한국시간(KST)";
        _ = BootstrapRealDataAsync();
    }

    private async Task BootstrapRealDataAsync()
    {
        try
        {
            StatusLine = "토스 실시세·실봉·뉴스 불러오는 중…";
            await RefreshAsync().ConfigureAwait(true);
        }
        catch
        {
            StatusLine = "초기 실데이터 로드 실패 · 새로고침 재시도";
        }
    }

    public ObservableCollection<string> StockKindOptions { get; }
    public ObservableCollection<string> StrategyOptions { get; }
    public ObservableCollection<string> SymbolOptions { get; }
    public ObservableCollection<string> TimeframeOptions { get; }
    public ObservableCollection<TimeframeChipVm> TimeframeChips { get; }

    [ObservableProperty] private string _title = "토스 · VMAR 자동매매";
    [ObservableProperty] private string _safetyHeadline = string.Empty;
    [ObservableProperty] private string _statusLine = "대기";
    [ObservableProperty] private string _sessionStatusLabel = "중지";
    [ObservableProperty] private string _watchSymbolsText = WatchlistCatalog.VmarSymbol;
    [ObservableProperty] private string _balanceLabel = string.Empty;
    [ObservableProperty] private string _returnRateLabel = string.Empty;
    [ObservableProperty] private string _safetyNote = string.Empty;
    [ObservableProperty] private string _selectedStockKind = "비전마린";
    [ObservableProperty] private string _selectedStrategy = "일분분할스캘프";
    [ObservableProperty] private string _selectedSymbol = WatchlistCatalog.VmarSymbol;
    [ObservableProperty] private string _selectedTimeframe = "15m";
    [ObservableProperty] private string _stockKindDescription = string.Empty;
    [ObservableProperty] private string _strategyDescription = string.Empty;
    [ObservableProperty] private string _chartTitle = "VMAR";
    [ObservableProperty] private string _chartSubtitle = string.Empty;
    [ObservableProperty] private string _indicatorLegend = string.Empty;
    [ObservableProperty] private string _lastPriceLabel = string.Empty;
    [ObservableProperty] private string _lastCloseText = "—";
    [ObservableProperty] private string _lastPriceAxisBadge = "—";
    [ObservableProperty] private bool _lastPriceAxisBadgeIsUp = true;
    [ObservableProperty] private double _lastPriceYFraction;
    [ObservableProperty] private double _lastPriceYMin;
    [ObservableProperty] private double _lastPriceYMax;
    [ObservableProperty] private double _lastCloseValue;
    [ObservableProperty] private double _lastCloseX;
    [ObservableProperty] private string _changeText = "—";
    [ObservableProperty] private bool _changeIsPositive = true;
    [ObservableProperty] private string _barCountText = string.Empty;
    [ObservableProperty] private string _lastBarTimeText = string.Empty;
    [ObservableProperty] private string _highLowText = string.Empty;
    [ObservableProperty] private string _openText = string.Empty;
    [ObservableProperty] private string _volumeText = string.Empty;
    [ObservableProperty] private string _ohlcStatusLine = string.Empty;
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
        VmarOneMinuteScalpPreset.OwnerSummary;
    [ObservableProperty] private string _officialStrategyLabel = VmarOneMinuteScalpPreset.OwnerSummary;
    [ObservableProperty] private string _connectionLabel = "연결 확인 전";
    [ObservableProperty] private string _connectionPill = "확인중";
    [ObservableProperty] private string _dataSourcePill = "데이터 확인중";
    [ObservableProperty] private string _chartWatermarkLabel = string.Empty;
    [ObservableProperty] private bool _showChartWatermark;
    [ObservableProperty] private string _killSwitchPill = "긴급정지 ON";
    [ObservableProperty] private string _orderModePill = "dry_run";
    [ObservableProperty] private string _liveLockPill = "실주문 잠금";
    [ObservableProperty] private string _gateStatusPill = "게이트 대기";
    [ObservableProperty] private string _botStatePill = "중지";
    [ObservableProperty] private string _startButtonLabel = "연습 시작";
    [ObservableProperty] private string _sessionBasisLabel = "연습 세션 기준 · 수익 보장 아님";
    [ObservableProperty] private bool _canStart = true;
    [ObservableProperty] private bool _canStop;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _newsDay;
    [ObservableProperty] private string _contingencyLabel = string.Empty;
    [ObservableProperty] private string _newsStatus = "뉴스 대기";
    public ObservableCollection<NewsItemRow> NewsItems { get; } = new();

    [ObservableProperty] private ISeries[] _series = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _volumeSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _rsiSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _volumeXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _volumeYAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _rsiXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _rsiYAxes = Array.Empty<Axis>();
    [ObservableProperty] private RectangularSection[] _sections = Array.Empty<RectangularSection>();
    [ObservableProperty] private RectangularSection[] _rsiSections = Array.Empty<RectangularSection>();
    [ObservableProperty] private DrawMarginFrame? _drawMarginFrame;
    [ObservableProperty] private DrawMarginFrame? _volumeDrawMarginFrame;
    [ObservableProperty] private DrawMarginFrame? _rsiDrawMarginFrame;
    [ObservableProperty] private LiveChartsCore.Measure.Margin? _drawMargin;
    [ObservableProperty] private LiveChartsCore.Measure.Margin? _volumeDrawMargin;
    [ObservableProperty] private LiveChartsCore.Measure.Margin? _rsiDrawMargin;
    [ObservableProperty] private string _rsiStatusText = string.Empty;

    partial void OnSelectedStockKindChanged(string value)
    {
        if (_suppressSelectionEcho)
        {
            return;
        }

        if (!Enum.TryParse<StockMarketKind>(value, out var kind))
        {
            return;
        }

        _harness.SetStockKind(kind);
        RefreshSymbolOptions();

        _suppressSelectionEcho = true;
        SelectedStockKind = StockMarketKind.비전마린.ToString();
        SelectedStrategy = VmarOneMinuteScalpPreset.Strategy.ToString();
        SelectedTimeframe = ChartTimeframeCatalog.UiLabel(VmarOneMinuteScalpPreset.Timeframe);
        OfficialStrategyLabel = VmarOneMinuteScalpPreset.OwnerSummary;
        RecommendedStrategyNote = VmarOneMinuteScalpPreset.OwnerSummary;
        _suppressSelectionEcho = false;
        SyncTimeframeChips();

        ApplyPanel(_harness.GetAutoTradePanel());
        RebuildChart();
        StatusLine = $"대상 · {WatchlistCatalog.Describe(StockMarketKind.비전마린)} · 토스 실데이터";
        _ = RefreshAsync();
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

        if (!WatchlistCatalog.IsKnownSymbol(value))
        {
            return;
        }

        var focus = WatchlistCatalog.NormalizeKnownSymbol(value) ?? WatchlistCatalog.VmarSymbol;
        _harness.SetFocusSymbol(focus);
        ChartTitle = $"{focus} · {SelectedTimeframe}";
        RebuildChart();
        _ = RefreshAsync();
    }

    partial void OnSelectedTimeframeChanged(string value)
    {
        SyncTimeframeChips();

        if (_suppressSelectionEcho)
        {
            return;
        }

        if (ChartTimeframeCatalog.TryParse(value, out var tf))
        {
            _harness.SetTimeframe(tf);
            var focus = _harness.Session.ResolveFocusSymbol();
            ChartTitle = $"{focus} · {ChartTimeframeCatalog.UiLabel(tf)}";
            ChartSubtitle = ChartTimeframeCatalog.Describe(tf) + " · 버블=거래대금 · TV 스타일";
            StatusLine = ChartTimeframeCatalog.NeedsAggregation(tf)
                ? $"시간봉 · {ChartTimeframeCatalog.UiLabel(tf)} · 집계({ChartTimeframeCatalog.SourceTossInterval(tf)}→{ChartTimeframeCatalog.UiLabel(tf)}) · 불러오는 중…"
                : $"시간봉 · {ChartTimeframeCatalog.UiLabel(tf)} · 토스 {ChartTimeframeCatalog.SourceTossInterval(tf)} · 불러오는 중…";
            RebuildChart();
            _ = RefreshAsync();
        }
    }

    public bool IsTimeframeSelected(string? tf) =>
        !string.IsNullOrWhiteSpace(tf)
        && string.Equals(SelectedTimeframe, tf, StringComparison.Ordinal);

    [RelayCommand]
    private void SelectTimeframe(string? label) => SelectTimeframeCore(label);

    private void SelectTimeframeCore(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        if (!ChartTimeframeCatalog.TryParse(label, out _))
        {
            return;
        }

        var ui = ChartTimeframeCatalog.Labels.FirstOrDefault(l =>
                     string.Equals(l, label, StringComparison.OrdinalIgnoreCase))
                 ?? label;

        if (string.Equals(SelectedTimeframe, ui, StringComparison.Ordinal))
        {
            return;
        }

        SelectedTimeframe = ui;
    }

    private void SyncTimeframeChips()
    {
        foreach (var chip in TimeframeChips)
        {
            chip.IsSelected = string.Equals(chip.Label, SelectedTimeframe, StringComparison.Ordinal);
        }
    }

    private void RefreshSymbolOptions()
    {
        var focus = _harness.Session.ResolveFocusSymbol();
        var watch = _harness.Session.ResolveWatchSymbols();
        SymbolOptions.Clear();
        foreach (var s in watch)
        {
            if (WatchlistCatalog.IsKnownSymbol(s) && !SymbolOptions.Contains(s))
            {
                SymbolOptions.Add(WatchlistCatalog.NormalizeKnownSymbol(s) ?? s);
            }
        }

        if (SymbolOptions.Count == 0)
        {
            SymbolOptions.Add(focus);
        }

        _suppressSelectionEcho = true;
        SelectedSymbol = SymbolOptions.Contains(focus) ? focus : SymbolOptions[0];
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
            StatusLine = "토스 실데이터 불러오는 중…";
            _ = await _harness.GetDashboardAsync().ConfigureAwait(true);
            RefreshSymbolOptions();
            ApplyPanel(_harness.GetAutoTradePanel());
            ConnectionLabel = CompactConnection(_harness.ConnectionLabel);
            ApplyConnectionAndDataPills();
            ApplyBracket(_harness.GetActiveBracketPlan());
            ApplySafetyPills();
            ApplyLiveUiLabels();
            ApplyNews();
            RebuildChart();
            StatusLine = _harness.IsLiveSubmissionEnabled
                ? $"갱신 · {DataSourcePill} · {OrderModePill} · 실거래"
                : $"갱신 · {DataSourcePill} · {OrderModePill} · 연습";
        }
        catch (Exception ex)
        {
            StatusLine = "오류 · 실데이터 실패 · 실주문 없음 · fail-closed";
            ConnectionLabel = $"연결 오류 · {ex.GetType().Name}";
            ConnectionPill = "오류";
            DataSourcePill = "데이터 오류";
            ApplySafetyPills();
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
        BotStatePill = MapBotStatePill(p.SessionStatusLabel, _harness.IsLiveSubmissionEnabled);
        var focus = string.IsNullOrWhiteSpace(p.FocusSymbol)
            ? WatchlistCatalog.VmarSymbol
            : p.FocusSymbol;
        WatchSymbolsText = string.IsNullOrWhiteSpace(p.WatchSymbolsText)
            ? focus
            : p.WatchSymbolsText;
        BalanceLabel = CompactBalance(p.BalanceLabel);
        ReturnRateLabel = CompactReturn(p.ReturnRateLabel, p.ReturnRatePercent);
        SafetyNote = p.SafetyNote;
        CanStart = p.CanStart;
        CanStop = p.CanStop;
        SelectedStockKind = p.StockKindLabel;
        SelectedStrategy = p.StrategyLabel;
        StockKindDescription = p.StockKindDescription;
        StrategyDescription = p.StrategyDescription;
        SelectedSymbol = focus;
        ChartTitle = $"{focus} · {SelectedTimeframe}";
        ChartSubtitle = ChartTimeframeCatalog.TryParse(SelectedTimeframe, out var tfApply)
            ? ChartTimeframeCatalog.Describe(tfApply) + " · 버블=거래대금"
            : ChartSubtitle;
        SafetyHeadline = ResolveSafetyHeadline();
        if (!SafetyNote.Contains("실주문", StringComparison.Ordinal))
        {
            SafetyNote = string.IsNullOrWhiteSpace(SafetyNote)
                ? ResolveDefaultSafetyNote(focus)
                : $"{SafetyNote} {ResolveSafetyNoteSuffix()}";
        }

        ApplySafetyPills();
        ApplyLiveUiLabels();
        _suppressSelectionEcho = false;
    }

    private void ApplyLiveUiLabels()
    {
        StartButtonLabel = _harness.IsLiveSubmissionEnabled ? "실거래 시작" : "연습 시작";
        SessionBasisLabel = _harness.IsLiveSubmissionEnabled
            ? "실계좌 기준 · 실주문 가능 · 수익 보장 아님"
            : "연습 세션 기준 · 수익 보장 아님";
        Title = _harness.IsLiveSubmissionEnabled
            ? "토스 · SPCX 실거래"
            : "토스 · VMAR 자동매매";
    }

    private void ApplyConnectionAndDataPills()
    {
        ConnectionPill = ShortConnectionPill(
            _harness.ConnectionModeLabel,
            _harness.ConnectionLabel,
            _harness.IsLiveReadOnlyConnected);
        DataSourcePill = _harness.ChartUsesRealCandles
            ? "토스 실봉"
            : _harness.IsLiveReadOnlyConnected
                ? "실연결 · 봉 폴백"
                : ShortDataSourcePill(_harness.ChartDataSourceLabel);
    }

    private void ApplySafetyPills()
    {
        var report = _harness.GetLiveReadinessReport();
        KillSwitchPill = report.SettingsKillSwitch ? "긴급정지 ON" : "긴급정지 OFF";
        OrderModePill = string.IsNullOrWhiteSpace(report.SettingsOrderMode)
            ? "dry_run"
            : report.SettingsOrderMode.ToLowerInvariant() switch
            {
                "dryrun" or "dry_run" => "dry_run",
                "paper" => "paper",
                "live" => _harness.IsLiveSubmissionEnabled ? "live" : "live(차단)",
                _ => report.SettingsOrderMode,
            };
        LiveLockPill = _harness.IsLiveSubmissionEnabled
            ? "실거래 ON · 자동주문 가능"
            : "실주문 잠금";

        if (report.SettingsKillSwitch)
        {
            GateStatusPill = "진입 차단 · 킬스위치 ON";
        }
        else if (_harness.IsLiveSubmissionEnabled)
        {
            GateStatusPill = "실거래 활성 · 토스 주문 전송 가능";
        }
        else if (report.SettingsAllowLiveOrders || string.Equals(report.SettingsOrderMode, "Live", StringComparison.OrdinalIgnoreCase))
        {
            GateStatusPill = "live 설정 감지 · 연결/데이터 확인 필요";
        }
        else
        {
            var gate = _harness.EvaluateEntryGate();
            GateStatusPill = CompactGate(gate.OwnerMessage);
        }

        ContingencyLabel = GateStatusPill;
        ApplyLiveUiLabels();
    }

    private string ResolveSafetyHeadline() =>
        _harness.IsLiveSubmissionEnabled
            ? "실거래 모드 — 자동매매 시 토스에 실주문 전송 · 투자 조언 아님"
            : SafetyHeadlineText;

    private string ResolveDefaultSafetyNote(string focus) =>
        _harness.IsLiveSubmissionEnabled
            ? $"토스 실데이터 · {focus} · 실거래 모드 · 자동주문 활성."
            : $"토스 실데이터 · {focus} · 실주문 게이트 잠금 · dry-run only.";

    private string ResolveSafetyNoteSuffix() =>
        _harness.IsLiveSubmissionEnabled ? "실거래 모드." : "실주문 게이트 잠금.";

    private static string ShortDataSourcePill(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "데이터 확인중";
        }

        if (label.Contains("실봉", StringComparison.Ordinal))
        {
            return "토스 실봉";
        }

        if (label.Contains("mock", StringComparison.OrdinalIgnoreCase))
        {
            return "mock 폴백";
        }

        return TruncateFit(label, 16);
    }

    private static string MapBotStatePill(string session, bool liveTrading)
    {
        if (string.IsNullOrWhiteSpace(session))
        {
            return "대기";
        }

        if (session.Contains("실행", StringComparison.Ordinal))
        {
            return liveTrading ? "실행중(실거래)" : "실행중(연습)";
        }

        if (session.Contains("중지", StringComparison.Ordinal))
        {
            return "중지";
        }

        return TruncateFit(session, 12);
    }

    partial void OnNewsDayChanged(bool value)
    {
        _harness.NewsDay = value;
        ApplyBracket(_harness.GetActiveBracketPlan());
        var gate = _harness.EvaluateEntryGate();
        ContingencyLabel = CompactGate(gate.OwnerMessage);
        StatusLine = value ? "뉴스데이 ON · 사이즈 50%" : "뉴스데이 OFF";
        RebuildChart();
    }

    private void BuildEmptyChart()
    {
        ApplyBracket(_harness.GetActiveBracketPlan());
        ConnectionLabel = CompactConnection(_harness.ConnectionLabel);
        ApplyConnectionAndDataPills();
        ApplySafetyPills();
        _ = LoadNewsOnStartAsync();
        RebuildChart();
    }

    private static string TruncateFit(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
        {
            return text;
        }

        return text[..(max - 1)] + "…";
    }

    private static string CompactConnection(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "연결 확인 전";
        }

        // Prefer short owner-facing status (drop long HTTP detail).
        if (raw.Contains("실", StringComparison.Ordinal) && raw.Contains("HTTP", StringComparison.OrdinalIgnoreCase))
        {
            return "토스 실연결";
        }

        if (raw.Contains("오류", StringComparison.Ordinal) || raw.Contains("실패", StringComparison.Ordinal))
        {
            return TruncateFit(raw, 28);
        }

        return TruncateFit(raw.Replace(" — ", " · ", StringComparison.Ordinal), 36);
    }

    private static string CompactGate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "게이트 대기";
        }

        if (raw.Contains("뉴스데이", StringComparison.Ordinal))
        {
            return "뉴스데이 · 사이즈 50% · 감성매매 금지";
        }

        if (raw.Contains("게이트 통과", StringComparison.Ordinal))
        {
            return "게이트 통과 · 실주문 아님";
        }

        if (raw.Contains("킬스위치", StringComparison.Ordinal) || raw.Contains("일손실", StringComparison.Ordinal))
        {
            return "진입 차단 · 킬스위치/일손실";
        }

        if (raw.Contains("추세", StringComparison.Ordinal) || raw.Contains("필터", StringComparison.Ordinal))
        {
            return "진입 차단 · 추세/필터 미충족";
        }

        if (raw.Contains("세션", StringComparison.Ordinal))
        {
            return "진입 차단 · 세션";
        }

        if (raw.Contains("데이터", StringComparison.Ordinal))
        {
            return "진입 차단 · 데이터";
        }

        // Domain messages are already short; keep whole string when reasonable
        return raw.Length <= 42 ? raw : TruncateFit(raw, 42);
    }

    private static string CompactBalance(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "—";
        }

        // "잔액 1.54 (토스 …)" → keep readable head
        return TruncateFit(raw.Replace("잔액 ", "", StringComparison.Ordinal), 28);
    }

    private static string CompactReturn(string raw, decimal returnRatePercent = 0m)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Contains("—", StringComparison.Ordinal))
        {
            return returnRatePercent == 0m ? "0.00% · 세션" : $"{returnRatePercent:N2}%";
        }

        var t = TruncateFit(raw.Replace("수익률 ", "", StringComparison.Ordinal), 16);
        return string.IsNullOrWhiteSpace(t) || t == "—" ? "0.00% · 세션" : t;
    }

    private async Task LoadNewsOnStartAsync()
    {
        try
        {
            await _harness.RefreshNewsAsync().ConfigureAwait(true);
            ApplyNews();
        }
        catch
        {
            NewsStatus = "뉴스 초기 로드 실패";
        }
    }

    private void ApplyNews()
    {
        NewsStatus = CompactNewsStatus(_harness.NewsStatus);
        NewsItems.Clear();
        foreach (var n in _harness.LastNews.Take(2))
        {
            NewsItems.Add(new NewsItemRow(
                n.Title.Trim(),
                $"{n.Source} · {n.PublishedKstLabel}",
                n.IsMaterialEvent ? "중요" : "",
                n.Url));
        }
    }

    private static string CompactNewsStatus(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "뉴스 대기";
        }

        // "Google News RSS · 2건 · …" keep head only
        var parts = raw.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return TruncateFit($"{parts[0]} · {parts[1]}", 36);
        }

        return TruncateFit(raw, 36);
    }

    [RelayCommand]
    private async Task RefreshNewsOnlyAsync()
    {
        await _harness.RefreshNewsAsync().ConfigureAwait(true);
        ApplyNews();
        StatusLine = "뉴스 갱신 · " + NewsStatus;
    }

    private void ApplyBracket(TradeBracketPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        OrderTypeLabel = _harness.IsLiveSubmissionEnabled
            ? $"{plan.OrderType} · 실거래"
            : $"{plan.OrderType} · 잠금";
        EntryLimitLabel = plan.EntryLimit > 0 ? plan.EntryLimit.ToString("N2") : "—";
        StopLossLabel = plan.StopPrice > 0 ? plan.StopPrice.ToString("N2") : "—";
        TakeProfitLabel = plan.TakeProfitPrice > 0 ? plan.TakeProfitPrice.ToString("N2") : "—";
        QuantityPlanLabel = plan.Quantity > 0 ? plan.Quantity.ToString("N0") : "0";
        RiskAmountLabel = plan.RiskAmount > 0 ? $"${plan.RiskAmount:N2}" : "—";
        RewardRiskLabel = plan.RewardRiskRatio > 0 ? $"1:{plan.RewardRiskRatio:N2}" : "—";
        AtrLabel = plan.Atr is decimal a
            ? $"{a:N4} · {plan.StopSource}"
            : plan.StopSource.ToString();
        CommissionLabel = plan.EstimatedCommissionUsd > 0m
            ? $"≈${plan.EstimatedCommissionUsd:N2}"
            : plan.IsValid ? "≈$0" : "—";
        NetRrLabel = plan.NetRewardRiskRatio > 0m
            ? $"1:{plan.NetRewardRiskRatio:N2}"
            : "—";
        BracketSummary = plan.IsValid
            ? $"R:R 1:{plan.RewardRiskRatio:N2} · 수수료≈${plan.EstimatedCommissionUsd:N2}"
            : plan.OwnerMessage;
    }

    private static string ShortConnectionPill(
        string modeLabel,
        string? ownerLabel = null,
        bool isLiveReadOnly = false)
    {
        if (isLiveReadOnly)
        {
            return "토스 실연결";
        }

        if (!string.IsNullOrWhiteSpace(ownerLabel)
            && (ownerLabel.Contains("오류", StringComparison.Ordinal)
                || ownerLabel.Contains("실패", StringComparison.Ordinal)))
        {
            return "오류";
        }

        if (!string.IsNullOrWhiteSpace(ownerLabel)
            && (ownerLabel.Contains("실 HTTP", StringComparison.Ordinal)
                || ownerLabel.Contains("실연결", StringComparison.Ordinal)
                || ownerLabel.Contains("토스 실", StringComparison.Ordinal)))
        {
            return "토스 실연결";
        }

        if (modeLabel.Contains("실 HTTP", StringComparison.Ordinal)
            || modeLabel.Contains("읽기 전용", StringComparison.Ordinal))
        {
            return "토스 실연결";
        }

        if (modeLabel.Contains("오류", StringComparison.Ordinal))
        {
            return "오류";
        }

        if (modeLabel.Contains("mock", StringComparison.OrdinalIgnoreCase)
            || modeLabel.Contains("모의", StringComparison.Ordinal))
        {
            return "mock";
        }

        return string.IsNullOrWhiteSpace(modeLabel) ? "확인중" : TruncateFit(modeLabel, 14);
    }


    private void RebuildChart()
    {
        var (candles, markers, indicators) = _harness.GetChartData();
        var bracket = _harness.GetActiveBracketPlan();
        ApplyBracket(bracket);

        _chartCandles = candles;
        var watermark = _harness.ChartWatermark;
        var bundle = ChartPresentationBuilder.Build(
            candles,
            markers,
            indicators,
            bracket,
            _harness.Timeframe,
            dataSourceWatermark: watermark);

        Series = bundle.PriceSeries;
        VolumeSeries = bundle.VolumeSeries;
        RsiSeries = bundle.RsiSeries ?? Array.Empty<ISeries>();
        XAxes = bundle.PriceXAxes;
        YAxes = bundle.PriceYAxes;
        VolumeXAxes = bundle.VolumeXAxes;
        VolumeYAxes = bundle.VolumeYAxes;
        RsiXAxes = bundle.RsiXAxes ?? Array.Empty<Axis>();
        RsiYAxes = bundle.RsiYAxes ?? Array.Empty<Axis>();
        Sections = bundle.PriceSections;
        RsiSections = bundle.RsiSections ?? Array.Empty<RectangularSection>();
        DrawMarginFrame = bundle.PriceFrame;
        VolumeDrawMarginFrame = bundle.VolumeFrame;
        RsiDrawMarginFrame = bundle.RsiFrame;
        DrawMargin = bundle.PriceMargin;
        VolumeDrawMargin = bundle.VolumeMargin;
        RsiDrawMargin = bundle.RsiMargin;
        RsiStatusText = bundle.RsiStatusText ?? string.Empty;
        LastPriceLabel = bundle.LastPriceLabel;
        LastCloseText = string.IsNullOrEmpty(bundle.LastPriceTag)
            ? (string.IsNullOrEmpty(bundle.LastCloseText) ? "—" : bundle.LastCloseText)
            : bundle.LastPriceTag;
        LastPriceAxisBadge = string.IsNullOrEmpty(bundle.LastPriceAxisBadge)
            ? LastCloseText
            : bundle.LastPriceAxisBadge;
        LastPriceAxisBadgeIsUp = bundle.LastPriceAxisBadgeIsUp;
        LastPriceYFraction = bundle.LastPriceYFraction;
        LastPriceYMin = bundle.LastPriceYMin;
        LastPriceYMax = bundle.LastPriceYMax;
        LastCloseValue = bundle.LastCloseValue;
        LastCloseX = candles.Count > 0
            ? KoreaTime.ToKstDateTime(candles[^1].Time).Ticks
            : 0;
        ChangeText = string.IsNullOrEmpty(bundle.ChangeText) ? "—" : bundle.ChangeText;
        ChangeIsPositive = bundle.ChangeIsPositive;
        BarCountText = bundle.BarCountText;
        LastBarTimeText = bundle.LastBarTimeText;
        HighLowText = bundle.HighLowText;
        OpenText = bundle.OpenText;
        VolumeText = bundle.VolumeText;
        _baseOhlcStatusLine = string.IsNullOrEmpty(bundle.StatusLineText) ? "—" : bundle.StatusLineText;
        OhlcStatusLine = _baseOhlcStatusLine;
        IndicatorLegend = string.IsNullOrEmpty(bundle.IndicatorLegend)
            ? watermark
            : bundle.IndicatorLegend;
        ChartWatermarkLabel = watermark;
        ShowChartWatermark = !string.IsNullOrWhiteSpace(watermark)
                             && !string.Equals(watermark, "토스 실봉", StringComparison.Ordinal);
        var focus = _harness.Session.ResolveFocusSymbol();
        ChartTitle = $"{focus} · {SelectedTimeframe}";
        ChartSubtitle = $"{_harness.ChartDataSourceLabel} · KST · 줌 연동";
        DataSourcePill = _harness.ChartUsesRealCandles
            ? "토스 실봉"
            : ShortDataSourcePill(_harness.ChartDataSourceLabel);
    }

    public void UpdateHoverFromChartX(double xCoordinate)
    {
        if (_chartCandles.Count == 0 || xCoordinate <= 0 || double.IsNaN(xCoordinate))
        {
            return;
        }

        DateTimeOffset hoverTime;
        try
        {
            var ticks = (long)xCoordinate;
            if (ticks < TimeSpan.FromDays(365).Ticks)
            {
                return;
            }

            var kstWall = new DateTime(ticks, DateTimeKind.Unspecified);
            hoverTime = new DateTimeOffset(kstWall, KoreaTime.TimeZone.BaseUtcOffset);
        }
        catch
        {
            return;
        }

        var hover = ChartPresentationBuilder.ResolveHoverOhlcStatus(_chartCandles, hoverTime);
        if (string.IsNullOrEmpty(hover))
        {
            return;
        }

        OhlcStatusLine = hover;
    }

    public void ClearHoverOhlc()
    {
        OhlcStatusLine = string.IsNullOrEmpty(_baseOhlcStatusLine) ? "—" : _baseOhlcStatusLine;
    }
}

/// <summary>One row in the news card.</summary>
public sealed record NewsItemRow(string Title, string Meta, string Badge, string? Url);

public partial class TimeframeChipVm : ObservableObject
{
    private readonly Action<string> _onSelect;

    public TimeframeChipVm(string label, Action<string> onSelect)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        _onSelect = onSelect ?? throw new ArgumentNullException(nameof(onSelect));
    }

    public string Label { get; }

    [ObservableProperty] private bool _isSelected;

    [RelayCommand]
    private void Select() => _onSelect(Label);
}
