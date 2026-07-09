#!/usr/bin/env bash
# Development verification loop (NOT a live trading loop).
# Runs secret scan → trading safety scan → owner readiness → dotnet test.
# Retries only on recoverable test/build failures. Safety BLOCK stops immediately.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

MAX_ATTEMPTS="${MAX_ATTEMPTS:-5}"
if ! [[ "$MAX_ATTEMPTS" =~ ^[0-9]+$ ]] || [[ "$MAX_ATTEMPTS" -lt 1 ]]; then
  echo "INVALID MAX_ATTEMPTS=$MAX_ATTEMPTS"
  exit 2
fi
if [[ "$MAX_ATTEMPTS" -gt 10 ]]; then
  echo "MAX_ATTEMPTS capped at 10 (was $MAX_ATTEMPTS)"
  MAX_ATTEMPTS=10
fi

echo "== DEV LOOP (development only; live orders never run here) =="
echo "cwd: $ROOT"
echo "max attempts: $MAX_ATTEMPTS"
echo "branch: $(git branch --show-current 2>/dev/null || echo unknown)"

run_gate() {
  local name="$1"
  shift
  echo ""
  echo "--- gate: $name ---"
  if "$@"; then
    echo "PASS: $name"
    return 0
  else
    echo "FAIL: $name"
    return 1
  fi
}

# Non-retryable safety gates (run every attempt; block must not be "worked around")
run_safety_gates() {
  local failed=0
  run_gate "check-secrets" bash ./scripts/grok/check-secrets.sh || failed=1
  run_gate "check-trading-safety" bash ./scripts/grok/check-trading-safety.sh || failed=1
  run_gate "check-owner-readiness" bash ./scripts/grok/check-owner-readiness.sh || failed=1
  # Passes while reporting LIVE_READY=false (blocked_as_expected). Fail only if safety broken / docs missing.
  run_gate "check-live-readiness" bash ./scripts/grok/check-live-readiness.sh || failed=1
  # MANDATORY: parallel worktree + max agents policy files
  run_gate "check-parallel-policy" bash ./scripts/grok/check-parallel-policy.sh || failed=1
  return "$failed"
}

run_dotnet_gates() {
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "warning: dotnet not installed — skipping build/test"
    return 0
  fi
  local failed=0
  run_gate "dotnet-restore" dotnet restore || failed=1
  if [[ "$failed" -ne 0 ]]; then return 1; fi
  run_gate "dotnet-build" dotnet build --configuration Release --no-restore || failed=1
  if [[ "$failed" -ne 0 ]]; then return 1; fi
  run_gate "dotnet-test" dotnet test --configuration Release --no-build --verbosity quiet || failed=1
  return "$failed"
}

attempt=1
while [[ "$attempt" -le "$MAX_ATTEMPTS" ]]; do
  echo ""
  echo "======== attempt $attempt / $MAX_ATTEMPTS ========"

  if ! run_safety_gates; then
    echo ""
    echo "DEV_LOOP_RESULT=BLOCKED_SAFETY"
    echo "Safety/secret gate failed. Do not retry by weakening gates."
    echo "live order: still blocked by project policy"
    exit 1
  fi

  if run_dotnet_gates; then
    echo ""
    echo "DEV_LOOP_RESULT=SUCCESS"
    echo "attempts_used=$attempt"
    echo "live order: blocked (dev loop does not enable live)"
    exit 0
  fi

  echo "Recoverable failure on attempt $attempt (build/test)."
  if [[ "$attempt" -ge "$MAX_ATTEMPTS" ]]; then
    break
  fi
  echo "Maker should fix code, then re-run. Waiting is not auto-edit in this script."
  echo "If invoked by an agent: fix failures and re-run this script (counts as next attempt)."
  attempt=$((attempt + 1))
done

echo ""
echo "DEV_LOOP_RESULT=STOPPED_MAX_ATTEMPTS"
echo "attempts_used=$MAX_ATTEMPTS"
echo "Report first failing gate to owner. Do not open live trading."
exit 1
