#!/usr/bin/env bash
# Context7 API key rotation wrapper.
# Reads ENV aliases only. NEVER prints key values.
set -euo pipefail

ALIASES=(
  CONTEXT7_API_KEY_1
  CONTEXT7_API_KEY_2
  CONTEXT7_API_KEY_3
  CONTEXT7_API_KEY_4
  CONTEXT7_API_KEY_5
  CONTEXT7_API_KEY_6
)

QUERY="${1:-}"
if [[ -z "$QUERY" ]]; then
  echo "usage: context7-key-rotation.sh \"<docs query>\""
  echo "Note: this wrapper only validates key presence + documents rotation policy."
  echo "Prefer Context7 MCP when available."
  exit 2
fi

for alias in "${ALIASES[@]}"; do
  # Indirect expansion without echoing value
  val="${!alias-}"
  if [[ -z "$val" ]]; then
    echo "skip: $alias (unset)"
    continue
  fi
  echo "try: $alias"
  # Actual Context7 HTTP call is environment-specific; MCP is preferred.
  # Here we only report which alias would be used.
  echo "Context7 request would use $alias"
  echo "SUCCESS_ALIAS=$alias"
  exit 0
done

echo "Context7 사용 실패:"
echo "- 실패 alias 범위: CONTEXT7_API_KEY_1 ~ CONTEXT7_API_KEY_6"
echo "- 실제 key 값 출력 없음"
echo "- 실패 유형: all aliases unset or empty"
echo "- 수동 확인 필요한 문서: .NET / Toss docs via official URLs"
echo "- 다음 조치: fill CONTEXT7_API_KEY_1..6 in local .env (never commit)"
exit 1
