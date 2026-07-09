#!/usr/bin/env bash
# Post-tool reminders based on changed paths (args: space-separated paths)
set -euo pipefail
CHANGED="${*:-}"
[[ -z "$CHANGED" ]] && exit 0

remind() { echo "HOOK REMINDER: $1"; }

echo "$CHANGED" | rg -q '\.cs$' && remind "C# changed → run dotnet build/test when SDK available"
echo "$CHANGED" | rg -q '\.csproj$' && remind "csproj changed → restore/build/test"
echo "$CHANGED" | rg -qi 'toss' && remind "Toss-related change → re-check official OpenAPI"
echo "$CHANGED" | rg -qi 'order' && remind "Order-related change → fail-closed review + trading safety scan"
echo "$CHANGED" | rg -qi 'risk' && remind "Risk change → risk gate tests"
echo "$CHANGED" | rg -qi 'Ui|UX|DESIGN' && remind "UI change → update UX/UI docs"
echo "$CHANGED" | rg -q '\.env\.example' && remind ".env.example change → placeholders only"
echo "$CHANGED" | rg -q 'AGENTS\.md|\.grok/config' && remind "governance change → ensure no secrets hardcoded"
exit 0
