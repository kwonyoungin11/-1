#!/usr/bin/env bash
# Create an isolated git worktree under .worktrees/
#
# Usage:
#   bash scripts/grok/new-worktree.sh <folder-name> [branch-name] [base-branch]
#
# Arguments:
#   folder-name   Directory under .worktrees/ (e.g. phase-readonly, wave-base)
#   branch-name   Optional; default feature/<folder-name>
#   base-branch   Optional base when creating a new branch; default main
#
# Examples:
#   bash scripts/grok/new-worktree.sh phase-readonly feature/toss-readonly
#   bash scripts/grok/new-worktree.sh pw05-risk feature/pw05-risk feature/parallel-wave-base
#
# Notes:
#   - Links main .env via symlink only (never prints secret values).
#   - For multi-agent waves prefer: scripts/grok/parallel-wave-setup.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
# Resolve main repo root (works from worktree or main checkout)
MAIN_ROOT="$(git -C "$ROOT" rev-parse --path-format=absolute --git-common-dir)"
MAIN_ROOT="$(cd "$MAIN_ROOT/.." && pwd)"

NAME="${1:-}"
BRANCH="${2:-}"
BASE_BRANCH="${3:-main}"
if [[ -z "$NAME" ]]; then
  echo "usage: new-worktree.sh <folder-name> [branch-name] [base-branch]"
  echo "example: new-worktree.sh phase-readonly feature/toss-readonly"
  echo "example: new-worktree.sh pw05-risk feature/pw05-risk feature/parallel-wave-base"
  exit 2
fi
if [[ -z "$BRANCH" ]]; then
  BRANCH="feature/$NAME"
fi

cd "$MAIN_ROOT"
mkdir -p .worktrees
if ! grep -q '^\.worktrees/' .gitignore 2>/dev/null; then
  printf '\n.worktrees/\n' >> .gitignore
  echo "note: added .worktrees/ to .gitignore (commit when ready)"
fi

WT=".worktrees/$NAME"
if [[ -d "$WT" ]]; then
  echo "already exists: $WT"
  git worktree list
  exit 0
fi

if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
  git worktree add "$WT" "$BRANCH"
else
  if ! git show-ref --verify --quiet "refs/heads/$BASE_BRANCH"; then
    echo "error: base branch not found: $BASE_BRANCH" >&2
    exit 1
  fi
  git worktree add -b "$BRANCH" "$WT" "$BASE_BRANCH"
fi

if [[ -f "$MAIN_ROOT/.env" ]]; then
  ln -sfn "$MAIN_ROOT/.env" "$WT/.env"
  echo "linked .env (values not printed)"
fi

echo "Worktree ready: $MAIN_ROOT/$WT"
echo "Branch: $BRANCH"
echo "Base: $BASE_BRANCH"
echo "Next: cd \"$MAIN_ROOT/$WT\""
git worktree list
