using System.Text;
using TradingBot.Domain;

namespace TradingBot.Backtesting;

/// <summary>
/// Grid-search split ladder parameters for research ranking.
/// Composite score = TotalReturnPct - 0.5*MDD + 5*Sharpe (trade-count floor).
/// Simulation only — not investment advice; not a live optimizer.
/// </summary>
public static class SplitLadderOptimizer
{
    public sealed record Ranked(
        SplitLadderParams Params,
        BacktestResult Result,
        double Score);

    /// <summary>
    /// Mathematical grid over legs / step / SL / TP / hold (CERS entry fixed thr=0.006).
    /// </summary>
    public static IReadOnlyList<Ranked> GridSearch(
        IReadOnlyList<CandlePoint> candles,
        BacktestConfig? config = null,
        Func<CandlePoint, bool>? includeBar = null,
        int minTrades = 20)
    {
        ArgumentNullException.ThrowIfNull(candles);
        config ??= new BacktestConfig(MaxHoldBars: 0);
        var expected = CersMath.ComputeExpectedEdge(candles);

        // Compact grid (~1k) for 40k-bar runs; project default always included.
        int[] buyLegs = [2, 3, 4, 5];
        double[] buySteps = [0.05, 0.10, 0.20, 0.50];
        double[] sellSteps = [0.10, 0.20, 0.50];
        double[] stops = [1.0, 1.2, 2.0, 3.0];
        double[] tps = [1.0, 1.5, 2.0, 3.0];
        int[] holds = [30, 40, 60];

        var ranked = new List<Ranked>();
        // Also evaluate project default explicitly
        var defaults = new List<SplitLadderParams> { SplitLadderParams.ProjectDefault };

        foreach (var bl in buyLegs)
        {
            foreach (var bs in buySteps)
            {
                foreach (var ss in sellSteps)
                {
                    foreach (var sl in stops)
                    {
                        foreach (var tp in tps)
                        {
                            foreach (var h in holds)
                            {
                                defaults.Add(new SplitLadderParams(
                                    BuyLegs: bl,
                                    BuyStepPercent: bs,
                                    SellLegs: bl,
                                    SellStepPercent: ss,
                                    StopLossFromAvgPercent: sl,
                                    TakeProfitFromAvgPercent: tp,
                                    EntryThreshold: CersPreset.EntryThreshold,
                                    MaxHoldBars: h,
                                    CooldownBarsAfterExit: 3));
                            }
                        }
                    }
                }
            }
        }

        // De-dupe by name
        var unique = defaults
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        foreach (var p in unique)
        {
            var result = SplitLadderEngine.Run(candles, p, config, expected, includeBar);
            if (result.TradeCount < minTrades && p.Name != SplitLadderParams.ProjectDefault.Name)
            {
                // keep default even if low trades for comparison
                continue;
            }

            var score = (double)result.TotalReturnPct
                        - (0.5 * (double)result.MaxDrawdownPct)
                        + (5.0 * result.Sharpe);
            ranked.Add(new Ranked(p, result, score));
        }

        // Ensure project default always ranked
        if (ranked.All(r => r.Params.Name != SplitLadderParams.ProjectDefault.Name))
        {
            var d = SplitLadderParams.ProjectDefault;
            var result = SplitLadderEngine.Run(candles, d, config, expected, includeBar);
            var score = (double)result.TotalReturnPct
                        - (0.5 * (double)result.MaxDrawdownPct)
                        + (5.0 * result.Sharpe);
            ranked.Add(new Ranked(d, result, score));
        }

        return ranked
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Result.TotalReturnPct)
            .ToList();
    }

    public static string RenderMarkdownReport(
        IReadOnlyList<Ranked> ranked,
        string symbol,
        string interval,
        int barCount,
        string dataSource,
        DateTimeOffset? first,
        DateTimeOffset? last,
        double? firstClose,
        double? lastClose,
        int topN = 25)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        var sb = new StringBuilder();
        sb.AppendLine("# VMAR 분할매수·분할매도 그리드 백테스트");
        sb.AppendLine();
        sb.AppendLine("**투자 조언 아님. 과거 시뮬레이션이며 미래 수익을 보장하지 않습니다. 실주문 경로 아님.**");
        sb.AppendLine();
        sb.AppendLine("## 1. 데이터");
        sb.AppendLine();
        sb.AppendLine($"- 심볼: **{symbol}**");
        sb.AppendLine($"- 인터벌: **{interval}**");
        sb.AppendLine($"- 소스: {dataSource}");
        sb.AppendLine($"- 봉 수: **{barCount:N0}**");
        if (first is not null && last is not null)
        {
            sb.AppendLine($"- 기간: **{KoreaTime.FormatFull(first.Value)} ~ {KoreaTime.FormatFull(last.Value)}**");
        }

        if (firstClose is not null && lastClose is not null && firstClose > 0)
        {
            var chg = (lastClose.Value / firstClose.Value - 1.0) * 100.0;
            sb.AppendLine($"- 가격: **${firstClose:F4} → ${lastClose:F4}** ({chg:F1}%)");
        }

        sb.AppendLine();
        sb.AppendLine("## 2. 모델 (수학)");
        sb.AppendLine();
        sb.AppendLine("1. **진입 신호**: CERS `expected > 0.006` (비용 2× 왕복 0.3%)");
        sb.AppendLine("2. **분할매수**: 기준 종가 대비 `ref × (1 − i·step%/100)` 에 균등 예산 LIMIT 레그");
        sb.AppendLine("3. **체결**: 봉 low ≤ 지정가 → 지정가(+슬리피지) 매수");
        sb.AppendLine("4. **분할매도**: 평균단가 대비 `avg × (1 + (j+1)·sellStep%/100)` 레그");
        sb.AppendLine("5. **손절/익절**: 평균단가 기준 SL% / TP% (intrabar high/low)");
        sb.AppendLine("6. **점수**: `Score = 총수익% − 0.5·MDD% + 5·Sharpe` (최소 거래수 필터)");
        sb.AppendLine("7. 비용: 수수료 0.1% + 슬리피지 0.05% 편도");
        sb.AppendLine();
        sb.AppendLine("## 3. 상위 결과");
        sb.AppendLine();
        sb.AppendLine("| 순위 | 설정 | 총수익% | MDD% | Sharpe | 거래 | 승률% | PF | Score |");
        sb.AppendLine("|------|------|---------|------|--------|------|-------|-----|-------|");

        var top = ranked.Take(topN).ToList();
        for (var i = 0; i < top.Count; i++)
        {
            var r = top[i];
            var p = r.Params;
            var res = r.Result;
            var label =
                $"B{p.BuyLegs}×{p.BuyStepPercent:0.##}%/S{p.SellStepPercent:0.##}% SL{p.StopLossFromAvgPercent:0.##} TP{p.TakeProfitFromAvgPercent:0.##} H{p.MaxHoldBars}";
            sb.AppendLine(
                $"| {i + 1} | `{label}` | {res.TotalReturnPct:F2} | {res.MaxDrawdownPct:F2} | " +
                $"{res.Sharpe:F2} | {res.TradeCount} | {res.WinRatePct:F1} | {res.ProfitFactor:F2} | {r.Score:F2} |");
        }

        sb.AppendLine();
        if (top.Count > 0)
        {
            var best = top[0];
            sb.AppendLine("## 4. 수학적으로 최고 점수 조건 (이 구간 한정)");
            sb.AppendLine();
            sb.AppendLine($"### 1위: `{best.Params.Name}`");
            sb.AppendLine();
            sb.AppendLine("| 파라미터 | 값 |");
            sb.AppendLine("|----------|-----|");
            sb.AppendLine($"| 분할매수 레그 수 | **{best.Params.BuyLegs}** |");
            sb.AppendLine($"| 매수 간격 (step%) | **{best.Params.BuyStepPercent:0.##}%** |");
            sb.AppendLine($"| 분할매도 레그 수 | **{best.Params.SellLegs}** |");
            sb.AppendLine($"| 매도 간격 (step%) | **{best.Params.SellStepPercent:0.##}%** |");
            sb.AppendLine($"| 손절 (평균단가 대비) | **{best.Params.StopLossFromAvgPercent:0.##}%** |");
            sb.AppendLine($"| 익절 (평균단가 대비) | **{best.Params.TakeProfitFromAvgPercent:0.##}%** |");
            sb.AppendLine($"| 진입 문턱 (CERS) | **{best.Params.EntryThreshold:0.####}** |");
            sb.AppendLine($"| 최대 보유 봉 | **{best.Params.MaxHoldBars}** |");
            sb.AppendLine($"| 총수익% | **{best.Result.TotalReturnPct:F2}** |");
            sb.AppendLine($"| MDD% | **{best.Result.MaxDrawdownPct:F2}** |");
            sb.AppendLine($"| 거래 수 | **{best.Result.TradeCount}** |");
            sb.AppendLine($"| Score | **{best.Score:F2}** |");
            sb.AppendLine();
        }

        // Project default comparison
        var proj = ranked.FirstOrDefault(r =>
            r.Params.BuyLegs == 3
            && Math.Abs(r.Params.BuyStepPercent - 0.10) < 1e-9
            && Math.Abs(r.Params.StopLossFromAvgPercent - 1.2) < 1e-9);
        sb.AppendLine("## 5. 프로젝트 기본(3레그·0.1%·SL1.2%) 비교");
        sb.AppendLine();
        if (proj is not null)
        {
            sb.AppendLine(
                $"- 기본 설정 수익 **{proj.Result.TotalReturnPct:F2}%** · MDD {proj.Result.MaxDrawdownPct:F2}% · " +
                $"거래 {proj.Result.TradeCount} · Score {proj.Score:F2}");
        }
        else
        {
            sb.AppendLine("- 기본 설정 결과 없음 (필터/데이터)");
        }

        sb.AppendLine();
        sb.AppendLine("## 6. 정직 한계");
        sb.AppendLine();
        sb.AppendLine("1. **이 구간(VMAR 하락장)에 맞춘 그리드** — 다른 기간·종목에서 1위가 바뀔 수 있음 (과최적화).");
        sb.AppendLine("2. 지정가 체결은 low/high 터치 가정 — 실호가보다 낙관적일 수 있음.");
        sb.AppendLine("3. 전액 예산 분할 — 실계좌 부분 자금과 다름.");
        sb.AppendLine("4. **실주문 검증 아님.** Live 기본 차단.");
        sb.AppendLine();
        sb.AppendLine($"생성: {DateTimeOffset.UtcNow:O} UTC");
        return sb.ToString();
    }
}
