#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
URL="https://openapi.tossinvest.com/openapi-docs/latest/openapi.json"
OUT="artifacts/openapi/toss-openapi.snapshot.json"
mkdir -p artifacts/openapi docs/specs
echo "Fetching official OpenAPI JSON (no credentials)..."
curl -fsSL "$URL" -o "$OUT"
jq '{openapi, info, servers, path_count: (.paths|keys|length), paths: (.paths|keys)}' "$OUT" > docs/specs/toss-openapi-summary.json
jq -r '.paths | to_entries[] | .key as $p | .value | to_entries[] | "\(.key | ascii_upcase)\t\($p)"' "$OUT" | sort > docs/specs/toss-endpoints.tsv
echo "Saved $OUT and docs/specs summaries"
