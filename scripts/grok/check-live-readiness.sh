#!/usr/bin/env bash
# Live readiness automation (docs + safety defaults only).
#
# Purpose:
#   Confirm live trading remains correctly BLOCKED and that readiness
#   evidence harness exists. This script never enables live orders.
#
# Exit codes:
#   0 — safety intact: live defaults blocked, checklist present, no live submit
#       path in src. Prints LIVE_READY=false (expected until owner phase 7).
#   1 — safety BROKEN (defaults allow live, kill switch off by default,
#       non-dry-run default, or SubmitOrderAsync / live submit pattern in src).
#
# This is NOT a green light for live. Passing means "blocked as designed".
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

echo "== live readiness automation =="
echo "note: this gate never flips live flags; live stays blocked by policy"

DEFAULTS="src/TradingBot.Domain/TradingSafetyDefaults.cs"
CHECKLIST="docs/LIVE_READINESS_CHECKLIST.md"
EVIDENCE="docs/plans/LIVE_READINESS_EVIDENCE.md"

broken=0
missing_docs=0

# --- Safety defaults (must remain fail-closed) ---
if [[ ! -f "$DEFAULTS" ]]; then
  echo "BROKEN: missing $DEFAULTS"
  broken=$((broken + 1))
else
  if ! rg -n 'AllowLiveOrders\s*=\s*false' "$DEFAULTS" >/dev/null; then
    echo "BROKEN: TradingSafetyDefaults.AllowLiveOrders is not false"
    broken=$((broken + 1))
  else
    echo "ok: AllowLiveOrders default false"
  fi

  if ! rg -n 'KillSwitch\s*=\s*true' "$DEFAULTS" >/dev/null; then
    echo "BROKEN: TradingSafetyDefaults.KillSwitch is not true"
    broken=$((broken + 1))
  else
    echo "ok: KillSwitch default true"
  fi

  if ! rg -n 'OrderMode\s*=\s*OrderMode\.DryRun' "$DEFAULTS" >/dev/null; then
    echo "BROKEN: TradingSafetyDefaults.OrderMode is not DryRun"
    broken=$((broken + 1))
  else
    echo "ok: OrderMode default DryRun"
  fi
fi

# --- No live order submit implementation in src ---
if rg -n --glob '*.cs' -e 'SubmitOrderAsync\s*\(' src 2>/dev/null; then
  echo "BROKEN: SubmitOrderAsync found under src (live submit path must not exist yet)"
  broken=$((broken + 1))
else
  echo "ok: no SubmitOrderAsync in src"
fi

# --- Evidence / checklist docs (required for exit 0) ---
if [[ -f "$CHECKLIST" ]]; then
  echo "ok: $CHECKLIST present"
else
  echo "missing: $CHECKLIST"
  missing_docs=$((missing_docs + 1))
fi

if [[ -f "$EVIDENCE" ]]; then
  echo "ok: $EVIDENCE present"
else
  # Evidence plan is part of automation harness; warn but treat as missing doc.
  echo "missing: $EVIDENCE"
  missing_docs=$((missing_docs + 1))
fi

# --- Summary (never claims live ready) ---
echo ""
echo "LIVE_READY=false"
echo "live_not_ready: expected until owner phase 7 + full checklist evidence"

if [[ "$broken" -gt 0 ]]; then
  echo "LIVE_SAFETY_INTACT=false"
  echo "LIVE_READINESS_STATUS=broken"
  echo "live readiness FAILED — safety BROKEN ($broken issue(s))"
  echo "Do not open live trading. Fix defaults / remove live submit paths first."
  exit 1
fi

if [[ "$missing_docs" -gt 0 ]]; then
  echo "LIVE_SAFETY_INTACT=true"
  echo "LIVE_READINESS_STATUS=incomplete_docs"
  echo "live readiness FAILED — safety intact but readiness docs missing ($missing_docs)"
  exit 1
fi

echo "LIVE_SAFETY_INTACT=true"
echo "LIVE_READINESS_STATUS=blocked_as_expected"
echo "live readiness automation PASSED (live remains blocked)"
exit 0
