# Premium chart design (shipped Supercharts)

Source of truth: `MainWindow.axaml` + `ChartPresentationBuilder` (wave-base rebuild).  
Light TradingView-style multi-pane. **Not** dual-Y via `ScalesYAt`. **Not** dark theme.

## Layout

```text
Grid RowDefinitions="Auto,Auto,7*,1.5*,1.5*"
```

| Row | Ratio | Content |
|-----|-------|---------|
| 0–1 | Auto | Title, last close, change%, TF chips, OHLCV status |
| 2 | `7*` ≈ **70%** | Price: candles, overlays, bubbles, last-price badge |
| 3 | `1.5*` ≈ **15%** | Volume columns + Vol SMA20 |
| 4 | `1.5*` ≈ **15%** | RSI14 + 30/70 guides + oversold/overbought bands |

Three separate LiveCharts2 `CartesianChart` controls (price / volume / RSI). Shared time via `LinkTimeAxes`, not one chart with dual Y scales.

## Margins & axes

- `rightM = 72` on **all** panes (`Margin(left, …, 72, …)`) so Y labels and last-price badge align.
- X labels: **RSI bottom only** (`MakeTimeAxis(showLabels: true)` on RSI; price/volume `false`).
- All axis times: **KST (UTC+9)** via `KoreaTime`.
- `LinkTimeAxes(priceX, volumeX, rsiX)`: bidirectional Min/Max zoom sync, re-entrancy safe.
- Per chart: `ZoomMode=X`, `FindingStrategy=CompareOnlyX`, `AnimationsSpeed=0:0:0`.

## Price pane overlays

| Series | Behavior |
|--------|----------|
| Candles | Up `#089981` / Down `#F23645` |
| Last | Horizontal **dashed** line only (direction color). **No `LastAccent`.** |
| ENTRY / SL / TP | Dashed level lines when bracket prices &gt; 0 |
| 추세추종 indicators | Domain may emit SMA20/SMA60 + EMA9/EMA21; builder **filters to EMA9+EMA21 only** when any EMA is present (SMA dropped to reduce clutter) |
| Bubbles | Top **3%** notional (`Volume * Close`); fill alpha `0x18` (low) |

## Last price badge

- `ChartBundle.LastPriceYFraction` + `LastCloseValue` / `LastCloseX`.
- View pins badge with `ScaleDataToPixels` when possible; falls back to `LastPriceYFraction * height`.
- Right-aligned overlay on price chart (`LastPriceBadge`).

## Change%

- Vs **previous bar close** (`candles[^2]`), not window-first bar.
- Same basis as OHLC status line.

## Visual tokens (light)

| Token | Value |
|-------|-------|
| Chart bg | `#FFFFFF` |
| Up / Down | `#089981` / `#F23645` |
| Entry | `#2962FF` |
| Grid / Axis | `#E6EAF2` / `#787B86` |
| RSI line | `#7E57C2` |

## Explicit non-claims

- No dark theme default.
- No `ScalesYAt` dual-Y single chart myth; panes are **stacked CartesianCharts**.
- No `LastAccent` series (tests assert absence).

## Key files

- `src/TradingBot.App/Views/MainWindow.axaml` — grid `7*,1.5*,1.5*`, triple chart, zoom/anim
- `src/TradingBot.App/ViewModels/ChartPresentationBuilder.cs` — series, margins, KST, LinkTimeAxes
- `src/TradingBot.App/Views/MainWindow.axaml.cs` — badge pin via `ScaleDataToPixels`
