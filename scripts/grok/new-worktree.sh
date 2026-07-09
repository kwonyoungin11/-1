#!/usr/bin/env bash
# Create an isolated git worktree under .worktrees/
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
# If we are inside a worktree, resolve to main repo root
COMMON="$(cd "$(git -C "$ROOT" rev-parse --git-common-dir)" && pwd)"
MAIN_ROOT="$(cd "$COMMON/.." && pwd)"
# git-common-dir for main is $MAIN/.git ; for worktree link it's $MAIN/.git
# When ROOT is worktree, common is MAIN/.git, parent is MAIN
if [[ -f "$COMMON/config" ]]; then
  MAIN_ROOT="$(cd "$COMMON/.." && pwd)"
fi
# Better: use rev-parse --show-toplevel from common
MAIN_ROOT="$(git -C "$ROOT" rev-parse --path-format=absolute --git-common-dir)"
MAIN_ROOT="$(cd "$MAIN_ROOT/.." && pwd)"

NAME="${1:-}"
BRANCH="${2:-}"
if [[ -z "$NAME" ]]; then
  echo "usage: new-worktree.sh <folder-name> [branch-name]"
  echo "example: new-worktree.sh phase-readonly feature/toss-readonly"
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
  git worktree add -b "$BRANCH" "$WT" main
fi

if [[ -f "$MAIN_ROOT/.env" ]]; then
  ln -sfn "$MAIN_ROOT/.env" "$WT/.env"
  echo "linked .env (values not printed)"
fi

echo "Worktree ready: $MAIN_ROOT/$WT"
echo "Branch: $BRANCH"
echo "Next: cd \"$MAIN_ROOT/$WT\""
git worktree list
