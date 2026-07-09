#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
SNAP="artifacts/openapi/toss-openapi.snapshot.json"
TMP="$(mktemp)"
URL="https://openapi.tossinvest.com/openapi-docs/latest/openapi.json"
if [[ ! -f "$SNAP" ]]; then
  echo "No local snapshot. Run fetch-toss-openapi-spec.sh first."
  exit 1
fi
curl -fsSL "$URL" -o "$TMP"
if cmp -s "$SNAP" "$TMP"; then
  echo "OpenAPI snapshot unchanged"
  rm -f "$TMP"
  exit 0
fi
echo "WARNING: Official OpenAPI JSON differs from local snapshot."
echo "Review before changing any Toss client code."
if command -v diff >/dev/null; then
  diff -u <(jq -S . "$SNAP") <(jq -S . "$TMP") | head -100 || true
fi
rm -f "$TMP"
exit 2
