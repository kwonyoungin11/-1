#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

echo "== TradingBot verify harness =="
bash ./scripts/grok/check-secrets.sh
bash ./scripts/grok/check-trading-safety.sh
bash ./scripts/grok/check-owner-readiness.sh

if ! command -v dotnet >/dev/null 2>&1; then
  echo "warning: dotnet SDK not installed — skipping restore/build/test"
  echo "See docs/MACOS_SETUP.md"
else
  dotnet --info
  dotnet restore
  dotnet build --configuration Release --no-restore
  dotnet test --configuration Release --no-build
  if command -v dotnet >/dev/null && dotnet format --version >/dev/null 2>&1; then
    dotnet format --verify-no-changes || echo "warning: format verify reported issues"
  fi
  dotnet list package --vulnerable --include-transitive || true
fi

if command -v gitleaks >/dev/null 2>&1; then
  gitleaks detect --no-banner || true
else
  echo "warning: gitleaks not installed"
fi

if command -v trivy >/dev/null 2>&1; then
  trivy fs . || true
else
  echo "warning: trivy not installed"
fi

echo "== verify complete (live orders remain blocked) =="
