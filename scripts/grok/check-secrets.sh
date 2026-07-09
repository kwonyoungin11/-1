#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

echo "== secret protection scan =="
failures=0

if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  if git ls-files --error-unmatch .env >/dev/null 2>&1; then
    echo "BLOCK: .env is tracked by git"
    failures=$((failures+1))
  else
    echo "ok: .env is not tracked"
  fi
  if grep -E '^\.env(\.|$|\s)' .gitignore >/dev/null 2>&1; then
    echo "ok: .gitignore covers .env"
  else
    echo "BLOCK: .gitignore missing .env rule"
    failures=$((failures+1))
  fi
else
  echo "warning: not a git repo"
fi

if [[ ! -f .env.example ]]; then
  echo "BLOCK: .env.example missing"
  failures=$((failures+1))
else
  echo "ok: .env.example present"
fi

# Scan text files for high-risk private key blocks (exclude openapi snapshot bulk)
while IFS= read -r f; do
  [[ -z "$f" ]] && continue
  case "$f" in
    .env|.env.*|./.env|./.env.*) continue ;;
  esac
  if rg -n --no-heading -e 'BEGIN (RSA |OPENSSH )?PRIVATE KEY' "$f" 2>/dev/null; then
    echo "BLOCK: private key material in $f"
    failures=$((failures+1))
  fi
done <<LIST
$(git ls-files 2>/dev/null || find . -type f \( -name '*.cs' -o -name '*.md' -o -name '*.toml' -o -name '*.json' -o -name '*.sh' -o -name '*.ps1' -o -name '.env.example' \) ! -path './.git/*' ! -path './artifacts/*')
LIST

if [[ $failures -gt 0 ]]; then
  echo "secret scan FAILED ($failures)"
  exit 1
fi
echo "secret scan PASSED"
