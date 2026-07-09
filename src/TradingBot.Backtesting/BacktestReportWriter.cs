using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// Writes offline multi-strategy backtest reports (markdown + JSON).
/// Simulation only — not investment advice; never places live orders.
/// </summary>
public static class BacktestReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Default report stem for VMAR CERS 6m: <c>artifacts/vmar_cers_6m_backtest_report</c>.
    /// </summary>
    public static string DefaultVmarCers6mStem(string repoRoot) =>
        Path.Combine(repoRoot, "artifacts", "vmar_cers_6m_backtest_report");

    public static BacktestReportPaths Write(
        string reportStem,
        BacktestReportDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportStem);
        ArgumentNullException.ThrowIfNull(document);

        var mdPath = reportStem.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? reportStem
            : reportStem + ".md";
        var jsonPath = reportStem.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? Path.ChangeExtension(reportStem, ".json")
            : reportStem + ".json";

        var dir = Path.GetDirectoryName(mdPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(mdPath, RenderMarkdown(document), Encoding.UTF8);
        // Slim JSON: drop equity curves, cap trades — full curve is huge for 1m 6m runs.
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(ToSlim(document), JsonOptions), Encoding.UTF8);
        return new BacktestReportPaths(mdPath, jsonPath);
    }

    private const int JsonTradeCap = 50;

    private static object ToSlim(BacktestReportDocument doc) => new
    {
        doc.Title,
        doc.Symbol,
        doc.Interval,
        doc.DataSource,
        doc.BarCount,
        doc.FirstBarTime,
        doc.LastBarTime,
        doc.FirstClose,
        doc.LastClose,
        Config = doc.Config,
        Results = doc.Results
            .OrderByDescending(r => r.TotalReturnPct)
            .Select(r => new
            {
                r.StrategyName,
                r.InitialCash,
                r.FinalEquity,
                r.TotalReturnPct,
                r.MaxDrawdownPct,
                r.Sharpe,
                r.TradeCount,
                r.WinRatePct,
                r.ProfitFactor,
                r.AvgHoldBars,
                r.Notes,
                Trades = r.Trades.Take(JsonTradeCap).ToList(),
                EquityCurvePointCount = r.EquityCurve.Count,
            })
            .ToList(),
        doc.GeneratedAtUtc,
        doc.Notes,
        Disclaimer = "simulation · not investment advice · past ≠ future · fills optimistic vs thin books",
    };

    public static string RenderMarkdown(BacktestReportDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var sb = new StringBuilder();
        sb.AppendLine($"# {doc.Title}");
        sb.AppendLine();
        sb.AppendLine("**투자 조언 아님. 과거 시뮬레이션이며 미래 수익을 보장하지 않습니다. 실주문 경로 아님.**");
        sb.AppendLine();
        sb.AppendLine("## 1. 데이터");
        sb.AppendLine();
        sb.AppendLine($"- 심볼: **{doc.Symbol}**");
        sb.AppendLine($"- 인터벌: **{doc.Interval}**");
        sb.AppendLine($"- 소스: {doc.DataSource}");
        sb.AppendLine($"- 봉 수: **{doc.BarCount:N0}**");
        if (doc.FirstBarTime is not null && doc.LastBarTime is not null)
        {
            sb.AppendLine(
                $"- 기간: **{FormatKst(doc.FirstBarTime.Value)} ~ {FormatKst(doc.LastBarTime.Value)}**");
        }

        if (doc.FirstClose is not null && doc.LastClose is not null && doc.FirstClose.Value > 0)
        {
            var chg = (doc.LastClose.Value / doc.FirstClose.Value - 1.0) * 100.0;
            sb.AppendLine(
                $"- 가격: **${doc.FirstClose.Value:F4} → ${doc.LastClose.Value:F4}** ({chg:F1}%)");
        }

        sb.AppendLine();
        sb.AppendLine("## 2. 비용 가정");
        sb.AppendLine();
        sb.AppendLine("| 항목 | 값 |");
        sb.AppendLine("|------|-----|");
        sb.AppendLine($"| 초기자본 | ${doc.Config.InitialCash:N0} |");
        sb.AppendLine($"| 수수료 편도 | {doc.Config.FeeRatePerSide:P2} |");
        sb.AppendLine($"| 슬리피지 편도 | {doc.Config.SlippageRatePerSide:P2} |");
        var rt = 2m * (doc.Config.FeeRatePerSide + doc.Config.SlippageRatePerSide);
        sb.AppendLine($"| 왕복 대략 | **{rt:P2}** |");
        sb.AppendLine($"| 쿨다운(봉) | {doc.Config.CooldownBarsAfterExit} |");
        sb.AppendLine($"| 정규장 신호만 | {doc.Config.RegularSessionOnly} |");
        sb.AppendLine();
        sb.AppendLine("## 3. 전략 성적");
        sb.AppendLine();
        sb.AppendLine("| 순위 | 전략 | 총수익% | MDD% | Sharpe | 거래수 | 승률% | PF | 평균보유(봉) |");
        sb.AppendLine("|------|------|---------|------|--------|--------|-------|-----|--------------|");

        var ranked = doc.Results
            .OrderByDescending(r => r.TotalReturnPct)
            .ThenBy(r => r.StrategyName, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
        {
            var r = ranked[i];
            sb.AppendLine(
                $"| {i + 1} | `{EscapeMd(r.StrategyName)}` | {r.TotalReturnPct:F2} | {r.MaxDrawdownPct:F2} | " +
                $"{r.Sharpe:F2} | {r.TradeCount} | {r.WinRatePct:F1} | {r.ProfitFactor:F2} | {r.AvgHoldBars:F1} |");
        }

        sb.AppendLine();
        sb.AppendLine("## 4. 결론 (정직)");
        sb.AppendLine();
        if (ranked.Count > 0)
        {
            var top = ranked[0];
            sb.AppendLine($"### 1위: `{EscapeMd(top.StrategyName)}`");
            sb.AppendLine(
                $"- 총수익 **{top.TotalReturnPct:F2}%** · MDD {top.MaxDrawdownPct:F2}% · 거래 {top.TradeCount}회 · 승률 {top.WinRatePct:F1}%");
            sb.AppendLine();
        }

        sb.AppendLine("### 핵심");
        sb.AppendLine("1. **과거 시뮬레이션 ≠ 미래 수익.** 실주문 게이트와 무관합니다.");
        sb.AppendLine("2. 1분봉 + 편도 수수료·슬리피지는 스캘프 엣지를 쉽게 잠식합니다.");
        sb.AppendLine("3. 플러스 결과가 나와도 과최적화·호가 두께·생존 편향을 의심해야 합니다.");
        sb.AppendLine("4. 체결은 **다음 봉 시가 ± 슬리피지** 가정 — 저유동 VMAR 실호가보다 낙관적일 수 있습니다.");
        sb.AppendLine("5. 전액 재투자(복리) 가정 — 소액·분할 시 결과는 달라집니다.");
        sb.AppendLine("6. 라이브 주문 기본값은 차단입니다. 백테스트가 실주문을 열지 않습니다.");
        sb.AppendLine();

        var buyHold = ranked.FirstOrDefault(r =>
            r.StrategyName.Equals("BuyHold", StringComparison.OrdinalIgnoreCase));
        if (buyHold is not null && ranked[0].TotalReturnPct > 0 && buyHold.TotalReturnPct < -50m)
        {
            sb.AppendLine(
                $"> 참고: 같은 기간 BuyHold는 **{buyHold.TotalReturnPct:F1}%**. " +
                "커스텀 지표 플러스는 ‘하락장 롱 스캘프 타이밍’ 시뮬레이션 결과이며 보장 아님.");
            sb.AppendLine();
        }

        if (ranked.Count > 0 && ranked[0].Trades.Count > 0)
        {
            sb.AppendLine("## 5. 1위 매매 샘플 (최대 30건)");
            sb.AppendLine();
            sb.AppendLine("| 진입 | 청산 | 진입가 | 청산가 | PnL$ | % | 봉 | 사유 |");
            sb.AppendLine("|------|------|--------|--------|------|---|-----|------|");
            foreach (var t in ranked[0].Trades.Take(30))
            {
                var hold = t.ExitIndex - t.EntryIndex;
                sb.AppendLine(
                    $"| {FormatShort(t.EntryTime)} | {FormatShort(t.ExitTime)} | " +
                    $"{t.EntryPrice:F4} | {t.ExitPrice:F4} | {t.PnLUsd:F2} | {t.ReturnPct:F1} | {hold} | " +
                    $"{EscapeMd(t.EntryReason)}/{EscapeMd(t.ExitReason)} |");
            }

            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine($"생성: {doc.GeneratedAtUtc:O} UTC · notes: {doc.Notes}");
        return sb.ToString();
    }

    public static BacktestReportDocument BuildDocument(
        string symbol,
        string interval,
        string dataSource,
        IReadOnlyList<CandlePoint> candles,
        BacktestConfig config,
        IReadOnlyList<BacktestResult> results,
        string? title = null,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(results);

        double? firstClose = candles.Count > 0 ? candles[0].Close : null;
        double? lastClose = candles.Count > 0 ? candles[^1].Close : null;
        DateTimeOffset? first = candles.Count > 0 ? candles[0].Time : null;
        DateTimeOffset? last = candles.Count > 0 ? candles[^1].Time : null;

        return new BacktestReportDocument(
            Title: title ?? $"{symbol} {interval} multi-strategy backtest (CERS suite)",
            Symbol: symbol,
            Interval: interval,
            DataSource: dataSource,
            BarCount: candles.Count,
            FirstBarTime: first,
            LastBarTime: last,
            FirstClose: firstClose,
            LastClose: lastClose,
            Config: config,
            Results: results.ToList(),
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Notes: notes ?? "simulation · not investment advice · live orders blocked");
    }

    private static string FormatKst(DateTimeOffset t) =>
        KoreaTime.FormatFull(t);

    private static string FormatShort(DateTimeOffset t)
    {
        var k = KoreaTime.ToKstDateTime(t);
        return k.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string EscapeMd(string? s) =>
        (s ?? string.Empty).Replace("|", "/", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

/// <summary>Paths written by <see cref="BacktestReportWriter"/>.</summary>
public sealed record BacktestReportPaths(string MarkdownPath, string JsonPath);

/// <summary>Serializable multi-strategy report (markdown + JSON).</summary>
public sealed record BacktestReportDocument(
    string Title,
    string Symbol,
    string Interval,
    string DataSource,
    int BarCount,
    DateTimeOffset? FirstBarTime,
    DateTimeOffset? LastBarTime,
    double? FirstClose,
    double? LastClose,
    BacktestConfig Config,
    IReadOnlyList<BacktestResult> Results,
    DateTimeOffset GeneratedAtUtc,
    string Notes);
