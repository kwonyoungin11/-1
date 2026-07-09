#!/usr/bin/env bash
# Pre-tool safety hook (project). Owner may need /hooks-trust.
# Blocks dangerous patterns when invoked with a command string as $1.
set -euo pipefail
CMD="${1:-}"
if [[ -z "$CMD" ]]; then
  exit 0
fi

block() {
  echo "HOOK BLOCK: $1"
  exit 1
}

# Secret dumping
if echo "$CMD" | rg -qi '(^|[;&|` ]|\n)(cat|less|more|bat|head|tail)\s+.*\.env(\s|$)|Get-Content\s+.*\.env|(^|[;&| ])printenv(\s|$)|(^|[;&| ])env(\s|$)'; then
  block "refusing to dump environment or .env contents"
fi

# Destructive git / fs
if echo "$CMD" | rg -q 'git\s+reset\s+--hard|git\s+clean\s+-fdx|rm\s+-rf\s+/|rm\s+-rf\s+\.(|\s)'; then
  block "destructive command"
fi

# Pipe to shell installers
if echo "$CMD" | rg -q 'curl\s+.*\|\s*(sh|bash|dotnet)'; then
  block "pipe-to-shell install"
fi

# Live order / gate bypass language in shell
if echo "$CMD" | rg -qi 'SubmitOrderAsync|bypass.*(risk|kill|approval)|ALLOW_LIVE_ORDERS=true.*ORDER_MODE=live'; then
  block "live order or safety bypass attempt"
fi

exit 0
