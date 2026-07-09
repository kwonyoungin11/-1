# Live Readiness Evidence — How Evidence Is Collected

**Status: LIVE REMAINS BLOCKED (`LIVE_READY=false`)**  
**Date baseline: 2026-07-09**  
**Worktree snapshot: `feature/pw06-readiness` (WAVE 06 — pre-live code vs ops honesty)**

This document explains **how** readiness evidence is collected and verified, and maps  
**current codebase artifacts** to [`docs/LIVE_READINESS_CHECKLIST.md`](../LIVE_READINESS_CHECKLIST.md).

It does **not** authorize live orders. Until every checklist item has reproducible evidence  
**and** the owner signs phase-7 approval, live is impossible.

---

## 0. Owner snapshot (한국어)

| 항목 | 값 |
|------|-----|
| 실거래 가능? | **아니오** (`LIVE_READY=false`) |
| Pre-live **code** | **완료** (Phase 0–5 엔지니어링 + mock/dry-run/paper unit/cockpit/safety) |
| Phase 6 **ops/owner** | **갭 남음** (다일 paper, 실 스모크, incident 날짜, Phase 7 서명 등) |
| 기계 게이트 | `check-live-readiness.sh` → `LIVE_READY=false` + exit 0 (정상 차단) |
| 안전 스캔 | `check-trading-safety.sh` PASSED |
| 단위 테스트 | `dotnet test` 실패 0 (2026-07-09, 이 worktree) |
| 주문 API | **미구현** · `SubmitOrderAsync` 없음 · `BlockedLiveOrderRouter` / `BlockedTossOrderClient` |
| 다음 | multi-day paper ops · (선택) redacted 읽기 스모크 · incident drill · 오너 Phase 7 전 개방 금지 |

---

## 0b. Pre-live development complete (code)

Honest split: **engineering finished for practice/read-only path** vs **ops that still block live**.

| Done in code (do not re-label as “dev incomplete”) | Evidence anchors |
|---------------------------------------------------|------------------|
| Safety defaults fail-closed | `TradingSafetyDefaults.cs` · `.env.example` · harness hard-codes |
| Mock Toss read portfolio path | `MockTossClients` · fixtures · `ReadOnlyPortfolioServiceTests` |
| Gated live HTTP read clients (stub-tested) | `LiveTossAuth/Account/MarketDataClient` · `LiveHttpGuard` · `LiveTossHttpClientTests` |
| No live order submit path | no `SubmitOrderAsync` · `BlockedTossOrderClient` · `BlockedLiveOrderRouter` |
| Strategy signals + order candidates | `StrategyCatalog` · `OrderCandidatePipeline` · Application tests |
| Risk / LiveOrderGate unit core | `RiskGate` · `LiveOrderGate` · `UsMarketSessionGuard` · Risk.Tests |
| Dry-run unit | `DryRunOrderRouter` · `InMemoryDryRunLedger` · `DryRunLedgerTests` |
| Paper **unit** (not multi-day ops) | `PaperOrderRouter` · `InMemoryPaperLedger` · `PaperLedgerTests` · `EvidenceBuilderTests` (`LiveModePresent=false`) |
| Idempotency **field present** | `OrderCandidate.ClientOrderId` generated in pipeline — **not** a duplicate-submit store |
| Cockpit live lock / kill UI | `CockpitSnapshot` · Web `LiveLock`/`SafetyStrip` · Avalonia safety strip · Ui tests |
| Audit redaction | Domain + Observability redactor tests |
| OpenAPI 1.2.2 snapshot on disk | `artifacts/openapi/toss-openapi.snapshot.json` |
| Readiness automation expects block | `check-live-readiness.sh` → `LIVE_READY=false` |

**Code conclusion:** Pre-live development for the safe practice pipeline is **complete**.  
Claiming “live ready” still requires the ops/owner section below — which is **not** done.

---

## 0c. Still blocks live (ops/owner — not "dev incomplete")

Do **not** mark these complete without dated artifacts / owner action.

| Blocker | Status | Notes |
|---------|--------|-------|
| Multi-day paper ops log | **Missing** | Unit paper ≠ period stability. Need redacted ledger export under `artifacts/` |
| Owner-approved redacted Toss read smoke | **Missing** | Optional for daily work; **required before claiming live-ready connectivity**. Never commit tokens |
| OpenAPI re-fetch / drift ops log | **Missing log** | Scripts exist; attach dated re-verify when run |
| gitleaks/trivy as hard gate | **Optional / not hard** | `verify.sh` non-fatal today |
| Duplicate-submit / idempotency **store** | **Not implemented** | `ClientOrderId` field only — design before any live submit |
| Account reconcile + full live limits | **Not evidenced** | Beyond pre-live unit Max* |
| Incident drill **date** | **Missing** | Procedure doc only: `docs/INCIDENT_RESPONSE.md` |
| Owner walkthrough completion note | **Missing date** | Doc exists; no owner signature/date |
| Owner Phase 7 signature | **Absent (intentional)** | Automation must never invent this |
| Final gate E all true simultaneously | **Not met** | Defaults remain blocked |

**LIVE_READY stays false** until these are evidenced **and** owner Phase 7 process completes.  
Multi-day paper, real Toss smoke, incident date, and Phase 7 signature are **explicitly not done**.

---

## 1. What “evidence” means

| Kind | Meaning | Example |
|------|---------|---------|
| Machine gate | Script exit 0 with explicit status lines | `LIVE_READY=false`, `LIVE_SAFETY_INTACT=true` |
| Automated test | `dotnet test` assertion that would fail if safety weakened | `TradingSafetyDefaultsTests` |
| Artifact | Timestamped log, ledger export, OpenAPI snapshot | dry-run ledger, paper fill log, `artifacts/openapi/` |
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

### Output (expected today — verified 2026-07-09)

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

| Script | Role | Live? |
|--------|------|-------|
| `scripts/grok/check-trading-safety.sh` | Defaults, `.env.example`, `IsLiveSubmissionEnabled => false`, no submit patterns | No |
| `scripts/grok/check-owner-readiness.sh` | Required harness + docs (includes this file + live script) | No |
| `scripts/grok/check-secrets.sh` | `.env` untracked, gitignore, example present | No |
| `scripts/grok/check-toss-openapi-diff.sh` | OpenAPI snapshot drift helper | No |
| `scripts/grok/fetch-toss-openapi-spec.sh` | Refresh official snapshot (ops) | No |
| `scripts/grok/dev-loop.sh` | Dev verification; includes safety + live-readiness | **Not** a live order loop |
| `scripts/grok/verify.sh` | Full harness; optional gitleaks/trivy (`|| true`) | Live remains blocked |

### Safety defaults source of truth

| Location | Values |
|----------|--------|
| `src/TradingBot.Domain/TradingSafetyDefaults.cs` | `AllowLiveOrders=false`, `KillSwitch=true`, `OrderMode=DryRun`, staleness 5s |
| `.env.example` | `ALLOW_LIVE_ORDERS=false`, `KILL_SWITCH=true`, `ORDER_MODE=dry_run`, `TOSS_ALLOW_LIVE_HTTP=false` |
| `src/TradingBot.Web/appsettings.json` | same fail-closed trading flags |
| App/Web/Runner harness wiring | hard-codes safe defaults at composition root |

---

## 4. Evidence map — checklist A–E → code / tests / scripts

Use this when updating `LIVE_READINESS_CHECKLIST.md`. Store ops artifacts under  
`artifacts/` (never commit secrets). Prefer redacted logs.

### A. Environment / security

| Checklist item | Status | How / where |
|----------------|--------|-------------|
| .NET SDK + `dotnet test` | **Evidence (code)** | `dotnet --info` (SDK 10.x) · `dotnet test TradingBot.sln` · or `bash scripts/grok/dev-loop.sh` |
| `.env` untracked | **Evidence (code)** | `git check-ignore -v .env` · `.gitignore` · `check-secrets.sh` |
| Secret scan | **Evidence (code)** | `bash scripts/grok/check-secrets.sh` |
| gitleaks/trivy | **Partial (ops/CI)** | `verify.sh` runs if installed, **non-fatal**. Install: see `docs/MACOS_SETUP.md` |
| No secret/account in logs | **Evidence (unit)** | `TradingSafetyDefaultsTests` · `AuditMessageRedactorTests` · `DomainTossRedactor` |

### B. Toss connection (read-only)

| Checklist item | Status | How / where |
|----------------|--------|-------------|
| Official OpenAPI snapshot | **Evidence (code artifact)** | `artifacts/openapi/toss-openapi.snapshot.json` (v **1.2.2**) · notes docs |
| Snapshot freshness process | **Partial (ops log missing)** | `fetch-toss-openapi-spec.sh` + `check-toss-openapi-diff.sh` — re-run and attach log |
| OAuth / mock | **Evidence (code)** | fixtures · `MockTossClients` · stub token path tests |
| Live HTTP clients (read) | **Code + stub tests** | `LiveToss*Client` · `TossReadOnlyFactory` |
| Live HTTP guard | **Evidence (code)** | `LiveHttpGuard` · `TOSS_ALLOW_LIVE_HTTP` default false |
| accounts / holdings / prices / calendar | **Mock evidence (code)** | Fixtures · `ReadOnlyPortfolioServiceTests` · `TossDtoMapperTests` |
| No order HTTP on read path | **Evidence (stub)** | `LiveTossHttpClientTests` excludes `orders` |
| Order client disabled | **Evidence (code)** | `BlockedTossOrderClient` · safety scan |
| Real sandbox smoke | **Missing ops/owner log** | Owner-local only; redacted log to `artifacts/` — **not done** |
| rate limit / errors | **Partial** | Notes; dedicated tests thin |
| redaction | **Evidence (code)** | Domain + Observability |

### C. Risk / orders

| Checklist item | Status | How / where |
|----------------|--------|-------------|
| Risk gate core + tests | **Evidence (pre-live code)** | `RiskGate` · `LiveOrderGate` · `UsMarketSessionGuard` · Risk.Tests |
| Full live limits / reconcile | **Missing ops/design** | Not multi-day paper; still blocks claiming live-ready limits |
| Dry-run stable (unit) | **Evidence (code)** | `DryRunOrderRouter` · `DryRunLedgerTests` · `OrderRouterTests` |
| Paper ledger (unit) | **Evidence (code)** | `PaperOrderRouter` · `PaperLedgerTests` · `PHASE_05_paper.md` |
| Evidence aggregation | **Evidence (code)** | `EvidenceBuilder` · `LiveModePresent == false` |
| Multi-day paper ops | **Missing (ops)** | **Do not mark done** without dated export under `artifacts/` |
| Manual approval UX | **Partial** | Flag + lock UI; no live unlock control (by design) |
| Idempotency field | **Present (code)** | `ClientOrderId` in candidate pipeline |
| Idempotency / duplicate store | **Missing** | No dedicated store/tests yet — not “multi-day paper” but still blocks live claims |
| Live impl still disabled | **Evidence (code)** | `BlockedLiveOrderRouter` · no `SubmitOrderAsync` · readiness + safety scripts |

### D. UX / operations

| Checklist item | Status | How / where |
|----------------|--------|-------------|
| Live lock / kill switch clear | **Evidence (code + unit)** | `CockpitSnapshot` · Web/Avalonia safety surfaces · Ui.Tests |
| Owner understands state | **Partial (owner)** | `OWNER_WALKTHROUGH.md` — needs dated owner completion note |
| Incident response drill | **Missing date (ops)** | `INCIDENT_RESPONSE.md` — **no rehearsal date recorded** |
| Owner live-approval signature | **Missing (owner Phase 7)** | Not created by automation |

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

| Final gate line | Current honest status |
|-----------------|----------------------|
| KILL_SWITCH=false | **Blocked** (default true) |
| ALLOW_LIVE_ORDERS=true | **Blocked** (default false) |
| ORDER_MODE=live | **Blocked** (default dry_run) |
| manual approval exists | Flag only; ops flow incomplete |
| session open | Unit guard only |
| market data fresh | Unit staleness rule only |
| risk gate pass | Pre-live core unit rules |
| dry-run pass | Unit OK |
| paper trading stable | Unit OK; **multi-day missing** |
| valid Toss credential | Local owner secret; **no committed smoke** |
| account reconciled | Not evidenced |
| limits ok | Partial Max* settings |
| duplicate guard pass | Not evidenced |
| idempotency key present | ClientOrderId field only |
| audit log enabled | `InMemoryAuditLog` unit |
| operator confirms live | **No Phase 7 signature** |

---

## 5. Test inventory (reproducible commands)

Run from repo / worktree root:

```bash
# Machine gates (expect LIVE_READY=false)
bash scripts/grok/check-live-readiness.sh
bash scripts/grok/check-trading-safety.sh
bash scripts/grok/check-owner-readiness.sh
bash scripts/grok/check-secrets.sh

# Unit / integration (in-memory + HTTP stubs — no real orders)
dotnet test TradingBot.sln --nologo
```

### Primary assemblies for readiness

| Project | What it proves |
|---------|----------------|
| `tests/TradingBot.Domain.Tests` | Fail-closed defaults · secret mask · strategy catalog |
| `tests/TradingBot.Application.Tests` | Candidate pipeline · strategy signals · auto-trade session |
| `tests/TradingBot.Risk.Tests` | LiveOrderGate blocks · candidate risk · session guard |
| `tests/TradingBot.Orders.Tests` | Dry-run / paper ledgers · blocked live router · evidence never live |
| `tests/TradingBot.Infrastructure.Toss.Tests` | Mock portfolio · live HTTP guard · stub read without orders · blocked order client |
| `tests/TradingBot.Observability.Tests` | Audit redaction |
| `tests/TradingBot.Ui.Tests` / `Web.Tests` / `App.Tests` / `Runner.Tests` | Cockpit/harness still show blocked live |

### Key source anchors (do not weaken)

| Component | Path | Role |
|-----------|------|------|
| Safety defaults | `src/TradingBot.Domain/TradingSafetyDefaults.cs` | Hard fail-closed constants |
| Live order gate | `src/TradingBot.Risk/LiveOrderGate.cs` | Multi-condition live eligibility (still blocks without impl) |
| Risk gate | `src/TradingBot.Risk/RiskGate.cs` | Candidate + session evaluation |
| Blocked live router | `src/TradingBot.Orders/BlockedLiveOrderRouter.cs` | Never calls Toss order APIs |
| Dry-run / paper | `src/TradingBot.Orders/DryRunOrderRouter.cs`, `PaperOrderRouter.cs` | Practice paths only |
| Toss order client | `src/TradingBot.Infrastructure.Toss/ITossClients.cs` (`BlockedTossOrderClient`) | `IsLiveSubmissionEnabled => false` |
| Live HTTP guard | `src/TradingBot.Infrastructure.Toss/Http/LiveHttpGuard.cs` | Blocks outbound unless owner flag |
| Mock clients | `src/TradingBot.Infrastructure.Toss/Mock/MockTossClients.cs` | Default read path |

---

## 6. Integration with dev loop / verify

- `dev-loop.sh` and `verify.sh` call `check-live-readiness.sh`.
- A **pass** contributes to “dev gates green” while printing `LIVE_READY=false`.
- A **fail** with `broken` is a safety stop — do not weaken the gate to “fix” tests.
- Opening live requires owner phase-7 process outside this automation (see checklist E + `docs/OWNER_PLAYBOOK.md`).
- **Do not confuse** `docs/DEV_LOOP.md` (engineering quality loop) with a live-trading loop.
- **Pre-live code complete** + **dev-loop green** still means **live blocked**.

---

## 7. Explicit non-goals

- Do not treat `LIVE_READINESS_STATUS=blocked_as_expected` as live permission.
- Do not flip source defaults or `.env.example` to live “for testing”.
- Do not add `SubmitOrderAsync` under `src` until live implementation is explicitly approved and gated.
- Do not commit tokens, account numbers, or raw `.env`.
- Do not claim multi-day paper stability from unit tests alone.
- Do not claim real Toss connectivity from mock/stub tests alone.
- Do not mark multi-day paper, owner Phase 7 signature, or real Toss smoke as done when artifacts/signatures are absent.
- Do not reclassify remaining ops/owner blockers as “development incomplete” when the practice pipeline code already exists.

---

## 8. Quick runbook

```bash
# From repo root (or active worktree)
bash scripts/grok/check-live-readiness.sh
# Expect: LIVE_READY=false and exit 0 while safety intact

bash scripts/grok/check-trading-safety.sh
bash scripts/grok/check-owner-readiness.sh
bash scripts/grok/check-secrets.sh
dotnet test TradingBot.sln --nologo
bash scripts/grok/dev-loop.sh   # development only; not live trading
```

### Optional read-only smoke (owner machine only)

```text
1. Copy .env.example → .env and fill Toss credentials locally (never commit)
2. TOSS_ALLOW_LIVE_HTTP=true  (read HTTP only)
3. ALLOW_LIVE_ORDERS stays false; KILL_SWITCH stays true; ORDER_MODE stays dry_run
4. Run read-only portfolio/quote path; confirm no order endpoints
5. Save redacted log under artifacts/ (no tokens/account numbers)
```

### Multi-day paper ops (still required before live claims)

```text
1. Agree owner paper period (calendar days / sessions)
2. Run paper path only (ORDER_MODE=paper or project paper mode; never live)
3. Export redacted ledger / session notes to artifacts/ with dates
4. No profit guarantees in logs or owner reports
5. Attach path in checklist C — only then mark multi-day paper evidenced
```

**Bottom line:** Pre-live **code** evidence is in place. Ops/owner evidence is **not**.  
Live trading remains **not ready** (`LIVE_READY=false`) until human checklist A–E ops items are fully evidenced and owner-approved. Phase 7 remains **forbidden**.
