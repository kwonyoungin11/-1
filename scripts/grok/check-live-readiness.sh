#!/usr/bin/env bash
# Live readiness automation (safety defaults + ops artifact presence).
#
# Purpose:
#   Confirm live trading remains correctly BLOCKED under fail-closed defaults,
#   and report whether ops artifacts under artifacts/live-readiness/ are present
#   for owner unlock consideration. This script never enables live orders.
#
# Exit codes:
#   0 — safety intact (defaults fail-closed). May still be missing evidence.
#       LIVE_READY is always false under defaults. Distinguishes:
#         LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence
#         LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock
#   1 — safety BROKEN (defaults allow live / kill off / non-dry-run default,
#       or unguarded live submit pattern). LIVE_OWNER_UNLOCK_STATUS=broken_defaults
#
# Notes:
#   - Gated router APIs (e.g. SubmitCandidateAsync behind live gates) are allowed.
#   - LIVE_READY is never printed true by this script alone.
#   - C# source of truth: TradingBot.Domain.LiveReadinessEvaluator
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

echo "== live readiness automation =="
echo "note: this gate never flips live flags; live stays blocked by policy"

DEFAULTS="src/TradingBot.Domain/TradingSafetyDefaults.cs"
CHECKLIST="docs/LIVE_READINESS_CHECKLIST.md"
EVIDENCE="docs/plans/LIVE_READINESS_EVIDENCE.md"
ARTIFACT_DIR="artifacts/live-readiness"

broken=0
missing_evidence=0

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

# --- Live submit path policy ---
# Fail only on unguarded legacy SubmitOrderAsync. Gated candidate APIs
# (SubmitCandidateAsync) are allowed to exist behind live gates.
if rg -n --glob '*.cs' -e 'SubmitOrderAsync\s*\(' src 2>/dev/null; then
  echo "BROKEN: SubmitOrderAsync found under src (unguarded live submit must not exist)"
  broken=$((broken + 1))
else
  echo "ok: no SubmitOrderAsync in src (gated SubmitCandidateAsync allowed)"
fi

# --- Docs (informational; missing docs count as missing evidence, not broken defaults) ---
if [[ -f "$CHECKLIST" ]]; then
  echo "ok: $CHECKLIST present"
else
  echo "missing: $CHECKLIST (evidence incomplete)"
  missing_evidence=$((missing_evidence + 1))
fi

if [[ -f "$EVIDENCE" ]]; then
  echo "ok: $EVIDENCE present"
else
  echo "missing: $EVIDENCE (evidence incomplete)"
  missing_evidence=$((missing_evidence + 1))
fi

# --- Ops artifacts (mirror LiveReadinessEvaluator) ---
echo "-- artifacts under $ARTIFACT_DIR --"
if [[ ! -d "$ARTIFACT_DIR" ]]; then
  echo "missing: $ARTIFACT_DIR/"
  missing_evidence=$((missing_evidence + 1))
else
  if [[ -f "$ARTIFACT_DIR/paper-multi-session-export.txt" ]]; then
    echo "ok: paper-multi-session-export.txt"
  elif [[ -f "$ARTIFACT_DIR/paper-multi-session-export.json" ]]; then
    echo "ok: paper-multi-session-export.json"
  else
    echo "missing: paper-multi-session-export.txt|json"
    missing_evidence=$((missing_evidence + 1))
  fi

  if [[ ! -f "$ARTIFACT_DIR/incident-drill-record.md" ]]; then
    echo "missing: incident-drill-record.md"
    missing_evidence=$((missing_evidence + 1))
  elif ! rg -n -e '\b(20[0-9]{2})-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])\b' \
      "$ARTIFACT_DIR/incident-drill-record.md" >/dev/null; then
    echo "missing: incident-drill-record.md date (YYYY-MM-DD)"
    missing_evidence=$((missing_evidence + 1))
  else
    echo "ok: incident-drill-record.md (has YYYY-MM-DD)"
  fi

  if [[ -f "$ARTIFACT_DIR/openapi-recheck.log" ]]; then
    echo "ok: openapi-recheck.log"
  else
    echo "missing: openapi-recheck.log"
    missing_evidence=$((missing_evidence + 1))
  fi

  if [[ -f "$ARTIFACT_DIR/owner-unlock-signoff.md" ]]; then
    echo "ok: owner-unlock-signoff.md"
  else
    echo "missing: owner-unlock-signoff.md"
    missing_evidence=$((missing_evidence + 1))
  fi

  if [[ -f "$ARTIFACT_DIR/toss-read-smoke-redacted.log" ]]; then
    echo "ok: toss-read-smoke-redacted.log (optional)"
  elif [[ -f "$ARTIFACT_DIR/toss-read-smoke-residual.md" ]]; then
    echo "ok: toss-read-smoke-residual.md (optional)"
  else
    echo "note: optional toss read smoke artifact absent"
  fi
fi

# --- Summary (never claims live ready under defaults) ---
echo ""
echo "LIVE_READY=false"
echo "live_not_ready: defaults never auto-enable live; owner unlock is separate"

if [[ "$broken" -gt 0 ]]; then
  echo "LIVE_SAFETY_INTACT=false"
  echo "LIVE_READINESS_STATUS=broken"
  echo "LIVE_OWNER_UNLOCK_STATUS=broken_defaults"
  echo "live readiness FAILED — safety BROKEN ($broken issue(s))"
  echo "Do not open live trading. Fix defaults / remove unguarded live submit paths first."
  exit 1
fi

echo "LIVE_SAFETY_INTACT=true"

if [[ "$missing_evidence" -gt 0 ]]; then
  echo "LIVE_READINESS_STATUS=blocked_missing_evidence"
  echo "LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence"
  echo "live readiness automation PASSED (safety intact; evidence incomplete: $missing_evidence)"
  echo "LIVE_READY remains false until owner unlock process after full evidence."
  exit 0
fi

echo "LIVE_READINESS_STATUS=ready_for_owner_unlock"
echo "LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock"
echo "live readiness automation PASSED (safety intact; required ops artifacts present)"
echo "LIVE_READY remains false — ready for owner unlock only, not live enablement."
exit 0
