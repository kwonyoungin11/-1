#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
echo "== owner readiness check =="
missing=0
required=(
  AGENTS.md
  .env.example
  .gitignore
  docs/OWNER_PLAYBOOK.md
  docs/LIVE_READINESS_CHECKLIST.md
  docs/UX_UI_GUIDE.md
  docs/DESIGN_SYSTEM.md
  docs/TOSS_OPENAPI_NOTES.md
  docs/MACOS_SETUP.md
  docs/INCIDENT_RESPONSE.md
  .agents/state/CURRENT_STATE.md
  scripts/grok/check-secrets.sh
  scripts/grok/check-trading-safety.sh
)
for f in "${required[@]}"; do
  if [[ -f "$f" ]]; then
    echo "ok: $f"
  else
    echo "missing: $f"
    missing=$((missing+1))
  fi
done
if [[ $missing -gt 0 ]]; then
  echo "owner readiness FAILED ($missing missing)"
  exit 1
fi
echo "owner readiness PASSED (docs/harness present; live still blocked)"
