# Chart · TradingView parity + Toss constraints

## Data source of truth

**Toss OpenAPI** `GET /api/v1/candles` (v1.2.2):

| Field | Allowed |
|-------|---------|
| `interval` | **`1m`**, **`1d` only** |
| `count` | max **200** |
| pagination | `before` / `nextBefore` |

Unsupported intervals are **never** sent. Higher TFs are **client OHLCV aggregation**.

## UI timeframes (9)

| UI | Source | Aggregation |
|----|--------|-------------|
| 1m | Toss 1m | — |
| 5m, 10m, 15m, 30m, 60m, 240m | Toss 1m (paged) | `CandleAggregator` minute buckets |
| 1D | Toss 1d | — |
| 1W | Toss 1d (paged) | Week bucket = **Monday 00:00 UTC** |

OHLCV merge: O=first open, H=max high, L=min low, C=last close, V=sum volume.

## TradingView Lightweight Charts patterns (ported to LiveCharts2)

| TV LWC | Our LiveCharts2 |
|--------|-----------------|
| CandlestickSeries up `#26a69a` / down `#ef5350` | `CandlesticksSeries` same colors |
| Histogram volume by direction | `ColumnSeries` ScalesYAt=1 |
| Line indicators | `LineSeries` SMA overlays |
| Multi-pane price+volume | Dual Y-axis (or two charts fallback) |
| Dark bg | `#0B0F14` |
| Zoom X | `ZoomMode=X`, animations off |

**Not used:** TradingView proprietary Charting Library HTML embed (license + Avalonia host).

## Bubble overlay (product)

Size ∝ √(volume×close), green if close≥open else red (ChartFanatics).

## Expert prompt (implementation header)

See plan Phase 0 in session plan file — data contract + acceptance criteria.
