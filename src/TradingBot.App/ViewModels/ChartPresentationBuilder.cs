using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using TradingBot.Domain;

namespace TradingBot.App.ViewModels;

/// <summary>
/// Premium TradingView-light chart builder: price + volume + RSI panes, KST axes, crosshair, sections.
/// </summary>
public static class ChartPresentationBuilder
{
    public static readonly SKColor TvUp = SKColor.Parse("#089981");
    public static readonly SKColor TvDown = SKColor.Parse("#F23645");
    public static readonly SKColor TvGrid = SKColor.Parse("#E6EAF2");
    public static readonly SKColor TvAxis = SKColor.Parse("#787B86");
    public static readonly SKColor TvEntry = SKColor.Parse("#2962FF");
    public static readonly SKColor TvBg = SKColor.Parse("#FFFFFF");
    public static readonly SKColor Crosshair = SKColor.Parse("#9598A1");
    public static readonly SKColor TvRsi = SKColor.Parse("#7E57C2");

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
        string IndicatorLegend,
        string LastCloseText = "",
        string ChangeText = "",
        bool ChangeIsPositive = true,
        string BarCountText = "",
        string LastBarTimeText = "",
        string HighLowText = "",
        string OpenText = "",
        string VolumeText = "",
        string StatusLineText = "",
        string LastPriceTag = "",
        bool LastPriceIsUp = true,
        string WatermarkText = "",
        ISeries[]? RsiSeries = null,
        Axis[]? RsiXAxes = null,
        Axis[]? RsiYAxes = null,
        DrawMarginFrame? RsiFrame = null,
        Margin? RsiMargin = null,
        string RsiStatusText = "",
        RectangularSection[]? RsiSections = null,
        string LastPriceAxisBadge = "",
        bool LastPriceAxisBadgeIsUp = true,
        double LastPriceYFraction = 0,
        double LastPriceYMin = 0,
        double LastPriceYMax = 0,
        double LastCloseValue = 0);

    public static ChartBundle Build(
        IReadOnlyList<CandlePoint> candles,
        IReadOnlyList<TradeMarker> markers,
        IReadOnlyList<ChartIndicatorLine> indicators,
        TradeBracketPlan bracket,
        ChartTimeframe timeframe,
        string? dataSourceWatermark = null)
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
        // Density: typical 40–120 bar windows keep body width in [8, 14] (TV-like).
        var maxBarWidth = candles.Count switch
        {
            > 300 => 3.0,
            > 220 => 4.5,
            > 160 => 6.0,
            > 120 => 7.5,
            > 90 => 9.0,
            > 60 => 11.0,
            > 40 => 12.5,
            _ => 13.5,
        };

        // Store wall-clock as KST DateTime so axes/tooltips read in Korean time.
        var financial = candles
            .Select(c =>
            {
                var k = KoreaTime.ToKstDateTime(c.Time);
                return new FinancialPoint(k, c.High, c.Open, c.Close, c.Low);
            })
            .ToArray();

        // Bubbles: only top 3% notional to reduce clutter
        var notionals = candles.Select(c => c.Volume * c.Close).ToArray();
        var topBubbleCount = notionals.Length == 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(notionals.Length * 0.03));
        var threshold = notionals.Length == 0
            ? 0
            : notionals.OrderByDescending(v => v).Skip(topBubbleCount - 1).FirstOrDefault();

        var buyBubbles = BuildBubbles(markers, candles, TradeMarkerSide.매수, threshold);
        var sellBubbles = BuildBubbles(markers, candles, TradeMarkerSide.매도, threshold);

        Func<ChartPoint, string> xTipFin = p =>
        {
            try
            {
                var x = p.Coordinate.SecondaryValue;
                if (x <= 0)
                {
                    x = p.Coordinate.PrimaryValue;
                }

                return KoreaTime.FormatAxisFromTicks((long)x, includeDate) + " KST";
            }
            catch
            {
                return string.Empty;
            }
        };

        var upStrokeColor = Darken(TvUp, 0.82f);
        var downStrokeColor = Darken(TvDown, 0.82f);
        var seriesName = string.IsNullOrWhiteSpace(bracket.Symbol)
            ? WatchlistCatalog.PrimarySymbol
            : bracket.Symbol;
        var candleSeries = new CandlesticksSeries<FinancialPoint>
        {
            Name = seriesName,
            Values = financial,
            UpFill = new SolidColorPaint(TvUp),
            UpStroke = new SolidColorPaint(upStrokeColor) { StrokeThickness = 1.25f },
            DownFill = new SolidColorPaint(TvDown),
            DownStroke = new SolidColorPaint(downStrokeColor) { StrokeThickness = 1.25f },
            MaxBarWidth = maxBarWidth,
            ZIndex = 2,
            AnimationsSpeed = TimeSpan.Zero,
            XToolTipLabelFormatter = xTipFin,
            YToolTipLabelFormatter = p =>
            {
                if (p.Context.DataSource is FinancialPoint fp)
                {
                    return $"O {fp.Open:N2}  H {fp.High:N2}  L {fp.Low:N2}  C {fp.Close:N2}";
                }

                return p.Coordinate.PrimaryValue.ToString("N2");
            },
        };

        var series = new List<ISeries> { candleSeries };

        // When any EMA overlay is present, draw EMA9 + EMA21 (drop SMA clutter).
        // Keep CERS / CERS edge so cost-aware edge can still show with EMA21.
        var hasEma = indicators.Any(i =>
            i.Name.StartsWith("EMA", StringComparison.OrdinalIgnoreCase));
        var drawnIndicators = hasEma
            ? indicators
                .Where(i =>
                    i.Name.Equals("EMA9", StringComparison.OrdinalIgnoreCase)
                    || i.Name.Equals("EMA21", StringComparison.OrdinalIgnoreCase)
                    || i.Name.Equals("CERS", StringComparison.OrdinalIgnoreCase)
                    || i.Name.Equals("CERS edge", StringComparison.OrdinalIgnoreCase))
                .ToList()
            : indicators.ToList();

        var legend = new List<string>();
        for (var i = 0; i < drawnIndicators.Count; i++)
        {
            var line = drawnIndicators[i];
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
                Stroke = new SolidColorPaint(color) { StrokeThickness = 1.0f },
                ZIndex = 4 + i,
                XToolTipLabelFormatter = p => FormatPointTime(p, includeDate),
                YToolTipLabelFormatter = p => $"{line.Name} {p.Coordinate.PrimaryValue:N2}",
            });
            legend.Add(line.Name);
        }

        // Last price dashed line — TV direction color (up green / down red)
        var lastClose = candles[^1].Close;
        var prevClose = candles.Count > 1 ? candles[^2].Close : candles[0].Open;
        var lastPriceIsUp = lastClose >= prevClose;
        var lastLineColor = lastPriceIsUp ? TvUp : TvDown;
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
            Stroke = new SolidColorPaint(lastLineColor)
            {
                StrokeThickness = 1,
                PathEffect = new DashEffect([4, 4]),
            },
            ZIndex = 3,
            IsVisibleAtLegend = false,
            IsHoverable = false,
            AnimationsSpeed = TimeSpan.Zero,
            XToolTipLabelFormatter = _ => string.Empty,
            YToolTipLabelFormatter = _ => string.Empty,
        });

        if (bracket.EntryLimit > 0m)
        {
            series.Add(LevelLine("ENTRY", (double)bracket.EntryLimit, t0, t1, TvEntry, dash: true, includeDate));
            legend.Add("ENTRY");
        }

        if (bracket.StopPrice > 0m)
        {
            series.Add(LevelLine("SL", (double)bracket.StopPrice, t0, t1, TvDown, dash: true, includeDate));
            legend.Add("SL");
        }

        if (bracket.TakeProfitPrice > 0m)
        {
            series.Add(LevelLine("TP", (double)bracket.TakeProfitPrice, t0, t1, TvUp, dash: true, includeDate));
            legend.Add("TP");
        }

        // Bubbles: visual volume-notional markers; not in find-set (avoids bogus crosshair Y)
        series.Add(new ScatterSeries<WeightedPoint>
        {
            Name = "대금↑",
            Values = buyBubbles,
            MinGeometrySize = 3,
            GeometrySize = 10,
            Fill = new SolidColorPaint(new SKColor(0x08, 0x99, 0x81, 0x18)),
            Stroke = null,
            ZIndex = 8,
            IsHoverable = false,
            AnimationsSpeed = TimeSpan.Zero,
            XToolTipLabelFormatter = _ => string.Empty,
            YToolTipLabelFormatter = _ => string.Empty,
        });
        series.Add(new ScatterSeries<WeightedPoint>
        {
            Name = "대금↓",
            Values = sellBubbles,
            MinGeometrySize = 3,
            GeometrySize = 10,
            Fill = new SolidColorPaint(new SKColor(0xF2, 0x36, 0x45, 0x18)),
            Stroke = null,
            ZIndex = 8,
            IsHoverable = false,
            AnimationsSpeed = TimeSpan.Zero,
            XToolTipLabelFormatter = _ => string.Empty,
            YToolTipLabelFormatter = _ => string.Empty,
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

        var volSmaPts = BuildVolumeSma(candles, period: 20);

        var volSeriesList = new List<ISeries>
        {
            new ColumnSeries<DateTimePoint>
            {
                Name = "Vol+",
                Values = volUp,
                Fill = new SolidColorPaint(new SKColor(0x08, 0x99, 0x81, 0x90)),
                Stroke = null,
                MaxBarWidth = maxBarWidth,
                Padding = 0.08,
                XToolTipLabelFormatter = p => FormatPointTime(p, includeDate),
                YToolTipLabelFormatter = p => $"Vol {FormatVolume(p.Coordinate.PrimaryValue)}",
                AnimationsSpeed = TimeSpan.Zero,
            },
            new ColumnSeries<DateTimePoint>
            {
                Name = "Vol-",
                Values = volDown,
                Fill = new SolidColorPaint(new SKColor(0xF2, 0x36, 0x45, 0x90)),
                Stroke = null,
                MaxBarWidth = maxBarWidth,
                Padding = 0.08,
                XToolTipLabelFormatter = p => FormatPointTime(p, includeDate),
                YToolTipLabelFormatter = p => $"Vol {FormatVolume(p.Coordinate.PrimaryValue)}",
                AnimationsSpeed = TimeSpan.Zero,
            },
        };
        if (volSmaPts.Count > 0)
        {
            volSeriesList.Add(new LineSeries<DateTimePoint>
            {
                Name = "Vol SMA20",
                Values = volSmaPts,
                GeometrySize = 0,
                LineSmoothness = 0,
                Fill = null,
                Stroke = new SolidColorPaint(SKColor.Parse("#F59E0B")) { StrokeThickness = 1.2f },
                ZIndex = 5,
                AnimationsSpeed = TimeSpan.Zero,
                XToolTipLabelFormatter = p => FormatPointTime(p, includeDate),
                YToolTipLabelFormatter = p => $"SMA20 {FormatVolume(p.Coordinate.PrimaryValue)}",
            });
        }

        var volSeries = volSeriesList.ToArray();

        // RSI pane (Wilder 14) — separate scale 0–100
        var closes = candles.Select(c => c.Close).ToArray();
        var rsiValues = ChartIndicatorCalculator.Rsi(closes, 14);
        var rsiPts = new List<DateTimePoint>();
        double? lastRsi = null;
        for (var i = 0; i < candles.Count; i++)
        {
            if (i < rsiValues.Count && rsiValues[i] is double rv && !double.IsNaN(rv))
            {
                rsiPts.Add(new DateTimePoint(KoreaTime.ToKstDateTime(candles[i].Time), rv));
                lastRsi = rv;
            }
        }

        var rsiSeriesList = new List<ISeries>();
        if (rsiPts.Count > 0)
        {
            rsiSeriesList.Add(new LineSeries<DateTimePoint>
            {
                Name = "RSI14",
                Values = rsiPts,
                GeometrySize = 0,
                LineSmoothness = 0,
                Fill = null,
                Stroke = new SolidColorPaint(TvRsi) { StrokeThickness = 1.5f },
                ZIndex = 4,
                AnimationsSpeed = TimeSpan.Zero,
                XToolTipLabelFormatter = p => FormatPointTime(p, includeDate),
                YToolTipLabelFormatter = p => $"RSI {p.Coordinate.PrimaryValue:N1}",
            });
        }

        // Dashed guide lines at 30 / 70
        rsiSeriesList.Add(new LineSeries<DateTimePoint>
        {
            Name = "RSI30",
            Values = new[]
            {
                new DateTimePoint(t0, 30),
                new DateTimePoint(t1, 30),
            },
            GeometrySize = 0,
            Fill = null,
            Stroke = new SolidColorPaint(new SKColor(0xF2, 0x36, 0x45, 0x90))
            {
                StrokeThickness = 1,
                PathEffect = new DashEffect([4, 4]),
            },
            ZIndex = 2,
            IsVisibleAtLegend = false,
            IsHoverable = false,
            AnimationsSpeed = TimeSpan.Zero,
            XToolTipLabelFormatter = _ => string.Empty,
            YToolTipLabelFormatter = _ => string.Empty,
        });
        rsiSeriesList.Add(new LineSeries<DateTimePoint>
        {
            Name = "RSI70",
            Values = new[]
            {
                new DateTimePoint(t0, 70),
                new DateTimePoint(t1, 70),
            },
            GeometrySize = 0,
            Fill = null,
            Stroke = new SolidColorPaint(new SKColor(0x08, 0x99, 0x81, 0x90))
            {
                StrokeThickness = 1,
                PathEffect = new DashEffect([4, 4]),
            },
            ZIndex = 2,
            IsVisibleAtLegend = false,
            IsHoverable = false,
            AnimationsSpeed = TimeSpan.Zero,
            XToolTipLabelFormatter = _ => string.Empty,
            YToolTipLabelFormatter = _ => string.Empty,
        });

        var rsiSeries = rsiSeriesList.ToArray();
        var rsiSections = new RectangularSection[]
        {
            new()
            {
                Yi = 0,
                Yj = 30,
                Fill = new SolidColorPaint(new SKColor(0xF2, 0x36, 0x45, 0x12)),
            },
            new()
            {
                Yi = 70,
                Yj = 100,
                Fill = new SolidColorPaint(new SKColor(0x08, 0x99, 0x81, 0x12)),
            },
        };
        var rsiStatusText = lastRsi is double lr
            ? $"RSI14 {lr:N1}"
            : "RSI14 —";

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

        // Triple-pane time axes: same scale + bidirectional Min/Max sync so zoom/pan stay locked.
        Axis MakeTimeAxis(bool showLabels) => new()
        {
            LabelsPaint = showLabels
                ? new SolidColorPaint(TvAxis)
                : new SolidColorPaint(SKColors.Transparent),
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
            CrosshairPadding = new LiveChartsCore.Drawing.Padding(4, 2),
            AnimationsSpeed = TimeSpan.Zero,
        };

        // Time labels only on bottom RSI pane (volume sits mid-stack).
        var priceX = MakeTimeAxis(showLabels: false);
        var volumeX = MakeTimeAxis(showLabels: false);
        var rsiX = MakeTimeAxis(showLabels: true);
        LinkTimeAxes(priceX, volumeX, rsiX);

        var yMin = priceMin - pad;
        var yMax = priceMax + pad;
        if (yMax <= yMin)
        {
            yMax = yMin + 1;
        }

        var priceStep = NicePriceStep(yMax - yMin, targetTicks: 4);
        var priceSeparators = CapSeparators(BuildEvenSeparators(yMin, yMax, priceStep), maxCount: 5);

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
            MinLimit = yMin,
            MaxLimit = yMax,
            MinStep = priceStep,
            ForceStepToMin = true,
            CustomSeparators = priceSeparators,
            LabelsDensity = 0,
            CrosshairPaint = crossPaint,
            CrosshairLabelsPaint = crossLabelPaint,
            CrosshairLabelsBackground = crossLabelBg,
            CrosshairSnapEnabled = true,
            CrosshairPadding = new LiveChartsCore.Drawing.Padding(6, 4),
            AnimationsSpeed = TimeSpan.Zero,
        };

        var volCeil = volMax * 1.12;
        var volStep = NicePriceStep(volCeil, targetTicks: 3);
        var volY = new Axis
        {
            Position = AxisPosition.End,
            LabelsPaint = new SolidColorPaint(TvAxis),
            SeparatorsPaint = new SolidColorPaint(SKColors.Transparent),
            ShowSeparatorLines = false,
            TextSize = 9,
            Labeler = FormatVolume,
            MinLimit = 0,
            MaxLimit = volCeil,
            MinStep = volStep,
            ForceStepToMin = true,
            CustomSeparators = CapSeparators(BuildEvenSeparators(0, volCeil, volStep), maxCount: 4),
            LabelsDensity = 0,
            CrosshairPaint = null,
            CrosshairLabelsPaint = null,
            IsVisible = true,
            AnimationsSpeed = TimeSpan.Zero,
        };

        var rsiY = new Axis
        {
            Position = AxisPosition.End,
            LabelsPaint = new SolidColorPaint(TvAxis),
            SeparatorsPaint = new SolidColorPaint(TvGrid)
            {
                StrokeThickness = 1,
                PathEffect = new DashEffect([3, 4]),
            },
            ShowSeparatorLines = true,
            TextSize = 9,
            Labeler = v => v.ToString("N0"),
            MinLimit = 0,
            MaxLimit = 100,
            MinStep = 50,
            ForceStepToMin = true,
            CustomSeparators = new double[] { 0, 30, 70, 100 },
            LabelsDensity = 0,
            CrosshairPaint = null,
            CrosshairLabelsPaint = null,
            IsVisible = true,
            AnimationsSpeed = TimeSpan.Zero,
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
        var prevOrFirst = candles.Count > 1 ? candles[^2] : first;
        var sessionHigh = candles.Max(c => c.High);
        var sessionLow = candles.Min(c => c.Low);
        // Change vs previous bar (same basis as FormatOhlcStatus), not window first.
        var chgBase = prevOrFirst.Close;
        var chg = chgBase > 0 ? (last.Close - chgBase) / chgBase * 100.0 : 0;
        var lastKst = KoreaTime.FormatAxis(last.Time, includeDate: true);
        var lastCloseText = last.Close.ToString("N2");
        var lastPriceTag = lastCloseText;
        var changeText = chg.ToString("+0.00;-0.00;0.00") + "%";
        var barCountText = $"{candles.Count}봉";
        var highLowText = $"H {sessionHigh:N2}  L {sessionLow:N2}";
        var openText = last.Open.ToString("N2");
        var volumeText = FormatVolume(last.Volume);
        var statusLine = FormatOhlcStatus(last, prevOrFirst);
        var lastLabel = statusLine;

        // Avalonia Y: 0=top, 1=bottom. Pin last-price badge to close within padded scale.
        var ySpan = yMax - yMin;
        var lastPriceYFraction = ySpan > 0
            ? Math.Clamp((yMax - last.Close) / ySpan, 0.02, 0.98)
            : 0.5;

        var watermark = string.IsNullOrWhiteSpace(dataSourceWatermark)
            ? string.Empty
            : dataSourceWatermark.Trim();
        var legendText = legend.Count == 0
            ? "KST · zoom-sync"
            : string.Join(" · ", legend);
        if (watermark.Length > 0)
        {
            legendText = string.IsNullOrEmpty(legendText)
                ? watermark
                : legendText + " · " + watermark;
        }

        var frame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(TvBg),
            Stroke = new SolidColorPaint(TvGrid) { StrokeThickness = 1 },
        };

        const float leftM = 8f;
        const float rightM = 72f;

        return new ChartBundle(
            PriceSeries: series.ToArray(),
            VolumeSeries: volSeries,
            PriceXAxes: [priceX],
            PriceYAxes: [priceY],
            VolumeXAxes: [volumeX],
            VolumeYAxes: [volY],
            PriceSections: sections.ToArray(),
            PriceFrame: frame,
            VolumeFrame: frame,
            PriceMargin: new Margin(leftM, 6, rightM, 2),
            // Volume mid-pane: no X labels → tight bottom; RSI bottom holds time labels.
            VolumeMargin: new Margin(leftM, 2, rightM, 4),
            LastPriceLabel: lastLabel,
            IndicatorLegend: legendText,
            LastCloseText: lastCloseText,
            ChangeText: changeText,
            ChangeIsPositive: chg >= 0,
            BarCountText: barCountText,
            LastBarTimeText: lastKst + " KST",
            HighLowText: highLowText,
            OpenText: openText,
            VolumeText: volumeText,
            StatusLineText: statusLine,
            LastPriceTag: lastPriceTag,
            LastPriceIsUp: lastPriceIsUp,
            WatermarkText: watermark,
            RsiSeries: rsiSeries,
            RsiXAxes: [rsiX],
            RsiYAxes: [rsiY],
            RsiFrame: frame,
            RsiMargin: new Margin(leftM, 2, rightM, 14),
            RsiStatusText: rsiStatusText,
            RsiSections: rsiSections,
            LastPriceAxisBadge: lastPriceTag,
            LastPriceAxisBadgeIsUp: lastPriceIsUp,
            LastPriceYFraction: lastPriceYFraction,
            LastPriceYMin: yMin,
            LastPriceYMax: yMax,
            LastCloseValue: last.Close);
    }

    private static List<DateTimePoint> BuildVolumeSma(IReadOnlyList<CandlePoint> candles, int period)
    {
        var pts = new List<DateTimePoint>();
        if (candles.Count < period || period < 2)
        {
            return pts;
        }

        double sum = 0;
        for (var i = 0; i < candles.Count; i++)
        {
            sum += Math.Max(0, candles[i].Volume);
            if (i >= period)
            {
                sum -= Math.Max(0, candles[i - period].Volume);
            }

            if (i >= period - 1)
            {
                var avg = sum / period;
                pts.Add(new DateTimePoint(KoreaTime.ToKstDateTime(candles[i].Time), avg));
            }
        }

        return pts;
    }

    /// <summary>
    /// Bidirectional Min/Max sync across price / volume / RSI time axes (re-entrancy safe).
    /// </summary>
    private static void LinkTimeAxes(params Axis[] axes)
    {
        if (axes.Length < 2)
        {
            return;
        }

        var syncing = false;
        const double epsilon = 1e-9;

        void SyncFrom(Axis source)
        {
            if (syncing)
            {
                return;
            }

            var min = source.MinLimit;
            var max = source.MaxLimit;

            syncing = true;
            try
            {
                foreach (var target in axes)
                {
                    if (ReferenceEquals(target, source))
                    {
                        continue;
                    }

                    var minDiffers = !NullableDoubleEquals(min, target.MinLimit, epsilon);
                    var maxDiffers = !NullableDoubleEquals(max, target.MaxLimit, epsilon);
                    if (!minDiffers && !maxDiffers)
                    {
                        continue;
                    }

                    if (min is double minV && max is double maxV)
                    {
                        target.SetLimits(minV, maxV, notify: true);
                    }
                    else
                    {
                        if (minDiffers)
                        {
                            target.MinLimit = min;
                        }

                        if (maxDiffers)
                        {
                            target.MaxLimit = max;
                        }
                    }
                }
            }
            finally
            {
                syncing = false;
            }
        }

        foreach (var axis in axes)
        {
            axis.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(Axis.MinLimit) or nameof(Axis.MaxLimit))
                {
                    SyncFrom(axis);
                }
            };
        }
    }

    private static bool NullableDoubleEquals(double? x, double? y, double epsilon)
    {
        if (x is null && y is null)
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return Math.Abs(x.Value - y.Value) <= epsilon;
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
        LastPriceLabel: "차트 데이터 없음",
        IndicatorLegend: string.Empty,
        LastCloseText: "—",
        ChangeText: "—",
        ChangeIsPositive: true,
        BarCountText: "0봉",
        LastBarTimeText: string.Empty,
        HighLowText: string.Empty,
        OpenText: string.Empty,
        VolumeText: string.Empty,
        StatusLineText: "차트 데이터 없음 · 새로고침 또는 연결 확인",
        LastPriceTag: string.Empty,
        LastPriceIsUp: true,
        WatermarkText: string.Empty,
        RsiSeries: Array.Empty<ISeries>(),
        RsiXAxes: Array.Empty<Axis>(),
        RsiYAxes: Array.Empty<Axis>(),
        RsiFrame: new DrawMarginFrame(),
        RsiMargin: new Margin(10),
        RsiStatusText: "RSI14 —",
        RsiSections: Array.Empty<RectangularSection>(),
        LastPriceAxisBadge: string.Empty,
        LastPriceAxisBadgeIsUp: true,
        LastPriceYFraction: 0,
        LastPriceYMin: 0,
        LastPriceYMax: 0,
        LastCloseValue: 0);

    private static WeightedPoint[] BuildBubbles(
        IReadOnlyList<TradeMarker> markers,
        IReadOnlyList<CandlePoint> candles,
        TradeMarkerSide side,
        double threshold)
    {
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

                if (threshold > 0 && candles[i].Volume * candles[i].Close < threshold)
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
        bool dash,
        bool includeDate) =>
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
            IsHoverable = true,
            AnimationsSpeed = TimeSpan.Zero,
            XToolTipLabelFormatter = _ => string.Empty,
            YToolTipLabelFormatter = _ => $"{name} {price:N2}",
        };

    private static string FormatPointTime(ChartPoint p, bool includeDate)
    {
        try
        {
            var x = p.Coordinate.SecondaryValue;
            if (double.IsNaN(x) || x <= 0)
            {
                x = p.Coordinate.PrimaryValue;
            }

            if (x < TimeSpan.FromDays(365).Ticks)
            {
                return string.Empty;
            }

            return KoreaTime.FormatAxisFromTicks((long)x, includeDate) + " KST";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static SKColor Darken(SKColor color, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        return new SKColor(
            (byte)(color.Red * factor),
            (byte)(color.Green * factor),
            (byte)(color.Blue * factor),
            color.Alpha);
    }

    /// <summary>TradingView-style volume axis / status formatting (shipped helper).</summary>
    public static string FormatVolume(double v)
    {
        if (v >= 1_000_000_000)
        {
            return $"{v / 1_000_000_000:0.##}B";
        }

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

    public static string FormatOhlcStatus(CandlePoint last, CandlePoint? prevOrFirst)
    {
        ArgumentNullException.ThrowIfNull(last);
        var baseline = prevOrFirst ?? last;
        var chg = baseline.Close > 0
            ? (last.Close - baseline.Close) / baseline.Close * 100.0
            : 0;
        var changeText = chg.ToString("+0.00;-0.00;0.00") + "%";
        var volumeText = FormatVolume(last.Volume);
        return
            $"O {last.Open:N2}  H {last.High:N2}  L {last.Low:N2}  C {last.Close:N2}  " +
            $"{changeText}  Vol {volumeText}";
    }

    public static string ResolveHoverOhlcStatus(
        IReadOnlyList<CandlePoint> candles,
        DateTimeOffset hoverTime)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (candles.Count == 0)
        {
            return string.Empty;
        }

        var bestIdx = 0;
        var bestSeconds = double.MaxValue;
        for (var i = 0; i < candles.Count; i++)
        {
            var seconds = Math.Abs((candles[i].Time - hoverTime).TotalSeconds);
            if (seconds < bestSeconds)
            {
                bestSeconds = seconds;
                bestIdx = i;
            }
        }

        var maxSeconds = MaxHoverSnapSeconds(candles);
        if (bestSeconds > maxSeconds)
        {
            return string.Empty;
        }

        CandlePoint? prev = bestIdx > 0 ? candles[bestIdx - 1] : null;
        return FormatOhlcStatus(candles[bestIdx], prev);
    }

    public static (string Text, bool IsUp) FormatLastPriceAxisBadge(
        CandlePoint last,
        CandlePoint? previous)
    {
        ArgumentNullException.ThrowIfNull(last);
        var text = last.Close.ToString("N2");
        var baseline = previous?.Close ?? last.Open;
        var isUp = last.Close >= baseline;
        return (text, isUp);
    }

    private static double MaxHoverSnapSeconds(IReadOnlyList<CandlePoint> candles)
    {
        if (candles.Count < 2)
        {
            return TimeSpan.FromHours(12).TotalSeconds;
        }

        double sum = 0;
        var gaps = 0;
        for (var i = 1; i < candles.Count; i++)
        {
            var gap = Math.Abs((candles[i].Time - candles[i - 1].Time).TotalSeconds);
            if (gap <= 0)
            {
                continue;
            }

            sum += gap;
            gaps++;
        }

        if (gaps == 0)
        {
            return TimeSpan.FromHours(12).TotalSeconds;
        }

        return (sum / gaps) * 1.5;
    }

    public static double NicePriceStep(double range, int targetTicks = 6)
    {
        if (range <= 0 || double.IsNaN(range) || double.IsInfinity(range))
        {
            return 0.5;
        }

        targetTicks = Math.Clamp(targetTicks, 2, 12);
        var rough = range / targetTicks;
        if (rough <= 0)
        {
            return 0.5;
        }

        var exp = Math.Floor(Math.Log10(rough));
        var baseStep = Math.Pow(10, exp);
        var mantissa = rough / baseStep;
        double nice = mantissa switch
        {
            < 1.5 => 1,
            < 3.5 => 2,
            < 7.5 => 5,
            _ => 10,
        };
        return nice * baseStep;
    }

    public static double[] BuildEvenSeparators(double min, double max, double step)
    {
        if (step <= 0 || max < min)
        {
            return [min, max];
        }

        var start = Math.Floor(min / step) * step;
        if (start < min - step * 1e-9)
        {
            start += step;
        }

        var list = new List<double>(12);
        for (var v = start; v <= max + step * 1e-9 && list.Count < 24; v += step)
        {
            list.Add(Math.Round(v, 10));
        }

        if (list.Count == 0)
        {
            list.Add(min);
            list.Add(max);
        }

        return list.ToArray();
    }

    public static double[] CapSeparators(double[] separators, int maxCount)
    {
        ArgumentNullException.ThrowIfNull(separators);
        if (separators.Length <= maxCount || maxCount < 2)
        {
            return separators;
        }

        var result = new double[maxCount];
        result[0] = separators[0];
        result[^1] = separators[^1];
        var inner = maxCount - 2;
        for (var i = 1; i <= inner; i++)
        {
            var t = (double)i / (inner + 1);
            var idx = (int)Math.Round(t * (separators.Length - 1));
            idx = Math.Clamp(idx, 1, separators.Length - 2);
            result[i] = separators[idx];
        }

        return result;
    }
}
