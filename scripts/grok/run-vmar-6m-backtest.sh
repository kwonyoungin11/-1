#!/usr/bin/env bash
# Offline VMAR ~6m 1m CERS multi-strategy backtest host.
# Simulation only — never places live orders. Not investment advice.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

SYMBOL="${SYMBOL:-VMAR}"
INTERVAL="${INTERVAL:-1m}"
TARGET_BARS="${TARGET_BARS:-40000}"
MAX_PAGES="${MAX_PAGES:-300}"
CACHE_ONLY="${CACHE_ONLY:-0}"
REPORT_STEM="${REPORT_STEM:-$ROOT/artifacts/vmar_cers_6m_backtest_report}"
CACHE_PATH="${CACHE_PATH:-$ROOT/artifacts/candles/${SYMBOL}_1m.json}"

echo "== VMAR CERS 6m backtest host =="
echo "cwd: $ROOT"
echo "symbol=$SYMBOL interval=$INTERVAL target_bars=$TARGET_BARS"
echo "cache: $CACHE_PATH"
echo "report stem: $REPORT_STEM"
echo "live orders: blocked (this script never unlocks them)"

mkdir -p "$ROOT/artifacts/candles"

ARGS=(
  backtest
  --symbol "$SYMBOL"
  --interval "$INTERVAL"
  --target-bars "$TARGET_BARS"
  --max-pages "$MAX_PAGES"
  --cache-path "$CACHE_PATH"
  --report-stem "$REPORT_STEM"
)

if [[ "$CACHE_ONLY" == "1" || "$CACHE_ONLY" == "true" ]]; then
  ARGS+=(--cache-only)
fi

# Prefer Release for long runs; fall back to default configuration.
dotnet run --project "$ROOT/src/TradingBot.Runner" -c Release --no-launch-profile -- "${ARGS[@]}"

echo ""
echo "Reports:"
echo "  ${REPORT_STEM}.md"
echo "  ${REPORT_STEM}.json"
echo "Candle cache:"
echo "  $CACHE_PATH"
