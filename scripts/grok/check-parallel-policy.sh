#!/usr/bin/env bash
# Verifies parallel-worktree / max-agent policy files exist and AGENTS.md marks them mandatory.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

fail() { echo "PARALLEL_POLICY=fail: $*"; exit 1; }
ok() { echo "ok: $*"; }

[[ -f docs/PARALLEL_AGENTS.md ]] || fail "missing docs/PARALLEL_AGENTS.md"
[[ -f docs/WORKTREE_POLICY.md ]] || fail "missing docs/WORKTREE_POLICY.md"
[[ -f .grok/skills/parallel-worktree-max-agents/SKILL.md ]] || fail "missing parallel-worktree-max-agents skill"
[[ -x scripts/grok/parallel-wave-setup.sh ]] || [[ -f scripts/grok/parallel-wave-setup.sh ]] || fail "missing parallel-wave-setup.sh"

rg -q 'MANDATORY: parallel worktrees|병렬 worktree \+ 최대 에이전트 필수|require_max_parallel_agents' AGENTS.md \
  || fail "AGENTS.md missing MANDATORY parallel worktree/max agent language"

rg -qi '필수|MANDATORY' docs/PARALLEL_AGENTS.md || fail "PARALLEL_AGENTS.md not marked mandatory"
rg -qi '필수|MANDATORY' docs/WORKTREE_POLICY.md || fail "WORKTREE_POLICY.md not marked mandatory"

ok "PARALLEL_AGENTS.md present and mandatory"
ok "WORKTREE_POLICY.md present and mandatory"
ok "parallel-worktree-max-agents skill present"
ok "parallel-wave-setup.sh present"
ok "AGENTS.md constitution enforces max parallel"

echo "PARALLEL_POLICY=pass"
echo "PARALLEL_WORKTREE_MAX_AGENTS=mandatory"
exit 0
