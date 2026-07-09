# Live Readiness Evidence — How Evidence Is Collected

**Status: LIVE REMAINS BLOCKED**  
**Date baseline: 2026-07-09**

This document explains **how** readiness evidence is collected and verified.  
It does **not** authorize live orders. Until every item on
[`docs/LIVE_READINESS_CHECKLIST.md`](../LIVE_READINESS_CHECKLIST.md) has
reproducible evidence **and** the owner signs phase-7 approval, live is impossible.

---

## 1. What “evidence” means

| Kind | Meaning | Example |
|------|---------|---------|
| Machine gate | Script exit 0 with explicit status lines | `LIVE_READY=false`, `LIVE_SAFETY_INTACT=true` |
| Automated test | `dotnet test` assertion that would fail if safety weakened | `TradingSafetyDefaultsTests` |
| Artifact | Timestamped log, ledger export, OpenAPI snapshot | dry-run ledger, paper fill log |
| Owner sign-off | Written approval with date and conditions | live transition approval form |

Passing a machine gate **never** means “open live”.  
The live-readiness automation gate is designed to **pass while live is blocked**.

---

## 2. Automation script (current)

```bash
bash scripts/grok/check-live-readiness.sh
```

### Pass conditions (exit 0)

All of the following must hold:

1. `src/TradingBot.Domain/TradingSafetyDefaults.cs`
   - `AllowLiveOrders = false`
   - `KillSwitch = true`
   - `OrderMode = OrderMode.DryRun`
2. `docs/LIVE_READINESS_CHECKLIST.md` exists
3. `docs/plans/LIVE_READINESS_EVIDENCE.md` exists (this file)
4. No `SubmitOrderAsync(` under `src/**/*.cs`

### Output (expected today)

```text
LIVE_READY=false
LIVE_SAFETY_INTACT=true
LIVE_READINESS_STATUS=blocked_as_expected
live readiness automation PASSED (live remains blocked)
```

### Fail conditions (exit 1)

| Cause | Status line | Action |
|-------|-------------|--------|
| Defaults allow live / kill off / not dry-run | `LIVE_READINESS_STATUS=broken` | Revert immediately; do not trade |
| `SubmitOrderAsync` present in `src` | `LIVE_READINESS_STATUS=broken` | Remove or gate behind explicit future phase |
| Checklist / evidence doc missing | `LIVE_READINESS_STATUS=incomplete_docs` | Restore docs; safety may still be intact |

**This script never sets `ALLOW_LIVE_ORDERS=true`, never clears the kill switch,
and never changes `ORDER_MODE` to live.**

---

## 3. Related gates (also non-live)

| Script | Role |
|--------|------|
| `scripts/grok/check-trading-safety.sh` | Broader fail-closed scan (defaults, env.example, forbidden patterns) |
| `scripts/grok/check-owner-readiness.sh` | Required harness + docs present (includes live-readiness script + this doc) |
| `scripts/grok/check-secrets.sh` | No committed secrets |
| `scripts/grok/dev-loop.sh` | Dev verification loop; includes safety + live-readiness; **not** a live order loop |
| `scripts/grok/verify.sh` | Full harness verify; live remains blocked at end |

---

## 4. How to collect evidence per checklist section

Use this map when filling `LIVE_READINESS_CHECKLIST.md`. Store artifacts under
`artifacts/` (never commit secrets). Prefer redacted logs.

### A. Environment / security

| Checklist item | How to collect |
|----------------|----------------|
| .NET SDK + `dotnet test` | `dotnet --info`; `bash scripts/grok/dev-loop.sh` or `verify.sh` output |
| `.env` untracked | `git status` / `git check-ignore -v .env`; `.gitignore` entry |
| Secret scan | `bash scripts/grok/check-secrets.sh`; optional gitleaks/trivy |
| No secret/account in logs | Unit tests (`SecretRedactor`, `AuditMessageRedactor`); sample log review |

### B. Toss connection (read-only)

| Checklist item | How to collect |
|----------------|----------------|
| Official OpenAPI snapshot | `artifacts/openapi/toss-openapi.snapshot.json` + `check-toss-openapi-diff.sh` |
| OAuth / read-only | Mock fixtures under tests; later sandbox session log (redacted) |
| accounts / holdings / prices / calendar | Integration or mock tests + read-only dashboard screenshot (no secrets) |
| rate limit / errors | Tests + Toss error model notes in `docs/TOSS_OPENAPI_NOTES.md` |
| redaction | Redactor tests green |

### C. Risk / orders

| Checklist item | How to collect |
|----------------|----------------|
| Risk gate rules + tests | `TradingBot.Risk.Tests` + decision audit samples |
| Dry-run stable | Dry-run ledger snapshots; multi-session run logs |
| Paper trading period | Paper ledger export; duration and PnL **not** a profit claim — ops stability only |
| Manual approval UX | Cockpit walkthrough notes (`docs/cockpit/OWNER_WALKTHROUGH.md`) |
| Idempotency / duplicate guard | Tests + sample keys in audit (no live submit) |
| Live impl still disabled | `check-live-readiness.sh` + `check-trading-safety.sh` both pass with `LIVE_READY=false` |

### D. UX / operations

| Checklist item | How to collect |
|----------------|----------------|
| Live lock / kill switch clear | Cockpit UI review; `LiveLock` / safety strip screenshots |
| Owner understands state | Owner walkthrough completion note |
| Incident response drill | Dated rehearsal against `docs/INCIDENT_RESPONSE.md` |
| Owner live-approval signature | Written form (date, conditions) — **not** created by this automation |

### E. Final gate (all required)

Evidence for section E is **not** automated to “true” by these scripts.  
Each line in the final gate block must have independent proof.  
Any single failure → **block**.  
Default config remains:

```text
ALLOW_LIVE_ORDERS=false
KILL_SWITCH=true
ORDER_MODE=dry_run
```

---

## 5. Integration with dev loop / verify

- `dev-loop.sh` and `verify.sh` call `check-live-readiness.sh`.
- A **pass** contributes to “dev gates green” while printing `LIVE_READY=false`.
- A **fail** with `broken` is a safety stop — do not weaken the gate to “fix” tests.
- Opening live requires owner phase-7 process outside this automation (see checklist E + `docs/OWNER_PLAYBOOK.md`).

---

## 6. Explicit non-goals

- Do not treat `LIVE_READINESS_STATUS=blocked_as_expected` as live permission.
- Do not flip source defaults or `.env.example` to live “for testing”.
- Do not add `SubmitOrderAsync` under `src` until live implementation is explicitly approved and gated.
- Do not commit tokens, account numbers, or raw `.env`.

---

## 7. Quick runbook

```bash
# From repo root (or active worktree)
bash scripts/grok/check-live-readiness.sh
# Expect: LIVE_READY=false and exit 0 while safety intact

bash scripts/grok/check-trading-safety.sh
bash scripts/grok/check-owner-readiness.sh
bash scripts/grok/dev-loop.sh   # development only; not live trading
```

**Bottom line:** Evidence collection is automated for *blocking integrity*.  
Live trading remains **not ready** until human checklist A–E is fully evidenced and owner-approved.
