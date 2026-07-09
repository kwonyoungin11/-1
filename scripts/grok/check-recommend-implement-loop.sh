#!/usr/bin/env bash
# Machine-readable recommend→implement loop stop condition.
#
# Purpose:
#   Tell agents whether the "recommend then implement" loop should STOP
#   because strategy scaffolding is solid and live readiness is at
#   ready_for_owner_unlock — while live remains blocked (fail-closed).
#
# Does NOT enable live orders. Does NOT replace safety gates.
#
# Machine tokens (always printed at end when safety path completes):
#   STRATEGY_SOLID=true|false
#   LIVE_READY=false (expected under defaults)
#   LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock|blocked_missing_evidence|broken_defaults|unknown
#   LIVE_SAFETY_INTACT=true|false
#   SAFETY_OK=true|false
#   PARALLEL_POLICY=pass|fail
#   LOOP_STOP=true|false
#
# LOOP_STOP=true only when ALL of:
#   STRATEGY_SOLID=true
#   LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock
#   LIVE_READY=false
#   LIVE_SAFETY_INTACT=true (and trading-safety + parallel policy ok)
#
# Exit codes:
#   0 — safety intact (LOOP_STOP may be true or false; use the token)
#   1 — safety BROKEN (trading-safety or live-readiness broken_defaults)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

echo "== recommend→implement loop stop check =="
echo "note: LOOP_STOP means stop coding loop; never means live unlock"

SAFETY_OK=true
PARALLEL_POLICY=fail
LIVE_READY=false
LIVE_OWNER_UNLOCK_STATUS=unknown
LIVE_SAFETY_INTACT=false
STRATEGY_SOLID=false
LOOP_STOP=false

# --- Gate: trading safety (blocking for SAFETY_OK) ---
echo ""
echo "--- check-trading-safety ---"
if bash ./scripts/grok/check-trading-safety.sh; then
  echo "ok: check-trading-safety"
else
  echo "FAIL: check-trading-safety"
  SAFETY_OK=false
fi

# --- Gate: live readiness (capture machine tokens) ---
echo ""
echo "--- check-live-readiness ---"
LIVE_OUT=""
LIVE_RC=0
set +e
LIVE_OUT="$(bash ./scripts/grok/check-live-readiness.sh 2>&1)"
LIVE_RC=$?
set -e
printf '%s\n' "$LIVE_OUT"

# Parse tokens from live-readiness output
if printf '%s\n' "$LIVE_OUT" | rg -q '^LIVE_READY=false$'; then
  LIVE_READY=false
elif printf '%s\n' "$LIVE_OUT" | rg -q '^LIVE_READY=true$'; then
  # Should never happen under project policy; treat as not-stop.
  LIVE_READY=true
else
  LIVE_READY=false
fi

if printf '%s\n' "$LIVE_OUT" | rg -q '^LIVE_SAFETY_INTACT=true$'; then
  LIVE_SAFETY_INTACT=true
elif printf '%s\n' "$LIVE_OUT" | rg -q '^LIVE_SAFETY_INTACT=false$'; then
  LIVE_SAFETY_INTACT=false
else
  # Infer from exit / unlock status when token missing
  if [[ "$LIVE_RC" -eq 0 ]]; then
    LIVE_SAFETY_INTACT=true
  else
    LIVE_SAFETY_INTACT=false
  fi
fi

if printf '%s\n' "$LIVE_OUT" | rg -q '^LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock$'; then
  LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock
elif printf '%s\n' "$LIVE_OUT" | rg -q '^LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence$'; then
  LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence
elif printf '%s\n' "$LIVE_OUT" | rg -q '^LIVE_OWNER_UNLOCK_STATUS=broken_defaults$'; then
  LIVE_OWNER_UNLOCK_STATUS=broken_defaults
else
  LIVE_OWNER_UNLOCK_STATUS=unknown
fi

if [[ "$LIVE_RC" -ne 0 ]] || [[ "$LIVE_SAFETY_INTACT" != "true" ]]; then
  SAFETY_OK=false
  LIVE_SAFETY_INTACT=false
fi

# --- Gate: parallel policy ---
echo ""
echo "--- check-parallel-policy ---"
if bash ./scripts/grok/check-parallel-policy.sh; then
  PARALLEL_POLICY=pass
  echo "ok: check-parallel-policy"
else
  PARALLEL_POLICY=fail
  SAFETY_OK=false
  echo "FAIL: check-parallel-policy"
fi

# --- STRATEGY_SOLID: file + type presence (no full test suite required) ---
echo ""
echo "--- strategy solid (file/type markers) ---"
solid_missing=0

check_file() {
  local path="$1"
  if [[ -f "$path" ]]; then
    echo "ok: file $path"
  else
    echo "missing: file $path"
    solid_missing=$((solid_missing + 1))
  fi
}

check_rg() {
  local label="$1"
  shift
  if rg -n --glob '*.cs' "$@" src >/dev/null 2>&1; then
    echo "ok: $label"
  else
    echo "missing: $label"
    solid_missing=$((solid_missing + 1))
  fi
}

# Required type source files
check_file "src/TradingBot.Domain/PositionRiskSizer.cs"
check_file "src/TradingBot.Risk/DailyLossGuard.cs"
check_file "src/TradingBot.Risk/TradingSessionWindow.cs"
check_file "src/TradingBot.Domain/Strategy/TrendFollowParameters.cs"
check_file "src/TradingBot.Application/Strategy/OrderCandidatePipeline.cs"
check_file "src/TradingBot.Domain/AutoTrade/StockMarketKind.cs"

# StockMarketKind contains 나스닥코어3
if rg -n '나스닥코어3' src/TradingBot.Domain/AutoTrade/StockMarketKind.cs >/dev/null 2>&1; then
  echo "ok: StockMarketKind contains 나스닥코어3"
else
  echo "missing: StockMarketKind 나스닥코어3"
  solid_missing=$((solid_missing + 1))
fi

# Type symbols present in tree
check_rg "class/record PositionRiskSizer" 'class PositionRiskSizer|static class PositionRiskSizer'
check_rg "DailyLossGuard type" 'class DailyLossGuard|static class DailyLossGuard'
check_rg "TradingSessionWindow type" 'record TradingSessionWindow|class TradingSessionWindow'
check_rg "TrendFollowParameters type" 'record TrendFollowParameters|class TrendFollowParameters'

# PracticeStrategyContext OR BuildCandidates practice param in OrderCandidatePipeline
PIPELINE="src/TradingBot.Application/Strategy/OrderCandidatePipeline.cs"
if rg -n 'PracticeStrategyContext' src --glob '*.cs' >/dev/null 2>&1; then
  echo "ok: PracticeStrategyContext present"
elif rg -n 'BuildCandidates' "$PIPELINE" >/dev/null 2>&1 \
  && rg -n '단순연습전략|TradingStrategyKind|practice' "$PIPELINE" >/dev/null 2>&1; then
  echo "ok: OrderCandidatePipeline.BuildCandidates practice param"
else
  echo "missing: PracticeStrategyContext or BuildCandidates practice param"
  solid_missing=$((solid_missing + 1))
fi

if [[ "$solid_missing" -eq 0 ]]; then
  STRATEGY_SOLID=true
  echo "strategy markers complete"
else
  STRATEGY_SOLID=false
  echo "strategy markers incomplete ($solid_missing)"
fi

# --- LOOP_STOP decision ---
if [[ "$STRATEGY_SOLID" == "true" ]] \
  && [[ "$LIVE_OWNER_UNLOCK_STATUS" == "ready_for_owner_unlock" ]] \
  && [[ "$LIVE_READY" == "false" ]] \
  && [[ "$LIVE_SAFETY_INTACT" == "true" ]] \
  && [[ "$SAFETY_OK" == "true" ]] \
  && [[ "$PARALLEL_POLICY" == "pass" ]]; then
  LOOP_STOP=true
else
  LOOP_STOP=false
fi

echo ""
echo "======== machine tokens ========"
echo "STRATEGY_SOLID=$STRATEGY_SOLID"
echo "LIVE_READY=$LIVE_READY"
echo "LIVE_OWNER_UNLOCK_STATUS=$LIVE_OWNER_UNLOCK_STATUS"
echo "LIVE_SAFETY_INTACT=$LIVE_SAFETY_INTACT"
echo "SAFETY_OK=$SAFETY_OK"
echo "PARALLEL_POLICY=$PARALLEL_POLICY"
echo "LOOP_STOP=$LOOP_STOP"

if [[ "$LOOP_STOP" == "true" ]]; then
  echo "recommend→implement loop: STOP (strategy solid + owner-unlock ready; live still blocked)"
else
  echo "recommend→implement loop: CONTINUE (or wait on evidence/strategy markers)"
fi

if [[ "$SAFETY_OK" != "true" ]]; then
  echo "loop stop check FAILED — safety broken (do not weaken gates)"
  exit 1
fi

# Exit 0 when safety ok; callers must read LOOP_STOP token for stop vs continue.
exit 0
