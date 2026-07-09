# PARALLEL WAVE 05 — max agents + worktrees

**Date:** 2026-07-09  
**Base:** `feature/parallel-wave-base` @ `.worktrees/wave-base`  
**UI:** keep current cockpit layout  
**Safety:** no live orders, no secret logging, `TOSS_ALLOW_LIVE_HTTP` stays default false unless owner opts in

## Agents (max parallel)

| ID | Worktree | Branch | Paths only | Goal |
|----|----------|--------|------------|------|
| A | `.worktrees/pw05-risk` | `feature/pw05-risk` | `src/TradingBot.Risk/`, `tests/TradingBot.Risk.Tests/` | Extra risk unit tests / session guard edge cases |
| B | `.worktrees/pw05-orders` | `feature/pw05-orders` | `src/TradingBot.Orders/`, `tests/TradingBot.Orders.Tests/` | dry-run/paper ledger evidence tests |
| C | `.worktrees/pw05-obs` | `feature/pw05-obs` | `src/TradingBot.Observability/`, `tests/TradingBot.Observability.Tests/` | session audit event helpers (no secrets) |
| D | `.worktrees/pw05-phase6` | `feature/pw05-phase6` | `docs/LIVE_READINESS_CHECKLIST.md`, `docs/plans/LIVE_READINESS_EVIDENCE.md`, `docs/plans/MASTER_PLAN.md` | Phase 6 evidence map progress (docs only) |
| E | `.worktrees/pw05-toss` | `feature/pw05-toss` | `src/TradingBot.Infrastructure.Toss/`, `tests/TradingBot.Infrastructure.Toss.Tests/` | env-gated read smoke test helper (skip if no flag) |
| F | `.worktrees/pw05-scripts` | `feature/pw05-scripts` | `scripts/grok/` | `parallel-wave-setup.sh` for N agent worktrees |
| G | `.worktrees/pw05-domain` | `feature/pw05-domain` | `src/TradingBot.Domain/`, `tests/TradingBot.Domain.Tests/` | catalog invariant tests |
| H | `.worktrees/pw05-app-tests` | `feature/pw05-app-tests` | `tests/TradingBot.App.Tests/` only | connection label / harness tests (no UI redesign) |

## Merge order

1. Each agent commits on its branch  
2. Orchestrator merges A→H into `feature/parallel-wave-base`  
3. `dotnet test` + safety scan on wave-base  
4. Owner-approved merge to `main`

## Forbidden

- Live order unlock  
- Printing `.env` values  
- Editing same files as another agent  
- Avalonia layout redesign  
