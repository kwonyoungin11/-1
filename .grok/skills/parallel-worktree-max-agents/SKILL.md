---
name: parallel-worktree-max-agents
description: MANDATORY project skill. All feature work must use git worktrees and maximum parallel agents (5-8 when independent). Never code on main. Invoke before any multi-file or multi-domain implementation.
---

# parallel-worktree-max-agents — **필수**

## Status

**MANDATORY (오너 확정).** Not optional. Not “when convenient”.

## When

**Before** any non-trivial implementation (2+ files, 2+ domains, or any feature wave):

1. Confirm you are **not** coding in repo `main` checkout for features.
2. Create/use wave base worktree.
3. Split into **max independent agents**, each with own worktree + branch + path zone.
4. Dispatch agents **in parallel** (same turn), not one-by-one.
5. Merge to wave-base → verify → only then main.

## Commands

```bash
bash scripts/grok/parallel-wave-setup.sh <wave> feature/parallel-wave-base <slugs...>
# or
bash scripts/grok/new-worktree.sh <name> <branch> <base>
```

## Limits

| Item | Rule |
|------|------|
| Min agents if 2+ independent tasks | 2 |
| Target agents | **5–8** |
| One agent does everything | **FORBIDDEN** |
| Work on main | **FORBIDDEN** |
| Live order unlock | **FORBIDDEN** |
| Secret print | **FORBIDDEN** |

## Path zones

Each agent gets exclusive paths (example):

- Risk / Orders / Observability / Toss / Domain / App.Tests / docs-phase6 / scripts

No overlapping writes.

## Orchestrator checklist

- [ ] wave base ready
- [ ] zones non-overlapping
- [ ] max agents spawned together
- [ ] each committed on its branch
- [ ] merge to base + tests + safety scan
- [ ] owner report in plain Korean

## Docs

- `docs/PARALLEL_AGENTS.md`
- `docs/WORKTREE_POLICY.md`
- `AGENTS.md` Absolute rules MANDATORY section
