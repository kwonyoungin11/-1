#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

echo "== trading safety scan =="
failures=0

if ! rg -n 'AllowLiveOrders\s*=\s*false' src/TradingBot.Domain/TradingSafetyDefaults.cs >/dev/null; then
  echo "BLOCK: TradingSafetyDefaults.AllowLiveOrders is not false"
  failures=$((failures+1))
else
  echo "ok: AllowLiveOrders default false"
fi

if ! rg -n 'KillSwitch\s*=\s*true' src/TradingBot.Domain/TradingSafetyDefaults.cs >/dev/null; then
  echo "BLOCK: TradingSafetyDefaults.KillSwitch is not true"
  failures=$((failures+1))
else
  echo "ok: KillSwitch default true"
fi

if ! rg -n 'OrderMode\s*=\s*OrderMode\.DryRun' src/TradingBot.Domain/TradingSafetyDefaults.cs >/dev/null; then
  echo "BLOCK: default OrderMode is not DryRun"
  failures=$((failures+1))
else
  echo "ok: OrderMode default DryRun"
fi

if ! rg -n 'IsLiveSubmissionEnabled\s*=>\s*false' src/TradingBot.Infrastructure.Toss >/dev/null; then
  echo "BLOCK: Toss order client live submission not disabled"
  failures=$((failures+1))
else
  echo "ok: Toss live submission disabled"
fi

if rg -n --glob '*.cs' -e 'ALLOW_LIVE_ORDERS\s*=\s*true' -e 'ORDER_MODE\s*=\s*live' -e 'CanSubmitLiveOrder\s*=\s*true' -e 'ReadyForLive\s*=\s*true' src 2>/dev/null; then
  echo "BLOCK: dangerous live-enable pattern in src"
  failures=$((failures+1))
else
  echo "ok: no live-enable literals in src"
fi

# Executable submit patterns only (ignore comments by requiring code-like context)
if rg -n --glob '*.cs' -e 'SubmitOrderAsync\s*\(' -e 'HttpMethod\.Post.*orders' -e 'PostAsJsonAsync\([^\)]*orders' src 2>/dev/null; then
  echo "BLOCK: order submit implementation pattern in src"
  failures=$((failures+1))
else
  echo "ok: no order submit implementation in src"
fi

if [[ -f .env.example ]]; then
  rg -n '^ALLOW_LIVE_ORDERS=false$' .env.example >/dev/null || { echo "BLOCK: .env.example ALLOW_LIVE_ORDERS not false"; failures=$((failures+1)); }
  rg -n '^KILL_SWITCH=true$' .env.example >/dev/null || { echo "BLOCK: .env.example KILL_SWITCH not true"; failures=$((failures+1)); }
  rg -n '^ORDER_MODE=dry_run$' .env.example >/dev/null || { echo "BLOCK: .env.example ORDER_MODE not dry_run"; failures=$((failures+1)); }
  echo "ok: .env.example safety defaults"
fi

if [[ $failures -gt 0 ]]; then
  echo "trading safety scan FAILED ($failures)"
  exit 1
fi
echo "trading safety scan PASSED"
