# Premium chart design (KST + LiveCharts2 advanced)

## Time

- All axis labels and last-price timestamps: **Korea Standard Time (KST, UTC+9)**
- Helper: `KoreaTime` in Domain
- Candles plotted with KST wall-clock `DateTime` for LiveCharts axes

## Layout (TradingView-like)

| Pane | Ratio | Content |
|------|-------|---------|
| Price | ~72% | Candles, SMA, Last dashed, ENTRY/SL/TP dashed, light bubbles, Sections |
| Volume | ~28% | Green/red columns only, X labels KST |

## LiveCharts2 APIs used

- `Axis.CrosshairPaint` / `CrosshairSnapEnabled` / label paints
- `RectangularSection` (Yi/Yj) risk & reward tint bands
- Dual `CartesianChart` stack
- `TooltipFindingStrategy=CompareOnlyX`
- `AnimationsSpeed=0`

## Visual tokens

- Up `#089981` Down `#F23645` Entry `#2962FF` Grid `#F0F3FA` Axis `#787B86`
- Bubbles: top volume quintile only, low alpha

## Not used (roadmap)

- Avalonia Charts product full migrate
- TradingView Charting Library WebView
