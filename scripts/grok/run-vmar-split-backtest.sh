#!/usr/bin/env bash
# VMAR split-buy/split-sell ladder grid backtest (simulation only).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
echo "== VMAR split ladder 6m grid backtest =="
echo "live orders: blocked · not investment advice"
dotnet run --project "$ROOT/src/TradingBot.Runner" -c Release --no-launch-profile -- \
  backtest-split --cache-only
echo "Report: artifacts/vmar_split_ladder_6m_backtest_report.md"
