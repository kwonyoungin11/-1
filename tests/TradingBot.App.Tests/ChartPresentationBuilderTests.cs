using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TradingBot.App.ViewModels;
using TradingBot.Domain;

namespace TradingBot.App.Tests;

/// <summary>
/// Exercises shipped ChartPresentationBuilder (Supercharts-grade status line, volume format, linked X axes, RSI pane).
/// No re-implemented oracles — asserts on Build() outputs from representative candles.
/// </summary>
public class ChartPresentationBuilderTests
{
    [Fact]
    public void Build_from_candles_produces_dual_panes_ohlc_status_and_overlays()
    {
        var candles = SampleCandles(count: 40, startClose: 100, lastClose: 110, lastVolume: 2_500_000);
        var bracket = SampleBracket(entry: 109.5m, stop: 105m, tp: 118m);
        var indicators = new[]
        {
            new ChartIndicatorLine("SMA20", candles.Select((_, i) => (double?)(100 + i * 0.2)).ToArray()),
        };

        var bundle = ChartPresentationBuilder.Build(
            candles,
            markers: Array.Empty<TradeMarker>(),
            indicators,
            bracket,
            ChartTimeframe.분봉15);

        Assert.NotEmpty(bundle.PriceSeries);
        Assert.NotEmpty(bundle.VolumeSeries);
        Assert.Single(bundle.PriceXAxes);
        Assert.Single(bundle.VolumeXAxes);
        Assert.NotSame(bundle.PriceXAxes[0], bundle.VolumeXAxes[0]);

        // Supercharts status line must include O/H/L/C + change + volume from real last candle
        var status = bundle.StatusLineText;
        Assert.False(string.IsNullOrWhiteSpace(status));
        Assert.Contains("O ", status, StringComparison.Ordinal);
        Assert.Contains("H ", status, StringComparison.Ordinal);
        Assert.Contains("L ", status, StringComparison.Ordinal);
        Assert.Contains("C ", status, StringComparison.Ordinal);
        Assert.Contains("Vol ", status, StringComparison.Ordinal);
        Assert.Contains(bundle.LastCloseText, status, StringComparison.Ordinal);
        Assert.Equal(candles[^1].Close.ToString("N2"), bundle.LastCloseText);
        Assert.True(bundle.ChangeIsPositive); // 100 → 110
        Assert.Contains("%", bundle.ChangeText, StringComparison.Ordinal);

        // Volume series includes SMA overlay (name contains SMA)
        Assert.Contains(bundle.VolumeSeries, s => s.Name is not null && s.Name.Contains("SMA", StringComparison.Ordinal));

        // ENTRY/SL/TP present as named series
        var names = bundle.PriceSeries.Select(s => s.Name).Where(n => n is not null).ToArray();
        Assert.Contains("ENTRY", names);
        Assert.Contains("SL", names);
        Assert.Contains("TP", names);
        Assert.Contains("Last", names);
        Assert.Equal(bundle.LastCloseText, bundle.LastPriceTag);
        Assert.Equal(candles[^1].Close.ToString("N2"), bundle.LastPriceTag);
        Assert.True(bundle.LastPriceIsUp);
        Assert.Equal(string.Empty, bundle.WatermarkText);

        // RSI pane populated for ≥20 bars
        Assert.NotNull(bundle.RsiSeries);
        Assert.NotEmpty(bundle.RsiSeries!);
        Assert.Contains(bundle.RsiSeries!, s => s.Name == "RSI14");
        Assert.NotNull(bundle.RsiXAxes);
        Assert.Single(bundle.RsiXAxes!);
        Assert.NotNull(bundle.RsiYAxes);
        Assert.Single(bundle.RsiYAxes!);
        Assert.Equal(0, bundle.RsiYAxes![0].MinLimit);
        Assert.Equal(100, bundle.RsiYAxes[0].MaxLimit);
        Assert.StartsWith("RSI14 ", bundle.RsiStatusText, StringComparison.Ordinal);
        Assert.NotNull(bundle.RsiFrame);
        Assert.NotNull(bundle.RsiMargin);
        Assert.NotNull(bundle.RsiSections);
        Assert.NotEmpty(bundle.RsiSections!);
    }

    [Fact]
    public void Build_with_enough_candles_produces_non_empty_rsi_series_and_fixed_y_scale()
    {
        var candles = SampleCandles(count: 30, startClose: 50, lastClose: 55, lastVolume: 100_000);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(54m, 48m, 60m),
            ChartTimeframe.분봉15);

        Assert.NotNull(bundle.RsiSeries);
        Assert.Contains(bundle.RsiSeries!, s => s.Name == "RSI14");
        Assert.NotNull(bundle.RsiYAxes);
        Assert.Equal(0d, bundle.RsiYAxes![0].MinLimit);
        Assert.Equal(100d, bundle.RsiYAxes[0].MaxLimit);
        Assert.Matches(@"RSI14 \d", bundle.RsiStatusText);
    }

    [Fact]
    public void Linked_time_axes_share_MinMax_when_either_pane_zooms()
    {
        var candles = SampleCandles(count: 25, startClose: 50, lastClose: 52, lastVolume: 10_000);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(51m, 49m, 55m),
            ChartTimeframe.분봉15);

        var priceX = bundle.PriceXAxes[0];
        var volumeX = bundle.VolumeXAxes[0];
        Assert.NotNull(bundle.RsiXAxes);
        var rsiX = bundle.RsiXAxes![0];

        // Price zoom → volume + rsi follow
        priceX.MinLimit = 1_000_000d;
        priceX.MaxLimit = 2_000_000d;
        Assert.Equal(priceX.MinLimit, volumeX.MinLimit);
        Assert.Equal(priceX.MaxLimit, volumeX.MaxLimit);
        Assert.Equal(priceX.MinLimit, rsiX.MinLimit);
        Assert.Equal(priceX.MaxLimit, rsiX.MaxLimit);

        // Volume pan → price + rsi follow
        volumeX.MinLimit = 500_000d;
        volumeX.MaxLimit = 1_500_000d;
        Assert.Equal(volumeX.MinLimit, priceX.MinLimit);
        Assert.Equal(volumeX.MaxLimit, priceX.MaxLimit);
        Assert.Equal(volumeX.MinLimit, rsiX.MinLimit);
        Assert.Equal(volumeX.MaxLimit, rsiX.MaxLimit);

        // RSI pan → price + volume follow
        rsiX.MinLimit = 2_000_000d;
        rsiX.MaxLimit = 3_000_000d;
        Assert.Equal(rsiX.MinLimit, priceX.MinLimit);
        Assert.Equal(rsiX.MaxLimit, priceX.MaxLimit);
        Assert.Equal(rsiX.MinLimit, volumeX.MinLimit);
        Assert.Equal(rsiX.MaxLimit, volumeX.MaxLimit);

        priceX.MinLimit = 2_500_000d;
        priceX.MaxLimit = 3_500_000d;
        Assert.Equal(priceX.MinLimit, volumeX.MinLimit);
        Assert.Equal(priceX.MaxLimit, volumeX.MaxLimit);
        Assert.Equal(priceX.MinLimit, rsiX.MinLimit);
        Assert.Equal(priceX.MaxLimit, rsiX.MaxLimit);
        volumeX.MinLimit = 2_750_000d;
        volumeX.MaxLimit = 3_750_000d;
        Assert.Equal(volumeX.MinLimit, priceX.MinLimit);
        Assert.Equal(volumeX.MaxLimit, priceX.MaxLimit);
        Assert.Equal(volumeX.MinLimit, rsiX.MinLimit);
        Assert.Equal(volumeX.MaxLimit, rsiX.MaxLimit);
    }

    [Fact]
    public void FormatVolume_uses_K_M_B_suffixes_on_shipped_helper()
    {
        Assert.Equal("500", ChartPresentationBuilder.FormatVolume(500));
        Assert.Equal("1.5K", ChartPresentationBuilder.FormatVolume(1_500));
        Assert.Equal("2.5M", ChartPresentationBuilder.FormatVolume(2_500_000));
        Assert.Equal("1.2B", ChartPresentationBuilder.FormatVolume(1_200_000_000));
    }

    [Fact]
    public void ResolveHoverOhlcStatus_nearest_bar_matches_FormatOhlcStatus_for_that_bar()
    {
        var candles = SampleCandles(count: 8, startClose: 100, lastClose: 108, lastVolume: 9_000);
        var target = candles[3];
        var prev = candles[2];

        var hover = ChartPresentationBuilder.ResolveHoverOhlcStatus(candles, target.Time);
        var expected = ChartPresentationBuilder.FormatOhlcStatus(target, prev);

        Assert.Equal(expected, hover);
        Assert.Contains($"C {target.Close:N2}", hover, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveHoverOhlcStatus_empty_or_far_returns_empty_not_last_bar_lie()
    {
        Assert.Equal(string.Empty, ChartPresentationBuilder.ResolveHoverOhlcStatus(
            Array.Empty<CandlePoint>(),
            DateTimeOffset.UtcNow));

        var candles = SampleCandles(count: 4, startClose: 50, lastClose: 52, lastVolume: 100);
        var far = candles[0].Time.AddYears(-2);
        Assert.Equal(string.Empty, ChartPresentationBuilder.ResolveHoverOhlcStatus(candles, far));
    }

    [Fact]
    public void FormatLastPriceAxisBadge_shows_price_and_direction()
    {
        var upLast = new CandlePoint(
            new DateTimeOffset(2026, 7, 1, 16, 0, 0, TimeSpan.Zero),
            Open: 10, High: 11, Low: 9.5, Close: 10.75, Volume: 100);
        var upPrev = new CandlePoint(
            new DateTimeOffset(2026, 7, 1, 15, 45, 0, TimeSpan.Zero),
            Open: 10, High: 10.2, Low: 9.8, Close: 10.0, Volume: 90);

        var (textUp, isUp) = ChartPresentationBuilder.FormatLastPriceAxisBadge(upLast, upPrev);
        Assert.Equal("10.75", textUp);
        Assert.True(isUp);

        var downLast = upLast with { Close = 9.5 };
        var (textDown, isDownUp) = ChartPresentationBuilder.FormatLastPriceAxisBadge(downLast, upPrev);
        Assert.Equal("9.50", textDown);
        Assert.False(isDownUp);
    }

    [Fact]
    public void Build_exposes_last_price_axis_badge_and_sparse_price_y_separators()
    {
        var candles = SampleCandles(count: 40, startClose: 76, lastClose: 77, lastVolume: 1_000);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(76.5m, 74m, 80m),
            ChartTimeframe.분봉15);

        Assert.Equal(bundle.LastPriceTag, bundle.LastPriceAxisBadge);
        Assert.Equal(candles[^1].Close.ToString("N2"), bundle.LastPriceAxisBadge);
        Assert.Equal(bundle.LastPriceIsUp, bundle.LastPriceAxisBadgeIsUp);

        var priceY = bundle.PriceYAxes[0];
        Assert.NotNull(priceY.CustomSeparators);
        var seps = priceY.CustomSeparators!.ToArray();
        Assert.InRange(seps.Length, 2, 5);
        Assert.True(priceY.ForceStepToMin);
    }

    [Fact]
    public void FormatOhlcStatus_matches_tv_style_and_Build_status_line()
    {
        var last = new CandlePoint(
            new DateTimeOffset(2026, 7, 1, 15, 0, 0, TimeSpan.Zero),
            Open: 10, High: 12, Low: 9, Close: 11, Volume: 1_500);
        var prev = new CandlePoint(
            new DateTimeOffset(2026, 7, 1, 14, 45, 0, TimeSpan.Zero),
            Open: 9, High: 10, Low: 8.5, Close: 10, Volume: 1_000);

        var status = ChartPresentationBuilder.FormatOhlcStatus(last, prev);
        Assert.Contains("O 10.00", status, StringComparison.Ordinal);
        Assert.Contains("H 12.00", status, StringComparison.Ordinal);
        Assert.Contains("L 9.00", status, StringComparison.Ordinal);
        Assert.Contains("C 11.00", status, StringComparison.Ordinal);
        Assert.Contains("%", status, StringComparison.Ordinal);
        Assert.Contains("Vol 1.5K", status, StringComparison.Ordinal);

        var candles = SampleCandles(count: 5, startClose: 80, lastClose: 76.95, lastVolume: 500);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(77m, 74m, 82m),
            ChartTimeframe.분봉15,
            dataSourceWatermark: "Toss REST");

        Assert.Equal(bundle.LastCloseText, bundle.LastPriceTag);
        Assert.Equal(ChartPresentationBuilder.FormatOhlcStatus(candles[^1], candles[^2]), bundle.StatusLineText);
        Assert.Equal("Toss REST", bundle.WatermarkText);
        Assert.Contains("Toss REST", bundle.IndicatorLegend, StringComparison.Ordinal);
        Assert.False(bundle.LastPriceIsUp);
        Assert.Contains(bundle.PriceSeries, s => s.Name == "Last");
    }

    [Fact]
    public void Build_empty_candles_returns_honest_empty_status_not_fake_series()
    {
        var bundle = ChartPresentationBuilder.Build(
            Array.Empty<CandlePoint>(),
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            TradeBracketPlan.Invalid("SPCX", "no data"),
            ChartTimeframe.분봉15);

        Assert.Empty(bundle.PriceSeries);
        Assert.Empty(bundle.VolumeSeries);
        Assert.NotNull(bundle.RsiSeries);
        Assert.Empty(bundle.RsiSeries!);
        Assert.NotNull(bundle.RsiXAxes);
        Assert.Empty(bundle.RsiXAxes!);
        Assert.NotNull(bundle.RsiYAxes);
        Assert.Empty(bundle.RsiYAxes!);
        Assert.Equal("RSI14 —", bundle.RsiStatusText);
        Assert.Contains("없음", bundle.StatusLineText, StringComparison.Ordinal);
        Assert.Equal("—", bundle.LastCloseText);
    }

    [Fact]
    public void MainWindow_axaml_has_no_page_ScrollViewer_and_dual_ZoomMode_X()
    {
        var axamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TradingBot.App", "Views", "MainWindow.axaml"));
        // Fallback: walk up from test assembly
        if (!File.Exists(axamlPath))
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "TradingBot.App", "Views", "MainWindow.axaml")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            axamlPath = Path.Combine(dir!.FullName, "src", "TradingBot.App", "Views", "MainWindow.axaml");
        }

        Assert.True(File.Exists(axamlPath), $"MainWindow.axaml not found at {axamlPath}");
        var text = File.ReadAllText(axamlPath);
        // Element tags only (comments may mention ScrollViewer by name)
        Assert.DoesNotContain("<ScrollViewer", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("</ScrollViewer>", text, StringComparison.OrdinalIgnoreCase);
        // both panes zoom X
        var zoomCount = text.Split("ZoomMode=\"X\"", StringSplitOptions.None).Length - 1;
        Assert.True(zoomCount >= 2, $"expected ≥2 ZoomMode=X, found {zoomCount}");
        Assert.Contains("OhlcStatusLine", text, StringComparison.Ordinal);
        Assert.Contains("VolumeXAxes", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_right_draw_margin_is_at_least_72_on_all_panes()
    {
        var candles = SampleCandles(count: 40, startClose: 100, lastClose: 110, lastVolume: 2_500_000);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(109.5m, 105m, 118m),
            ChartTimeframe.분봉15);

        Assert.True(bundle.PriceMargin.Right >= 72f, $"PriceMargin.Right={bundle.PriceMargin.Right}");
        Assert.True(bundle.VolumeMargin.Right >= 72f, $"VolumeMargin.Right={bundle.VolumeMargin.Right}");
        Assert.NotNull(bundle.RsiMargin);
        Assert.True(bundle.RsiMargin!.Right >= 72f, $"RsiMargin.Right={bundle.RsiMargin.Right}");
    }

    [Fact]
    public void Build_max_bar_width_is_density_aware_between_8_and_14_for_typical_windows()
    {
        foreach (var count in new[] { 40, 80, 120 })
        {
            var candles = SampleCandles(count, startClose: 100, lastClose: 105, lastVolume: 100_000);
            var bundle = ChartPresentationBuilder.Build(
                candles,
                Array.Empty<TradeMarker>(),
                Array.Empty<ChartIndicatorLine>(),
                SampleBracket(104m, 98m, 112m),
                ChartTimeframe.분봉15);

            var candle = Assert.IsType<CandlesticksSeries<LiveChartsCore.Defaults.FinancialPoint>>(
                bundle.PriceSeries.Single(s => s.Name == "SPCX" || s.Name == WatchlistCatalog.PrimarySymbol));
            Assert.InRange(candle.MaxBarWidth, 8.0, 14.0);
        }
    }

    [Fact]
    public void Build_does_not_emit_LastAccent_series()
    {
        var candles = SampleCandles(count: 40, startClose: 100, lastClose: 110, lastVolume: 1_000);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(109m, 105m, 118m),
            ChartTimeframe.분봉15);

        Assert.DoesNotContain(bundle.PriceSeries, s => s.Name == "LastAccent");
    }

    [Fact]
    public void Build_filters_trend_follow_overlays_to_ema9_and_ema21_only()
    {
        var candles = SampleCandles(count: 40, startClose: 100, lastClose: 110, lastVolume: 1_000);
        var values = candles.Select((_, i) => (double?)(100 + i * 0.15)).ToArray();
        var indicators = new[]
        {
            new ChartIndicatorLine("SMA20", values),
            new ChartIndicatorLine("SMA60", values),
            new ChartIndicatorLine("EMA9", values),
            new ChartIndicatorLine("EMA21", values),
        };

        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            indicators,
            SampleBracket(109m, 105m, 118m),
            ChartTimeframe.분봉15);

        var names = bundle.PriceSeries
            .Select(s => s.Name)
            .Where(n => n is not null)
            .ToArray();

        Assert.Contains("EMA9", names);
        Assert.Contains("EMA21", names);
        Assert.DoesNotContain("SMA20", names);
        Assert.DoesNotContain("SMA60", names);
        Assert.DoesNotContain(names, n => n!.StartsWith("SMA", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_change_text_uses_previous_bar_not_window_first()
    {
        var t0 = new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero);
        var candles = new List<CandlePoint>
        {
            new(t0, 99.8, 100.5, 99.5, 100.0, 50_000),
            new(t0.AddMinutes(15), 100.0, 102.0, 99.8, 101.5, 51_000),
            new(t0.AddMinutes(30), 101.5, 104.0, 101.0, 103.0, 52_000),
            new(t0.AddMinutes(45), 103.0, 106.0, 102.5, 105.0, 53_000),
            new(t0.AddMinutes(60), 105.0, 111.0, 104.5, 110.0, 54_000),
        };

        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(109m, 100m, 120m),
            ChartTimeframe.분봉15);

        var last = candles[^1];
        var prev = candles[^2];
        var first = candles[0];

        var prevBarChg = (last.Close - prev.Close) / prev.Close * 100.0;
        var windowFirstChg = (last.Close - first.Close) / first.Close * 100.0;
        var expectedPrev = prevBarChg.ToString("+0.00;-0.00;0.00") + "%";
        var windowFirstText = windowFirstChg.ToString("+0.00;-0.00;0.00") + "%";

        Assert.Equal(expectedPrev, bundle.ChangeText);
        Assert.NotEqual(windowFirstText, bundle.ChangeText);

        var status = ChartPresentationBuilder.FormatOhlcStatus(last, prev);
        Assert.Contains(expectedPrev, status, StringComparison.Ordinal);
        Assert.Contains(bundle.ChangeText, status, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_x_axis_labels_only_on_bottom_rsi_pane()
    {
        var candles = SampleCandles(count: 40, startClose: 100, lastClose: 110, lastVolume: 1_000);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(109m, 105m, 118m),
            ChartTimeframe.분봉15);

        Assert.True(IsLabelsPaintHidden(bundle.PriceXAxes[0].LabelsPaint), "PriceX labels must be hidden");
        Assert.True(IsLabelsPaintHidden(bundle.VolumeXAxes[0].LabelsPaint), "VolumeX labels must be hidden");
        Assert.NotNull(bundle.RsiXAxes);
        Assert.True(IsLabelsPaintVisible(bundle.RsiXAxes![0].LabelsPaint), "RsiX labels must be visible");
    }

    [Fact]
    public void Build_bubbles_only_top_3_percent_notional()
    {
        const int n = 100;
        var candles = SampleCandles(n, startClose: 50, lastClose: 60, lastVolume: 200_000).ToList();
        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            candles[i] = c with { Volume = 10_000 + i * 1_000 };
        }

        var markers = candles
            .Select(c => new TradeMarker(c.Time, c.Close, TradeMarkerSide.매수, "매수", SizeWeight: 2.0))
            .ToList();

        var bundle = ChartPresentationBuilder.Build(
            candles,
            markers,
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(58m, 48m, 70m),
            ChartTimeframe.분봉15);

        var buyScatter = Assert.IsType<ScatterSeries<LiveChartsCore.Defaults.WeightedPoint>>(
            bundle.PriceSeries.Single(s => s.Name == "대금↑"));
        var sellScatter = Assert.IsType<ScatterSeries<LiveChartsCore.Defaults.WeightedPoint>>(
            bundle.PriceSeries.Single(s => s.Name == "대금↓"));

        var buyCount = CountSeriesValues(buyScatter.Values);
        var sellCount = CountSeriesValues(sellScatter.Values);
        var maxAllowed = (int)Math.Ceiling(n * 0.03);

        Assert.True(
            buyCount + sellCount <= maxAllowed,
            $"bubble count {buyCount + sellCount} exceeds top-3% cap {maxAllowed}");

        Assert.True(buyScatter.GeometrySize <= 10, $"buy GeometrySize={buyScatter.GeometrySize}");
        Assert.True(sellScatter.GeometrySize <= 10, $"sell GeometrySize={sellScatter.GeometrySize}");
    }

    [Fact]
    public void Build_exposes_last_price_y_fraction_pinned_to_close()
    {
        var candles = SampleCandles(count: 40, startClose: 100, lastClose: 110, lastVolume: 1_000);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(109m, 105m, 118m),
            ChartTimeframe.분봉15);

        // Expected API: ChartBundle.LastPriceYFraction (missing → RuntimeBinderException = RED).
        // dynamic keeps the suite compiling so other contract tests can run.
        dynamic shaped = bundle;
        double fraction = (double)shaped.LastPriceYFraction;
        Assert.InRange(fraction, 0.02, 0.98);

        var yMin = bundle.PriceYAxes[0].MinLimit ?? 0;
        var yMax = bundle.PriceYAxes[0].MaxLimit ?? 1;
        var close = candles[^1].Close;
        var expected = (yMax - close) / (yMax - yMin);
        Assert.InRange(fraction, expected - 0.05, expected + 0.05);
    }

    [Fact]
    public void Build_volume_column_padding_is_tight_not_gappy()
    {
        var candles = SampleCandles(count: 40, startClose: 100, lastClose: 110, lastVolume: 1_000);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(109m, 105m, 118m),
            ChartTimeframe.분봉15);

        var volPlus = Assert.IsType<ColumnSeries<LiveChartsCore.Defaults.DateTimePoint>>(
            bundle.VolumeSeries.Single(s => s.Name == "Vol+"));
        var volMinus = Assert.IsType<ColumnSeries<LiveChartsCore.Defaults.DateTimePoint>>(
            bundle.VolumeSeries.Single(s => s.Name == "Vol-"));

        Assert.True(volPlus.Padding <= 0.12, $"Vol+ Padding={volPlus.Padding}");
        Assert.True(volMinus.Padding <= 0.12, $"Vol- Padding={volMinus.Padding}");
    }

    [Fact]
    public void Build_candle_stroke_thickness_at_least_1_2()
    {
        var candles = SampleCandles(count: 40, startClose: 100, lastClose: 110, lastVolume: 1_000);
        var bundle = ChartPresentationBuilder.Build(
            candles,
            Array.Empty<TradeMarker>(),
            Array.Empty<ChartIndicatorLine>(),
            SampleBracket(109m, 105m, 118m),
            ChartTimeframe.분봉15);

        var candle = Assert.IsType<CandlesticksSeries<LiveChartsCore.Defaults.FinancialPoint>>(
            bundle.PriceSeries.Single(s => s.Name == "SPCX"));
        var upStroke = Assert.IsType<SolidColorPaint>(candle.UpStroke);
        Assert.True(upStroke.StrokeThickness >= 1.2f, $"UpStroke.StrokeThickness={upStroke.StrokeThickness}");
    }

    [Fact]
    public void MainWindow_axaml_uses_star_pane_ratios_not_fixed_80_64_and_collapses_duplicate_price_tag()
    {
        var axamlPath = ResolveMainWindowAxamlPath();
        Assert.True(File.Exists(axamlPath), $"MainWindow.axaml not found at {axamlPath}");
        var text = File.ReadAllText(axamlPath);

        Assert.DoesNotContain("Height=\"80\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Height=\"64\"", text, StringComparison.Ordinal);
        Assert.True(
            text.Contains("7*", StringComparison.Ordinal)
            || text.Contains("*,", StringComparison.Ordinal)
            || System.Text.RegularExpressions.Regex.IsMatch(text, @"RowDefinitions=""[^""]*\*"),
            "expected star-based RowDefinitions for chart panes");

        Assert.DoesNotContain("Classes=\"price-tag\"", text, StringComparison.Ordinal);
    }

    private static bool IsLabelsPaintHidden(object? paint)
    {
        if (paint is null)
        {
            return true;
        }

        if (paint is SolidColorPaint solid)
        {
            return solid.Color.Alpha == 0;
        }

        return false;
    }

    private static bool IsLabelsPaintVisible(object? paint)
    {
        if (paint is not SolidColorPaint solid)
        {
            return false;
        }

        return solid.Color.Alpha > 0;
    }

    private static int CountSeriesValues(object? values)
    {
        if (values is null)
        {
            return 0;
        }

        if (values is System.Collections.ICollection coll)
        {
            return coll.Count;
        }

        if (values is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object?>().Count();
        }

        return 0;
    }

    private static string ResolveMainWindowAxamlPath()
    {
        var axamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "TradingBot.App", "Views", "MainWindow.axaml"));
        if (File.Exists(axamlPath))
        {
            return axamlPath;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !File.Exists(Path.Combine(dir.FullName, "src", "TradingBot.App", "Views", "MainWindow.axaml")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? axamlPath
            : Path.Combine(dir.FullName, "src", "TradingBot.App", "Views", "MainWindow.axaml");
    }

    private static IReadOnlyList<CandlePoint> SampleCandles(
        int count,
        double startClose,
        double lastClose,
        double lastVolume)
    {
        var list = new List<CandlePoint>(count);
        var t0 = new DateTimeOffset(2026, 7, 1, 14, 30, 0, TimeSpan.Zero);
        for (var i = 0; i < count; i++)
        {
            var frac = count == 1 ? 1.0 : (double)i / (count - 1);
            var c = startClose + (lastClose - startClose) * frac;
            var o = c - 0.2;
            var h = c + 0.5;
            var l = c - 0.5;
            var vol = i == count - 1 ? lastVolume : 50_000 + i * 100;
            list.Add(new CandlePoint(t0.AddMinutes(i * 15), o, h, l, c, vol));
        }

        return list;
    }

    private static TradeBracketPlan SampleBracket(decimal entry, decimal stop, decimal tp) =>
        new(
            Symbol: "SPCX",
            Side: "BUY",
            OrderType: "LIMIT",
            EntryLimit: entry,
            StopPrice: stop,
            TakeProfitPrice: tp,
            Quantity: 1m,
            StopDistancePerShare: entry - stop,
            RiskAmount: entry - stop,
            RewardAmount: tp - entry,
            RewardRiskRatio: 2m,
            Notional: entry,
            Atr: 0.5m,
            StopSource: BracketStopSource.Atr,
            IsValid: true,
            OwnerMessage: "test bracket",
            EstimatedCommissionUsd: 0.1m);
}
