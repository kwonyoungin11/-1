using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using TradingBot.Domain;

namespace TradingBot.App.ViewModels;

/// <summary>
/// Premium TradingView-light chart builder: dual pane, KST axes, crosshair, sections.
/// </summary>
public static class ChartPresentationBuilder
{
    public static readonly SKColor TvUp = SKColor.Parse("#089981");
    public static readonly SKColor TvDown = SKColor.Parse("#F23645");
    public static readonly SKColor TvGrid = SKColor.Parse("#F0F3FA");
    public static readonly SKColor TvAxis = SKColor.Parse("#787B86");
    public static readonly SKColor TvEntry = SKColor.Parse("#2962FF");
    public static readonly SKColor TvBg = SKColor.Parse("#FFFFFF");
    public static readonly SKColor Crosshair = SKColor.Parse("#9598A1");

    private static readonly SKColor[] IndicatorColors =
    [
        SKColor.Parse("#2962FF"),
        SKColor.Parse("#FF6D00"),
        SKColor.Parse("#9C27B0"),
        SKColor.Parse("#00897B"),
        SKColor.Parse("#E91E63"),
    ];

    public sealed record ChartBundle(
        ISeries[] PriceSeries,
        ISeries[] VolumeSeries,
        Axis[] PriceXAxes,
        Axis[] PriceYAxes,
        Axis[] VolumeXAxes,
        Axis[] VolumeYAxes,
        RectangularSection[] PriceSections,
        DrawMarginFrame PriceFrame,
        DrawMarginFrame VolumeFrame,
        Margin PriceMargin,
        Margin VolumeMargin,
        string LastPriceLabel,
        string IndicatorLegend);

    public static ChartBundle Build(
        IReadOnlyList<CandlePoint> candles,
        IReadOnlyList<TradeMarker> markers,
        IReadOnlyList<ChartIndicatorLine> indicators,
        TradeBracketPlan bracket,
        ChartTimeframe timeframe)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(markers);
        ArgumentNullException.ThrowIfNull(indicators);
        ArgumentNullException.ThrowIfNull(bracket);

        if (candles.Count == 0)
        {
            return Empty();
        }

        var barDuration = ChartTimeframeCatalog.BarDuration(timeframe);
        var includeDate = timeframe is ChartTimeframe.일봉 or ChartTimeframe.주봉
                          || barDuration.TotalHours >= 1;
        var maxBarWidth = candles.Count switch
        {
            > 140 => 3.5,
            > 100 => 5.0,
            > 70 => 7.0,
            > 40 => 9.0,
            _ => 12.0,
        };

        // Store wall-clock as KST DateTime so axes/tooltips read in Korean time.
        var financial = candles
            .Select(c =>
            {
                var k = KoreaTime.ToKstDateTime(c.Time);
                return new FinancialPoint(k, c.High, c.Open, c.Close, c.Low);
            })
            .ToArray();

        // Bubbles: only top volume quintile to reduce clutter
        var notionals = candles.Select(c => c.Volume * c.Close).ToArray();
        var threshold = notionals.Length == 0
            ? 0
            : notionals.OrderByDescending(v => v).Skip(Math.Max(0, notionals.Length / 5)).FirstOrDefault();
        var strongMarkers = markers
            .Where((_, i) => i < candles.Count && candles[i].Volume * candles[i].Close >= threshold * 0.99)
            .ToList();
        if (strongMarkers.Count == 0)
        {
            strongMarkers = markers.Take(Math.Min(12, markers.Count)).ToList();
        }

        // Prefer markers aligned by time if counts differ
        var buyBubbles = BuildBubbles(markers, candles, TradeMarkerSide.매수, threshold);
        var sellBubbles = BuildBubbles(markers, candles, TradeMarkerSide.매도, threshold);

        var series = new List<ISeries>
        {
            new CandlesticksSeries<FinancialPoint>
            {
                Name = "SPCX",
                Values = financial,
                UpFill = new SolidColorPaint(TvUp),
                UpStroke = new SolidColorPaint(TvUp) { StrokeThickness = 1 },
                DownFill = new SolidColorPaint(TvDown),
                DownStroke = new SolidColorPaint(TvDown) { StrokeThickness = 1 },
                MaxBarWidth = maxBarWidth,
                ZIndex = 2,
            },
        };

        var legend = new List<string>();
        for (var i = 0; i < indicators.Count; i++)
        {
            var line = indicators[i];
            var color = IndicatorColors[i % IndicatorColors.Length];
            var pts = new List<DateTimePoint>();
            for (var j = 0; j < candles.Count; j++)
            {
                var v = j < line.Values.Count ? line.Values[j] : null;
                if (v is double d && !double.IsNaN(d))
                {
                    pts.Add(new DateTimePoint(KoreaTime.ToKstDateTime(candles[j].Time), d));
                }
            }

            series.Add(new LineSeries<DateTimePoint>
            {
                Name = line.Name,
                Values = pts,
                GeometrySize = 0,
                LineSmoothness = 0,
                Fill = null,
                Stroke = new SolidColorPaint(color) { StrokeThickness = 1.5f },
                ZIndex = 4 + i,
            });
            legend.Add(line.Name);
        }

        // Last price dashed line
        var lastClose = candles[^1].Close;
        var t0 = KoreaTime.ToKstDateTime(candles[0].Time);
        var t1 = KoreaTime.ToKstDateTime(candles[^1].Time);
        series.Add(new LineSeries<DateTimePoint>
        {
            Name = "Last",
            Values = new[]
            {
                new DateTimePoint(t0, lastClose),
                new DateTimePoint(t1, lastClose),
            },
            GeometrySize = 0,
            Fill = null,
            Stroke = new SolidColorPaint(Crosshair)
            {
                StrokeThickness = 1,
                PathEffect = new DashEffect([4, 4]),
            },
            ZIndex = 3,
        });

        if (bracket.EntryLimit > 0m)
        {
            series.Add(LevelLine("ENTRY", (double)bracket.EntryLimit, t0, t1, TvEntry, dash: true));
            legend.Add("ENTRY");
        }

        if (bracket.StopPrice > 0m)
        {
            series.Add(LevelLine("SL", (double)bracket.StopPrice, t0, t1, TvDown, dash: true));
            legend.Add("SL");
        }

        if (bracket.TakeProfitPrice > 0m)
        {
            series.Add(LevelLine("TP", (double)bracket.TakeProfitPrice, t0, t1, TvUp, dash: true));
            legend.Add("TP");
        }

        series.Add(new ScatterSeries<WeightedPoint>
        {
            Name = "Vol↑",
            Values = buyBubbles,
            MinGeometrySize = 4,
            GeometrySize = 28,
            Fill = new SolidColorPaint(new SKColor(0x08, 0x99, 0x81, 0x40)),
            Stroke = null,
            ZIndex = 8,
        });
        series.Add(new ScatterSeries<WeightedPoint>
        {
            Name = "Vol↓",
            Values = sellBubbles,
            MinGeometrySize = 4,
            GeometrySize = 28,
            Fill = new SolidColorPaint(new SKColor(0xF2, 0x36, 0x45, 0x40)),
            Stroke = null,
            ZIndex = 8,
        });

        // Volume pane
        var volUp = new List<DateTimePoint>();
        var volDown = new List<DateTimePoint>();
        foreach (var c in candles)
        {
            var kt = KoreaTime.ToKstDateTime(c.Time);
            var pt = new DateTimePoint(kt, Math.Max(0, c.Volume));
            if (c.Close >= c.Open)
            {
                volUp.Add(pt);
            }
            else
            {
                volDown.Add(pt);
            }
        }

        var volSeries = new ISeries[]
        {
            new ColumnSeries<DateTimePoint>
            {
                Name = "Vol+",
                Values = volUp,
                Fill = new SolidColorPaint(new SKColor(0x08, 0x99, 0x81, 0x88)),
                Stroke = null,
                MaxBarWidth = maxBarWidth,
                Padding = 0.4,
            },
            new ColumnSeries<DateTimePoint>
            {
                Name = "Vol-",
                Values = volDown,
                Fill = new SolidColorPaint(new SKColor(0xF2, 0x36, 0x45, 0x88)),
                Stroke = null,
                MaxBarWidth = maxBarWidth,
                Padding = 0.4,
            },
        };

        var priceMin = candles.Min(c => c.Low);
        var priceMax = candles.Max(c => c.High);
        if (bracket.StopPrice > 0m)
        {
            priceMin = Math.Min(priceMin, (double)bracket.StopPrice);
        }

        if (bracket.TakeProfitPrice > 0m)
        {
            priceMax = Math.Max(priceMax, (double)bracket.TakeProfitPrice);
        }

        if (bracket.EntryLimit > 0m)
        {
            priceMin = Math.Min(priceMin, (double)bracket.EntryLimit);
            priceMax = Math.Max(priceMax, (double)bracket.EntryLimit);
        }

        var pad = Math.Max(0.4, (priceMax - priceMin) * 0.05);
        var volMax = Math.Max(1, candles.Max(c => c.Volume));

        var grid = new SolidColorPaint(TvGrid) { StrokeThickness = 1 };
        var crossPaint = new SolidColorPaint(Crosshair) { StrokeThickness = 1 };
        var crossLabelBg = new SKColor(0x29, 0x62, 0xFF, 0xE6).AsLvcColor();
        var crossLabelPaint = new SolidColorPaint(SKColors.White);

        Axis MakeX(bool showLabels) => new()
        {
            LabelsPaint = showLabels ? new SolidColorPaint(TvAxis) : new SolidColorPaint(SKColors.Transparent),
            SeparatorsPaint = grid,
            ShowSeparatorLines = true,
            TextSize = 10,
            UnitWidth = barDuration.Ticks,
            MinStep = Math.Max(barDuration.Ticks, TimeSpan.FromMinutes(1).Ticks),
            Labeler = value => value <= 0
                ? string.Empty
                : KoreaTime.FormatAxisFromTicks((long)value, includeDate),
            CrosshairPaint = crossPaint,
            CrosshairLabelsPaint = crossLabelPaint,
            CrosshairLabelsBackground = crossLabelBg,
            CrosshairSnapEnabled = true,
            CrosshairPadding = new LiveChartsCore.Drawing.Padding(6, 4),
        };

        var priceY = new Axis
        {
            Position = AxisPosition.End,
            LabelsPaint = new SolidColorPaint(TvAxis),
            SeparatorsPaint = new SolidColorPaint(TvGrid)
            {
                StrokeThickness = 1,
                PathEffect = new DashEffect([3, 4]),
            },
            ShowSeparatorLines = true,
            TextSize = 10,
            Labeler = v => v.ToString("N2"),
            MinLimit = priceMin - pad,
            MaxLimit = priceMax + pad,
            CrosshairPaint = crossPaint,
            CrosshairLabelsPaint = crossLabelPaint,
            CrosshairLabelsBackground = crossLabelBg,
            CrosshairSnapEnabled = true,
            CrosshairPadding = new LiveChartsCore.Drawing.Padding(6, 4),
        };

        var volY = new Axis
        {
            Position = AxisPosition.End,
            LabelsPaint = new SolidColorPaint(TvAxis),
            SeparatorsPaint = new SolidColorPaint(SKColors.Transparent),
            ShowSeparatorLines = false,
            TextSize = 9,
            Labeler = FormatVolume,
            MinLimit = 0,
            MaxLimit = volMax * 1.15,
        };

        // Sections: risk zone (stop→entry), reward zone (entry→tp)
        var sections = new List<RectangularSection>();
        if (bracket.IsValid && bracket.StopPrice > 0m && bracket.EntryLimit > 0m)
        {
            sections.Add(new RectangularSection
            {
                Yi = (double)Math.Min(bracket.StopPrice, bracket.EntryLimit),
                Yj = (double)Math.Max(bracket.StopPrice, bracket.EntryLimit),
                Fill = new SolidColorPaint(new SKColor(0xF2, 0x36, 0x45, 0x14)),
            });
        }

        if (bracket.IsValid && bracket.TakeProfitPrice > 0m && bracket.EntryLimit > 0m)
        {
            sections.Add(new RectangularSection
            {
                Yi = (double)Math.Min(bracket.TakeProfitPrice, bracket.EntryLimit),
                Yj = (double)Math.Max(bracket.TakeProfitPrice, bracket.EntryLimit),
                Fill = new SolidColorPaint(new SKColor(0x08, 0x99, 0x81, 0x14)),
            });
        }

        var last = candles[^1];
        var first = candles[0];
        var chg = first.Close > 0 ? (last.Close - first.Close) / first.Close * 100.0 : 0;
        var lastKst = KoreaTime.FormatAxis(last.Time, includeDate: true);
        var lastLabel =
            $"종가 {last.Close:N2} · 구간 {chg:+0.00;-0.00;0}% · {candles.Count}봉 · {lastKst} KST · " +
            $"E {bracket.EntryLimit:N2} / SL {bracket.StopPrice:N2} / TP {bracket.TakeProfitPrice:N2}";

        var legendText = legend.Count == 0
            ? "프리미엄 차트 · Crosshair · Sections · KST"
            : string.Join(" · ", legend) + " · KST · Crosshair";

        var frame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(TvBg),
            Stroke = new SolidColorPaint(TvGrid) { StrokeThickness = 1 },
        };

        return new ChartBundle(
            PriceSeries: series.ToArray(),
            VolumeSeries: volSeries,
            PriceXAxes: [MakeX(showLabels: false)],
            PriceYAxes: [priceY],
            VolumeXAxes: [MakeX(showLabels: true)],
            VolumeYAxes: [volY],
            PriceSections: sections.ToArray(),
            PriceFrame: frame,
            VolumeFrame: frame,
            PriceMargin: new Margin(8, 10, 52, 4),
            VolumeMargin: new Margin(8, 4, 52, 22),
            LastPriceLabel: lastLabel,
            IndicatorLegend: legendText);
    }

    private static ChartBundle Empty() => new(
        Array.Empty<ISeries>(),
        Array.Empty<ISeries>(),
        Array.Empty<Axis>(),
        Array.Empty<Axis>(),
        Array.Empty<Axis>(),
        Array.Empty<Axis>(),
        Array.Empty<RectangularSection>(),
        new DrawMarginFrame(),
        new DrawMarginFrame(),
        new Margin(10),
        new Margin(10),
        string.Empty,
        string.Empty);

    private static WeightedPoint[] BuildBubbles(
        IReadOnlyList<TradeMarker> markers,
        IReadOnlyList<CandlePoint> candles,
        TradeMarkerSide side,
        double threshold)
    {
        // Prefer candle-aligned volume filter
        var list = new List<WeightedPoint>();
        if (markers.Count == candles.Count)
        {
            for (var i = 0; i < candles.Count; i++)
            {
                var m = markers[i];
                if (m.Side != side)
                {
                    continue;
                }

                if (candles[i].Volume * candles[i].Close < threshold * 0.85 && threshold > 0)
                {
                    continue;
                }

                var k = KoreaTime.ToKstDateTime(m.Time);
                list.Add(new WeightedPoint(k.Ticks, m.Price, Math.Clamp(m.SizeWeight * 0.7, 0.3, 4.0)));
            }
        }
        else
        {
            foreach (var m in markers.Where(x => x.Side == side))
            {
                var k = KoreaTime.ToKstDateTime(m.Time);
                list.Add(new WeightedPoint(k.Ticks, m.Price, Math.Clamp(m.SizeWeight * 0.65, 0.3, 4.0)));
            }
        }

        return list.ToArray();
    }

    private static LineSeries<DateTimePoint> LevelLine(
        string name,
        double price,
        DateTime t0,
        DateTime t1,
        SKColor color,
        bool dash) =>
        new()
        {
            Name = name,
            Values = new[]
            {
                new DateTimePoint(t0, price),
                new DateTimePoint(t1, price),
            },
            GeometrySize = 0,
            Fill = null,
            Stroke = new SolidColorPaint(color)
            {
                StrokeThickness = 1.4f,
                PathEffect = dash ? new DashEffect([6, 4]) : null,
            },
            ZIndex = 12,
        };

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
