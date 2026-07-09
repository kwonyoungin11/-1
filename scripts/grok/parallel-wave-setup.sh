#!/usr/bin/env bash
# =============================================================================
# parallel-wave-setup.sh
#
# Create N agent git worktrees for a parallel development wave under .worktrees/.
# Each agent gets: dedicated folder + feature branch + .env symlink (no secret print).
#
# Usage:
#   bash scripts/grok/parallel-wave-setup.sh <wave-id> <base-branch> <slug> [slug ...]
#   bash scripts/grok/parallel-wave-setup.sh --wave <id> --base <branch> <slug> [slug ...]
#   bash scripts/grok/parallel-wave-setup.sh --wave <id> --base <branch> --agents risk,orders,obs
#
# Arguments:
#   wave-id       Wave number or tag (e.g. 05, 5, wave05). Normalized to "pw05".
#   base-branch   Existing branch to fork agent branches from
#                 (e.g. feature/parallel-wave-base). Must already exist locally.
#   slug ...      Agent short names (e.g. risk orders scripts)
#                 → worktree .worktrees/pw05-risk, branch feature/pw05-risk
#
# Options:
#   -h, --help    Show this help and exit
#   --wave ID     Same as positional wave-id
#   --base BR     Same as positional base-branch
#   --agents LIST Comma-separated slugs (alternative to positional slugs)
#   --dry-run     Print planned actions only; do not create worktrees
#   --skip-env    Do not create .env symlink even if main .env exists
#
# Examples:
#   # Three agents for wave 05 from parallel base:
#   bash scripts/grok/parallel-wave-setup.sh 05 feature/parallel-wave-base risk orders scripts
#
#   # Flag form with comma list:
#   bash scripts/grok/parallel-wave-setup.sh \
#     --wave 05 --base feature/parallel-wave-base --agents risk,orders,obs,scripts
#
#   # Dry-run (plan only):
#   bash scripts/grok/parallel-wave-setup.sh --dry-run 05 main risk orders
#
# Safety:
#   - Never cats/prints .env or secret values (symlink only; message says linked).
#   - Does not modify production C# code.
#   - Skips worktrees that already exist (idempotent).
#   - Fails if base branch is missing.
#
# Related:
#   scripts/grok/new-worktree.sh  — single worktree helper
#   docs/PARALLEL_AGENTS.md       — parallel agent policy
#   docs/WORKTREE_POLICY.md       — worktree policy
# =============================================================================
set -euo pipefail

usage() {
  sed -n '2,48p' "$0" | sed 's/^# \?//'
}

# Resolve main repository root (works when invoked from a worktree).
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
MAIN_ROOT="$(git -C "$ROOT" rev-parse --path-format=absolute --git-common-dir)"
MAIN_ROOT="$(cd "$MAIN_ROOT/.." && pwd)"

WAVE_ID=""
BASE_BRANCH=""
DRY_RUN=0
SKIP_ENV=0
AGENTS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h | --help)
      usage
      exit 0
      ;;
    --wave)
      WAVE_ID="${2:-}"
      shift 2
      ;;
    --base)
      BASE_BRANCH="${2:-}"
      shift 2
      ;;
    --agents)
      IFS=',' read -r -a _agent_list <<<"${2:-}"
      for a in "${_agent_list[@]}"; do
        a="$(echo "$a" | tr -d '[:space:]')"
        [[ -n "$a" ]] && AGENTS+=("$a")
      done
      shift 2
      ;;
    --dry-run)
      DRY_RUN=1
      shift
      ;;
    --skip-env)
      SKIP_ENV=1
      shift
      ;;
    --)
      shift
      break
      ;;
    -*)
      echo "error: unknown option: $1" >&2
      echo "run with --help for usage" >&2
      exit 2
      ;;
    *)
      # Positional: wave-id, base-branch, then slugs
      if [[ -z "$WAVE_ID" ]]; then
        WAVE_ID="$1"
      elif [[ -z "$BASE_BRANCH" ]]; then
        BASE_BRANCH="$1"
      else
        AGENTS+=("$1")
      fi
      shift
      ;;
  esac
done

# Remaining positionals after --
while [[ $# -gt 0 ]]; do
  if [[ -z "$WAVE_ID" ]]; then
    WAVE_ID="$1"
  elif [[ -z "$BASE_BRANCH" ]]; then
    BASE_BRANCH="$1"
  else
    AGENTS+=("$1")
  fi
  shift
done

if [[ -z "$WAVE_ID" || -z "$BASE_BRANCH" || ${#AGENTS[@]} -eq 0 ]]; then
  echo "error: need wave-id, base-branch, and at least one agent slug" >&2
  echo "" >&2
  usage >&2
  exit 2
fi

# Normalize wave id → "pw05" (accept 5, 05, pw05, wave05, wave-05)
normalize_wave() {
  local raw="$1"
  raw="$(echo "$raw" | tr '[:upper:]' '[:lower:]')"
  raw="${raw#wave-}"
  raw="${raw#wave}"
  raw="${raw#pw-}"
  raw="${raw#pw}"
  # pad single digit
  if [[ "$raw" =~ ^[0-9]$ ]]; then
    raw="0${raw}"
  fi
  if [[ ! "$raw" =~ ^[0-9a-z][0-9a-z_-]*$ ]]; then
    echo "error: invalid wave-id after normalize: $raw" >&2
    exit 2
  fi
  echo "pw${raw}"
}

# Validate agent slug (safe path/branch segment)
validate_slug() {
  local s="$1"
  if [[ ! "$s" =~ ^[a-zA-Z0-9][a-zA-Z0-9_-]*$ ]]; then
    echo "error: invalid agent slug: $s (use letters, digits, _ or -)" >&2
    exit 2
  fi
}

WAVE_PREFIX="$(normalize_wave "$WAVE_ID")"

for s in "${AGENTS[@]}"; do
  validate_slug "$s"
done

cd "$MAIN_ROOT"
mkdir -p .worktrees

if ! grep -q '^\.worktrees/' .gitignore 2>/dev/null; then
  printf '\n.worktrees/\n' >> .gitignore
  echo "note: added .worktrees/ to .gitignore (commit when ready)"
fi

if ! git show-ref --verify --quiet "refs/heads/${BASE_BRANCH}"; then
  # also try remote-tracking if local missing
  if git show-ref --verify --quiet "refs/remotes/origin/${BASE_BRANCH}"; then
    echo "note: local branch missing; creating local '${BASE_BRANCH}' from origin/${BASE_BRANCH}"
    if [[ "$DRY_RUN" -eq 0 ]]; then
      git branch "${BASE_BRANCH}" "origin/${BASE_BRANCH}"
    fi
  else
    echo "error: base branch not found locally or on origin: ${BASE_BRANCH}" >&2
    echo "hint: create it first, e.g. bash scripts/grok/new-worktree.sh wave-base ${BASE_BRANCH}" >&2
    exit 1
  fi
fi

echo "=== Parallel wave setup ==="
echo "main root : $MAIN_ROOT"
echo "wave      : $WAVE_PREFIX"
echo "base      : $BASE_BRANCH"
echo "agents    : ${AGENTS[*]}"
echo "count     : ${#AGENTS[@]}"
if [[ "$DRY_RUN" -eq 1 ]]; then
  echo "mode      : dry-run (no changes)"
fi
echo ""

CREATED=0
SKIPPED=0
LINKED=0

for slug in "${AGENTS[@]}"; do
  NAME="${WAVE_PREFIX}-${slug}"
  BRANCH="feature/${NAME}"
  WT=".worktrees/${NAME}"

  if [[ -d "$WT" ]]; then
    echo "[skip] already exists: $WT (branch may be $BRANCH)"
    SKIPPED=$((SKIPPED + 1))
  else
    echo "[plan] worktree add -b $BRANCH $WT $BASE_BRANCH"
    if [[ "$DRY_RUN" -eq 0 ]]; then
      if git show-ref --verify --quiet "refs/heads/${BRANCH}"; then
        # branch exists → attach worktree to existing branch
        git worktree add "$WT" "$BRANCH"
      else
        git worktree add -b "$BRANCH" "$WT" "$BASE_BRANCH"
      fi
      CREATED=$((CREATED + 1))
      echo "[ok]   created $WT on $BRANCH"
    fi
  fi

  # .env symlink — never print file contents
  if [[ "$SKIP_ENV" -eq 0 ]]; then
    if [[ -f "$MAIN_ROOT/.env" ]]; then
      if [[ "$DRY_RUN" -eq 1 ]]; then
        echo "[plan] ln -sfn <main>/.env $WT/.env  (values not printed)"
      else
        if [[ -d "$WT" ]]; then
          ln -sfn "$MAIN_ROOT/.env" "$WT/.env"
          LINKED=$((LINKED + 1))
          echo "[ok]   linked .env → $WT/.env (values not printed)"
        fi
      fi
    else
      echo "[note] no main .env found; skip symlink for $NAME"
    fi
  fi
  echo ""
done

echo "=== Summary ==="
echo "created : $CREATED"
echo "skipped : $SKIPPED"
echo "env link: $LINKED"
echo ""
echo "=== git worktree list ==="
git worktree list

if [[ "$DRY_RUN" -eq 0 && "$CREATED" -gt 0 ]]; then
  echo ""
  echo "Next: cd into each worktree and assign one agent per folder."
  echo "Example: cd \"$MAIN_ROOT/.worktrees/${WAVE_PREFIX}-${AGENTS[0]}\""
fi
