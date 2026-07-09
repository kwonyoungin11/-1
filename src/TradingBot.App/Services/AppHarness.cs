using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Observability;
using TradingBot.Orders;
using TradingBot.Risk;
using TradingBot.Ui;

namespace TradingBot.App.Services;

/// <summary>
/// Owner-facing practice evidence from dry-run + paper ledgers.
/// Live is always blocked in the desktop app host — no secrets, no account numbers.
/// </summary>
public sealed record AppTradingEvidenceSummary(
    int DryRunCount,
    int PaperCount,
    bool LiveBlocked,
    string? ExportText = null,
    TradingEvidenceSnapshot? Snapshot = null)
{
    /// <summary>
    /// Always false in AppHarness — dry-run and paper routers never enable live submission.
    /// </summary>
    public bool IsLiveSubmissionEnabled => false;
}

/// <summary>
/// Mac 앱 조합 루트. 차트·자동매매 연습 세션. 실주문 HTTP 없음.
/// 버블 크기 = 체결 규모(수량×가격).
/// </summary>
public sealed class AppHarness
{
    private readonly TradingSafetySettings _settings;
    private readonly IReadOnlyPortfolioService _portfolio;
    private readonly OrderCandidatePipeline _pipeline;
    private readonly LiveOrderGate _liveOrderGate;
    private readonly IDryRunLedger _dryRunLedger;
    private readonly DryRunOrderRouter _dryRun;
    private readonly IPaperLedger _paperLedger;
    private readonly PaperOrderRouter _paper;
    private readonly IOrderRouter? _gatedLiveRouter;
    private readonly IAuditLog _audit;
    private readonly AutoTradeSessionService _session;
    private readonly TossOptions _tossOptions;
    private readonly EvidenceBuilder _evidenceBuilder;
    private string _connectionLabel = "연결 확인 전";
    private string _connectionModeLabel = "mock";

    public AppHarness(
        TradingSafetySettings settings,
        IReadOnlyPortfolioService portfolio,
        OrderCandidatePipeline pipeline,
        LiveOrderGate liveOrderGate,
        IDryRunLedger dryRunLedger,
        DryRunOrderRouter dryRun,
        IPaperLedger paperLedger,
        PaperOrderRouter paper,
        IAuditLog audit,
        AutoTradeSessionService session,
        TossOptions? tossOptions = null,
        IOrderRouter? gatedLiveRouter = null)
    {
        _settings = settings;
        _portfolio = portfolio;
        _pipeline = pipeline;
        _liveOrderGate = liveOrderGate;
        _dryRunLedger = dryRunLedger;
        _dryRun = dryRun;
        _paperLedger = paperLedger;
        _paper = paper;
        _audit = audit;
        _session = session;
        _tossOptions = tossOptions ?? new TossOptions { AllowLiveHttp = false };
        _connectionModeLabel = TossReadOnlyFactory.DescribeMode(_tossOptions);
        _evidenceBuilder = new EvidenceBuilder(_dryRunLedger, _paperLedger);
        // Optional gated live router (BlockedLiveOrderRouter today; GatedLiveOrderRouter when merged).
        // Registered for readiness / future wiring only — practice loop never routes here under defaults.
        _gatedLiveRouter = gatedLiveRouter;
    }

    public AutoTradeSessionService Session => _session;

    public string ConnectionLabel => _connectionLabel;

    public string ConnectionModeLabel => _connectionModeLabel;

    /// <summary>
    /// True when a gated live router instance is held (still not used by CreateDefault practice loop).
    /// </summary>
    public bool IsGatedLiveRouterRegistered => _gatedLiveRouter is not null;

    /// <summary>
    /// Practice routers never enable live Toss order HTTP. Gated live router (if registered)
    /// also reports false until a future approved live implementation.
    /// </summary>
    public bool IsLiveSubmissionEnabled =>
        _dryRun.IsLiveSubmissionEnabled
        || _paper.IsLiveSubmissionEnabled
        || (_gatedLiveRouter?.IsLiveSubmissionEnabled ?? false);

    /// <summary>
    /// Settings that would theoretically select a live path. CreateDefault is always fail-closed
    /// (all three false / non-live), so practice never uses the gated live router.
    /// </summary>
    public bool SettingsWouldAllowLiveRouting =>
        _settings.AllowLiveOrders
        && !_settings.KillSwitch
        && _settings.OrderMode == OrderMode.Live;

    public static AppHarness CreateDefault()
    {
        var settings = new TradingSafetySettings
        {
            AllowLiveOrders = false,
            KillSwitch = true,
            OrderMode = OrderMode.DryRun,
            MaxOrderNotional = 50_000m,
            MarketDataMaxStalenessSeconds = TradingSafetyDefaults.MarketDataMaxStalenessSeconds,
        };
        var toss = TossReadOnlyFactory.LoadOptionsFromEnvironment();
        var portfolio = TossReadOnlyFactory.CreatePortfolioService(toss);
        var audit = new InMemoryAuditLog();
        // Shared index: same ClientOrderId cannot pass dry-run then paper twice
        var clientOrderIds = new ClientOrderIdIndex();
        var dryLedger = new InMemoryDryRunLedger();
        var dryRun = new DryRunOrderRouter(dryLedger, clientOrderIds);
        var paperLedger = new InMemoryPaperLedger();
        // Paper uses its own index for paper-only dups; dry-run already registered ids
        // stay unique per pipeline step via unique factory. Keep separate paper index.
        var paper = new PaperOrderRouter(paperLedger);
        // Gated live stub: registered for readiness evidence, never used by practice loop
        // under CreateDefault fail-closed settings. Prefer GatedLiveOrderRouter when present
        // in Orders assembly; fall back to BlockedLiveOrderRouter (always IsLiveSubmissionEnabled=false).
        var gatedLive = CreateGatedLiveRouterOrBlocked(settings);
        return new AppHarness(
            settings,
            portfolio,
            new OrderCandidatePipeline(),
            new LiveOrderGate(),
            dryLedger,
            dryRun,
            paperLedger,
            paper,
            audit,
            new AutoTradeSessionService(),
            toss,
            gatedLiveRouter: gatedLive);
    }

    /// <summary>
    /// Prefer <c>GatedLiveOrderRouter</c> if another wave merges it into TradingBot.Orders;
    /// otherwise use <see cref="BlockedLiveOrderRouter"/> (fail-closed stub).
    /// </summary>
    private static IOrderRouter CreateGatedLiveRouterOrBlocked(TradingSafetySettings settings)
    {
        // Type may land from pw07-live-router merge; reflection keeps this worktree independent.
        var ordersAsm = typeof(BlockedLiveOrderRouter).Assembly;
        var gatedType = ordersAsm.GetType("TradingBot.Orders.GatedLiveOrderRouter");
        if (gatedType is not null)
        {
            try
            {
                var instance = Activator.CreateInstance(gatedType, settings);
                if (instance is IOrderRouter router)
                {
                    return router;
                }
            }
            catch
            {
                // Fall through to blocked stub — never throw open a live path on construction failure.
            }
        }

        return new BlockedLiveOrderRouter(settings);
    }

    public AutoTradePanelSnapshot GetAutoTradePanel() => _session.ToPanelSnapshot();

    public string StartAutoTrade()
    {
        _ = _session.TryStart(out var msg);
        _audit.Append(new AuditEntry(DateTimeOffset.UtcNow, "session", msg, "auto_start"));
        return msg;
    }

    public string StopAutoTrade()
    {
        _ = _session.TryStop(out var msg);
        _audit.Append(new AuditEntry(DateTimeOffset.UtcNow, "session", msg, "auto_stop"));
        return msg;
    }

    public void SetStockKind(StockMarketKind kind) => _session.StockKind = kind;

    public void SetStrategy(TradingStrategyKind strategy) => _session.Strategy = strategy;

    public void SetFocusSymbol(string symbol) => _session.FocusSymbol = symbol;

    public (IReadOnlyList<CandlePoint> Candles, IReadOnlyList<TradeMarker> Markers) GetChartData()
    {
        var symbol = _session.ResolveFocusSymbol();
        var seed = WatchlistCatalog.ChartSeedPrice(symbol);
        var candles = MockCandleSeriesFactory.CreateSeries(symbol, 160, DateTimeOffset.UtcNow, seed);
        var paperAll = MockCandleSeriesFactory.MarkersFromPaperFills(_paperLedger.GetSnapshot());
        // 포커스 종목 paper 체결만 강조 + 데모 버블(거래대금 규모)
        var paperFocus = paperAll
            .Where(m => true) // paper fill에 symbol 없음 → 전체 반영; 규모는 Notional
            .ToList();
        var demo = MockCandleSeriesFactory.CreateDemoMarkers(candles);
        if (paperFocus.Count == 0)
        {
            return (candles, demo);
        }

        var merged = new List<TradeMarker>(demo.Count + paperFocus.Count);
        merged.AddRange(demo);
        // paper 규모를 차트 Weight 스케일로 정규화 (로그 스케일 근사)
        var maxPaper = paperFocus.Max(m => m.SizeWeight);
        foreach (var p in paperFocus)
        {
            var w = maxPaper <= 0 ? 1 : 1.2 + 4.0 * (p.SizeWeight / maxPaper);
            merged.Add(p with { SizeWeight = w, Label = $"{p.Label}(연습체결)" });
        }

        return (candles, merged);
    }

    public async Task<CockpitDashboardModel> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        _audit.Append(new AuditEntry(
            DateTimeOffset.UtcNow,
            "system",
            "데스크톱 콕핏 갱신 — 실거래 차단 · 연습만",
            "app_boot"));

        var liveDecision = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());
        var symbols = _session.ResolveWatchSymbols();
        var portfolio = await _portfolio
            .GetSnapshotAsync(symbols, cancellationToken)
            .ConfigureAwait(false);
        _connectionLabel = portfolio.ConnectionOwnerMessage;
        _connectionModeLabel = TossReadOnlyFactory.DescribeMode(_tossOptions);
        var snapshot = CockpitReadOnlyProjector.Project(portfolio, _settings);

        var now = DateTimeOffset.UtcNow;
        var usSession = UsMarketSessionGuard.Evaluate(portfolio.UsMarket, now);
        var strategy = _session.Strategy;
        var qty = StrategyCatalog.BaseQuantity(strategy);
        IReadOnlyList<EvaluatedOrderCandidate> evaluated = Array.Empty<EvaluatedOrderCandidate>();

        if (_session.Status == AutoTradeSessionStatus.실행중)
        {
            evaluated = _pipeline.BuildCandidates(
                portfolio.Quotes,
                _settings,
                defaultOrderQuantity: qty,
                nowUtc: now,
                marketSessionOpen: usSession.IsOpenForOrders,
                marketSessionKnown: usSession.IsKnown,
                usMarket: portfolio.UsMarket,
                strategy: strategy);

            foreach (var item in evaluated.Where(e => e.IsAcceptedForDryRun))
            {
                // CreateDefault practice loop: dry-run + paper only.
                // Gated live router is intentionally NOT used here even if registered.
                // Live path remains fail-closed unless settings would allow AND a future
                // approved implementation is wired (SettingsWouldAllowLiveRouting is false
                // under CreateDefault).
                _ = await _dryRun.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
                var paperResult = await _paper.RouteAsync(item.Candidate, cancellationToken).ConfigureAwait(false);
                if (paperResult.Accepted && item.Candidate.LimitPrice is decimal px)
                {
                    _session.ApplyVirtualFill(item.Candidate.Side, item.Candidate.Quantity, px);
                    // 규모(수량×가격)에 비례한 소액 평가손익 연습
                    _session.ApplyScaffoldMarkToMarket(item.Candidate.Quantity * px * 0.0005m);
                }
            }
        }

        return CockpitDashboardMapper.Compose(
            snapshot,
            _settings,
            candidates: evaluated,
            extraRiskDecision: liveDecision);
    }

    public (int DryRun, int Paper, bool LiveBlocked) GetEvidenceCounts() =>
        (_dryRunLedger.Count, _paperLedger.Count, true);

    /// <summary>
    /// Practice evidence summary: dry-run/paper ledger counts, live always blocked,
    /// optional export text built from <see cref="EvidenceBuilder"/> / UI owner message.
    /// Never enables live orders or touches Toss order HTTP.
    /// </summary>
    public AppTradingEvidenceSummary GetTradingEvidenceSummary()
    {
        // Exercise fail-closed live gate; desktop host always reports blocked.
        _ = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());

        var snapshot = _evidenceBuilder.Build();
        var dry = snapshot.Summary.DryRunEntryCount;
        var paper = snapshot.Summary.PaperFillCount;

        // Real exporter over live ledger snapshots (shipped path).
        var exporter = new TradingEvidenceExporter(_dryRunLedger, _paperLedger);
        var exportText = exporter.ExportAsText();
        if (!exportText.Contains("live_orders=false", StringComparison.Ordinal)
            && !exportText.Contains("LiveSubmissionEnabled=false", StringComparison.OrdinalIgnoreCase))
        {
            exportText += "\nlive_orders=false\nLiveSubmissionEnabled=false";
        }

        return new AppTradingEvidenceSummary(
            DryRunCount: dry,
            PaperCount: paper,
            LiveBlocked: true,
            ExportText: exportText,
            Snapshot: snapshot);
    }

    /// <summary>
    /// Live readiness / owner-unlock status for the desktop host.
    /// Prefer LiveReadinessEvaluator when present in loaded assemblies; otherwise scan
    /// repo-root checklist/evidence/artifact paths via <see cref="System.IO"/>.
    /// Never enables live submission — file presence alone cannot unlock orders.
    /// </summary>
    public AppLiveReadinessReport GetLiveReadinessReport()
    {
        // Fail-closed gate exercise (defaults block).
        _ = _liveOrderGate.Evaluate(_settings, new LiveOrderContext());

        // If a future LiveReadinessEvaluator type is merged, prefer its Evaluate when callable.
        var fromEvaluator = TryEvaluateViaLiveReadinessEvaluator();
        if (fromEvaluator is not null)
        {
            return fromEvaluator;
        }

        return BuildReportFromArtifactScan();
    }

    private AppLiveReadinessReport? TryEvaluateViaLiveReadinessEvaluator()
    {
        Type? evaluatorType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                evaluatorType =
                    asm.GetType("TradingBot.Risk.LiveReadinessEvaluator")
                    ?? asm.GetType("TradingBot.Application.LiveReadinessEvaluator")
                    ?? asm.GetType("TradingBot.Domain.LiveReadinessEvaluator");
            }
            catch
            {
                continue;
            }

            if (evaluatorType is not null)
            {
                break;
            }
        }

        if (evaluatorType is null)
        {
            return null;
        }

        try
        {
            var evaluate = evaluatorType.GetMethod(
                "Evaluate",
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Instance);

            object? result = null;
            if (evaluate is not null && evaluate.IsStatic)
            {
                result = evaluate.Invoke(null, null);
            }
            else if (evaluate is not null)
            {
                var instance = Activator.CreateInstance(evaluatorType);
                result = evaluate.Invoke(instance, null);
            }

            if (result is null)
            {
                return null;
            }

            // Map common property names if the evaluator returns a DTO; still force LiveBlocked.
            var t = result.GetType();
            string ownerStatus = ReadStringProp(t, result, "OwnerUnlockStatus")
                ?? ReadStringProp(t, result, "Status")
                ?? "evaluator_present_live_blocked";
            bool checklist = ReadBoolProp(t, result, "ChecklistPresent") ?? false;
            bool evidence = ReadBoolProp(t, result, "EvidenceDocPresent") ?? false;
            bool artifactDir = ReadBoolProp(t, result, "LiveReadinessArtifactDirPresent") ?? false;
            bool script = ReadBoolProp(t, result, "AutomationScriptPresent") ?? false;
            IReadOnlyList<string> files =
                ReadStringListProp(t, result, "OwnerUnlockArtifactFiles")
                ?? Array.Empty<string>();

            return new AppLiveReadinessReport(
                LiveBlocked: true,
                IsLiveSubmissionEnabled: IsLiveSubmissionEnabled,
                OwnerUnlockStatus: ownerStatus,
                ChecklistPresent: checklist,
                EvidenceDocPresent: evidence,
                LiveReadinessArtifactDirPresent: artifactDir,
                AutomationScriptPresent: script,
                GatedLiveRouterRegistered: IsGatedLiveRouterRegistered,
                GatedLiveRouterUsedInPractice: false,
                SettingsAllowLiveOrders: _settings.AllowLiveOrders,
                SettingsKillSwitch: _settings.KillSwitch,
                SettingsOrderMode: _settings.OrderMode.ToString(),
                OwnerUnlockArtifactFiles: files,
                Summary: "LiveReadinessEvaluator present; desktop host still LiveBlocked=true.");
        }
        catch
        {
            // Evaluator wiring failed — fall back to artifact scan. Never open live.
            return null;
        }
    }

    private AppLiveReadinessReport BuildReportFromArtifactScan()
    {
        var root = TryFindRepoRoot();
        var checklistPresent = root is not null
            && File.Exists(Path.Combine(root, "docs", "LIVE_READINESS_CHECKLIST.md"));
        var evidencePresent = root is not null
            && File.Exists(Path.Combine(root, "docs", "plans", "LIVE_READINESS_EVIDENCE.md"));
        var scriptPresent = root is not null
            && File.Exists(Path.Combine(root, "scripts", "grok", "check-live-readiness.sh"));

        var artifactDir = root is null
            ? null
            : Path.Combine(root, "artifacts", "live-readiness");
        var artifactDirPresent = artifactDir is not null && Directory.Exists(artifactDir);

        var ownerFiles = new List<string>();
        if (artifactDirPresent && root is not null)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(
                             artifactDir!,
                             "*",
                             SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(path);
                    if (name.Contains("owner", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("unlock", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("phase7", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("signature", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("signoff", StringComparison.OrdinalIgnoreCase))
                    {
                        ownerFiles.Add(Path.GetRelativePath(root, path));
                    }
                }
            }
            catch (IOException)
            {
                // IO failure must not open live; report empty owner files.
            }
            catch (UnauthorizedAccessException)
            {
                // Same fail-closed handling.
            }
        }

        var ownerStatus = ResolveOwnerUnlockStatus(
            ownerFiles.Count,
            artifactDirPresent,
            checklistPresent,
            evidencePresent);

        var summary =
            "LIVE_READY=false; desktop CreateDefault uses dry-run/paper only; "
            + $"OwnerUnlockStatus={ownerStatus}; "
            + $"gatedLiveRouterRegistered={IsGatedLiveRouterRegistered}; "
            + "gatedLiveRouterUsedInPractice=false";

        return new AppLiveReadinessReport(
            LiveBlocked: true,
            IsLiveSubmissionEnabled: IsLiveSubmissionEnabled,
            OwnerUnlockStatus: ownerStatus,
            ChecklistPresent: checklistPresent,
            EvidenceDocPresent: evidencePresent,
            LiveReadinessArtifactDirPresent: artifactDirPresent,
            AutomationScriptPresent: scriptPresent,
            GatedLiveRouterRegistered: IsGatedLiveRouterRegistered,
            GatedLiveRouterUsedInPractice: false,
            SettingsAllowLiveOrders: _settings.AllowLiveOrders,
            SettingsKillSwitch: _settings.KillSwitch,
            SettingsOrderMode: _settings.OrderMode.ToString(),
            OwnerUnlockArtifactFiles: ownerFiles,
            Summary: summary);
    }

    private static string ResolveOwnerUnlockStatus(
        int ownerFileCount,
        bool artifactDirPresent,
        bool checklistPresent,
        bool evidencePresent)
    {
        if (ownerFileCount > 0)
        {
            // Artifacts may exist for ops logging; they do not unlock live in AppHarness.
            return "artifacts_present_but_live_still_blocked";
        }

        if (artifactDirPresent)
        {
            return "artifact_dir_present_no_owner_unlock";
        }

        if (checklistPresent || evidencePresent)
        {
            return "docs_present_owner_unlock_absent";
        }

        return "owner_unlock_absent";
    }

    /// <summary>
    /// Walk up from <see cref="AppContext.BaseDirectory"/> to find repo root (TradingBot.sln).
    /// Returns null when not found (e.g. packaged deploy without solution file).
    /// </summary>
    public static string? TryFindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TradingBot.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string? ReadStringProp(Type t, object instance, string name)
    {
        var p = t.GetProperty(name);
        return p?.GetValue(instance) as string;
    }

    private static bool? ReadBoolProp(Type t, object instance, string name)
    {
        var p = t.GetProperty(name);
        if (p?.GetValue(instance) is bool b)
        {
            return b;
        }

        return null;
    }

    private static IReadOnlyList<string>? ReadStringListProp(Type t, object instance, string name)
    {
        var p = t.GetProperty(name);
        var v = p?.GetValue(instance);
        return v switch
        {
            IReadOnlyList<string> list => list,
            IEnumerable<string> e => e.ToList(),
            _ => null,
        };
    }
}
