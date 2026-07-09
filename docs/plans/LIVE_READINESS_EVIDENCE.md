# Live Readiness Evidence — How Evidence Is Collected

**Status: LIVE REMAINS BLOCKED under defaults (`LIVE_READY=false` · not auto-live)**  
**Owner unlock: `LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock`** (ops capture set present under `artifacts/live-readiness/`)  
**Date baseline: 2026-07-09**  
**Worktree snapshot: `feature/parallel-wave-base` (WAVE 07 integrated)**

This document explains **how** readiness evidence is collected and verified, and maps  
**current codebase artifacts** to [`docs/LIVE_READINESS_CHECKLIST.md`](../LIVE_READINESS_CHECKLIST.md).

It does **not** authorize live orders. Until every checklist item has reproducible evidence  
**and** the owner signs phase-7 approval, live is impossible.

**Honesty line:** A live-capable **code path** may exist only **behind gates**. Defaults block.  
Owner must set flags. Default launch stays dry-run. Scripts never auto-true `LIVE_READY`.  
`ready_for_owner_unlock` is **not** auto-live.

---

## 0. Owner snapshot (한국어)

| 항목 | 값 |
|------|-----|
| 실거래 기본 가능? | **아니오** (`LIVE_READY=false` · auto-live 없음) |
| 오너 언락 상태 | **`ready_for_owner_unlock`** (ops 캡처 세트 있음 · 기본 실행은 차단) |
| Pre-live **code** | **완료** (Phase 0–5 + gated live router + evaluator + export) |
| Phase 6 **ops capture** | **첨부됨** (`artifacts/live-readiness/*`) · multi-calendar-day real ops / 실 토스 스모크 / 오너 실서명 = 별도 residual |
| Live-capable path | **GATED** — `GatedLiveOrderRouter` · defaults block · owner must set flags · no auto-live |
| 기계 게이트 | `check-live-readiness.sh` → `LIVE_READY=false` + `ready_for_owner_unlock` |
| 안전 스캔 | `check-trading-safety.sh` PASSED |
| 단위 테스트 | `dotnet test` 실패 0 (wave-base) |
| 주문 경로 | 게이트형 `GatedLiveOrderRouter` + transport · 기본 harness는 dry-run/paper |
| 증거 폴더 | `artifacts/live-readiness/` — **캡처 세트 존재** |
| 다음 | 오너 실서명 · env 플래그 · (선택) 실 읽기 스모크 · 기본 실행은 계속 dry-run |

---

## 0a. Live-capable path is GATED

| Principle | Meaning |
|-----------|---------|
| Defaults block | `ALLOW_LIVE_ORDERS=false`, `KILL_SWITCH=true`, `ORDER_MODE=dry_run` in source, `.env.example`, harness |
| Owner must set flags | Live consideration requires **owner-local** env/flag changes — never agent/script auto-flip |
| Gated path ≠ open | Even if a live code path is later present, it stays behind LiveOrderGate + flags + risk |
| Default launch | Always dry-run / blocked under repository defaults |
| No auto-live | No process self-promotes to live when evidence is “enough” |
| Script never auto-trues `LIVE_READY` | `check-live-readiness.sh` prints `LIVE_READY=false` while safety defaults hold; it does **not** set env flags or claim live |

```text
LIVE_READY=false                 # automation expectation under defaults — never auto-true
LIVE_SAFETY_INTACT=true          # defaults still fail-closed
LIVE_READINESS_STATUS=blocked_as_expected
LIVE_OWNER_UNLOCK_STATUS=...     # separate from LIVE_READY (see §0d)
```

---

## 0d. LIVE_OWNER_UNLOCK_STATUS meanings

Human/ops readiness for **owner to consider unlock** — **not** permission to trade live.

| Value | Meaning | Auto-live? | Default launch | Owner action |
|-------|---------|------------|----------------|--------------|
| `blocked_missing_evidence` | Required ops/owner artifacts missing or incomplete | No | dry-run · blocked | Collect evidence under `artifacts/live-readiness/` |
| `ready_for_owner_unlock` | Agreed evidence set is present so owner **may review** unlock | **No** | **still dry-run · blocked** | Manual flags + Phase 7 signature + gate E; process does not self-enable live |
| (unset) | Not evaluated yet | No | dry-run · blocked | Run readiness evaluation after artifacts exist |

### Do not confuse

| Phrase | Honest reading |
|--------|----------------|
| `LIVE_READY=false` | Live not authorized; expected under defaults |
| `LIVE_READINESS_STATUS=blocked_as_expected` | Safety gate green **because** live is still blocked |
| `LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence` | Do not invite owner unlock yet |
| `LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock` | Evidence enough to **discuss** unlock — **not** live on, **not** auto-live |
| “code path available when gates open” | Implementation may exist later behind gates; **default launch still dry-run** |

**Current status:** `LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock`  
(`artifacts/live-readiness/` capture set present).  
**`LIVE_READY=false` under defaults (never auto-live).**

---

## 0b. Pre-live development complete (code)

Honest split: **engineering finished for practice + gated live path** vs **residual owner/live flags**.

| Done in code (do not re-label as “dev incomplete”) | Evidence anchors |
|---------------------------------------------------|------------------|
| Safety defaults fail-closed | `TradingSafetyDefaults.cs` · `.env.example` · harness hard-codes |
| Mock Toss read portfolio path | `MockTossClients` · fixtures · `ReadOnlyPortfolioServiceTests` |
| Gated live HTTP read clients (stub-tested) | `LiveTossAuth/Account/MarketDataClient` · `LiveHttpGuard` · `LiveTossHttpClientTests` |
| Gated live order path | `GatedLiveOrderRouter` · `ILiveOrderTransport` · `RecordingLiveOrderTransport` · `GatedLiveOrderRouterTests` |
| No unguarded live submit | no free `SubmitOrderAsync` · defaults block · practice = dry-run/paper |
| Strategy signals + order candidates | `StrategyCatalog` · `OrderCandidatePipeline` · Application tests |
| Risk / LiveOrderGate unit core | `RiskGate` · `LiveOrderGate` · `UsMarketSessionGuard` · Risk.Tests |
| Dry-run unit | `DryRunOrderRouter` · `InMemoryDryRunLedger` · `DryRunLedgerTests` |
| Paper **unit** (not multi-day ops) | `PaperOrderRouter` · `InMemoryPaperLedger` · `PaperLedgerTests` · `EvidenceBuilderTests` |
| Idempotency **field + store (dry-run/paper)** | `ClientOrderIdFactory` + `ClientOrderIdIndex` · `ClientOrderIdempotencyTests` |
| Evidence export **code** | `TradingEvidenceExporter` · `OpsEvidenceWriter` · `live_orders=false` |
| Live readiness evaluator | `LiveReadinessEvaluator` · `LiveReadinessEvaluatorTests` |
| Cockpit live lock / kill UI | `CockpitSnapshot` · Avalonia safety strip · Ui tests |
| Audit redaction | Domain + Observability redactor tests |
| OpenAPI 1.2.2 snapshot on disk | `artifacts/openapi/toss-openapi.snapshot.json` |
| Readiness automation | `check-live-readiness.sh` → `LIVE_READY=false` + unlock token |

**Code conclusion:** Pre-live development + gated live capability is **complete**.  
**Owner unlock review is available** (`ready_for_owner_unlock`). Default launch still cannot live-trade.

---

## 0c. Still blocks **default live trading** (ops residual / owner — not missing unlock capture)

Unlock **capture set is present**. These rows are residual risks / owner actions — **not** “no artifacts”.

| Blocker | Status | Notes |
|---------|--------|-------|
| **Multi-session export** under `artifacts/live-readiness/` | **Present** (`paper-multi-session-export.txt`) | **≠ multi-calendar-day real ops** |
| **Multi-calendar-day real ops** log | **Missing** (residual) | Separate from multi-session export |
| Owner-approved redacted Toss read smoke | Residual doc present · **real smoke log absent** | `toss-read-smoke-residual.md` · optional real log |
| OpenAPI re-fetch / drift ops log | **Present** (`openapi-recheck.log`) | Unlock capture OK |
| gitleaks/trivy as hard gate | **Optional / not hard** | `verify.sh` non-fatal today |
| Duplicate-submit / idempotency **store (practice)** | **Implemented** | Gated live reuses index pattern |
| Account reconcile + full live limits | **Not evidenced** (residual) | Beyond pre-live unit Max* |
| Incident drill **date** | **Present** (`incident-drill-record.md` 2026-07-09) | Unlock capture OK |
| Owner walkthrough completion note | **Missing date** (residual) | Doc exists |
| Owner Phase 7 signature | **UNSIGNED template** (`owner-unlock-signoff.md`) | Human must replace template |
| Final gate E all true simultaneously | **Not met** (defaults block — intentional) | Owner must set flags |
| Owner env unlock flags | **Defaults block** | Owner must set; scripts never flip |

**LIVE_READY stays false** under defaults (script never auto-trues).  
**LIVE_OWNER_UNLOCK_STATUS = `ready_for_owner_unlock`** because capturable artifacts exist.

### Naming rule: multi-session export ≠ multi-calendar-day real ops

| Term | Use when | Do **not** claim |
|------|----------|------------------|
| **multi-session export** | Several dry-run/paper sessions aggregated via `TradingEvidenceExporter` (or equivalent) into `artifacts/live-readiness/multi-session-export.*` | “multi-day production ops complete”, “N calendar days stable” |
| **multi-calendar-day real ops** | Owner actually operated paper across **calendar dates** with dated notes under `artifacts/live-readiness/multi-day-ops-*.md` | Implied by multi-session export alone |
| unit tests | `dotnet test` in-memory ledgers | Either of the above |

If only a multi-session export is attached, checklist language must say **multi-session export**, not multi-day real ops.

---

## 0e. Capturable ops artifacts — `artifacts/live-readiness/`

Store **redacted** ops evidence here (never secrets). Empty directory / missing files ⇒ **no ops evidence**.

| File (recommended / observed) | Source / how to produce | Proves | Does **not** prove |
|-------------------------------|-------------------------|--------|---------------------|
| `paper-multi-session-export.txt` (or `multi-session-export.json`/`.txt`) | `TradingEvidenceExporter` and/or ops generator over dry-run+paper sessions | **Multi-session export**; `live_orders=false` | Multi-calendar-day real ops |
| `paper-session-notes-YYYYMMDD.md` | Owner/operator notes per session | Session context (no profit guarantees) | Calendar-day stability alone |
| `multi-day-ops-YYYYMMDD-YYYYMMDD.md` | Only after real multi-calendar-day paper | Multi-calendar-day real ops | Anything if file absent |
| `toss-read-smoke-redacted.log` | Owner-local real read smoke (`TOSS_ALLOW_LIVE_HTTP=true` only) | Real read connectivity (no orders) | Live order safety |
| `toss-read-smoke-residual.md` | Residual-risk note when real smoke not run | Mock path residual documentation | Real Toss smoke complete |
| `openapi-recheck.log` / `openapi-reverify-YYYYMMDD.log` | fetch + diff scripts | Snapshot freshness ops | Live trading |
| `incident-drill-record.md` / `incident-drill-YYYYMMDD.md` | Tabletop via `docs/INCIDENT_RESPONSE.md` | Drill date + kill-switch confirmation | Live unlock |
| `owner-walkthrough-signoff.md` | Owner cockpit walkthrough | Owner understanding dated | Live permission |
| `owner-unlock-signoff.md` | Owner Phase 7 / unlock form | Only after **human signature** | If still `UNSIGNED TEMPLATE` → no unlock |
| `phase7-owner-approval.md` | Owner-written only | Phase 7 consent | Auto-generated text |
| `gate-e-snapshot-YYYYMMDD.txt` | Operator gate E snapshot | Simultaneous-gate record (aliases only) | Auto-live |

**Current (wave-base):** capturable set **present** under `artifacts/live-readiness/` ⇒ unlock **`ready_for_owner_unlock`**.  
Treat `paper-multi-session-export.txt` as **multi-session export** only; treat unsigned `owner-unlock-signoff.md` as template (not Phase 7 consent); treat residual smoke as **not** real connectivity evidence.

---

## 1. What “evidence” means

| Kind | Meaning | Example |
|------|---------|---------|
| Machine gate | Script exit 0 with explicit status lines | `LIVE_READY=false`, `LIVE_SAFETY_INTACT=true` |
| Unlock status | Ops completeness for owner review | `LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock` (capture set present) |
| Automated test | `dotnet test` assertion that would fail if safety weakened | `TradingSafetyDefaultsTests` |
| Artifact | Timestamped log, multi-session export, OpenAPI snapshot | `artifacts/live-readiness/`, `artifacts/openapi/` |
| Owner sign-off | Written approval with date and conditions | live transition approval form |

Passing a machine gate **never** means “open live”.  
The live-readiness automation gate is designed to **pass while live is blocked**.  
`ready_for_owner_unlock` **never** means auto-live.

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

**Important:** Under defaults, **`LIVE_READY` remains false always** from this script’s perspective.  
The script **never auto-trues** `LIVE_READY`, never sets `ALLOW_LIVE_ORDERS=true`, never clears kill switch, never sets `ORDER_MODE=live`.  
Owner env configuration for a future unlock is **outside** this script and still does not rewrite repository defaults.

`LIVE_OWNER_UNLOCK_STATUS` is evaluated from **ops artifacts + checklist honesty** (see §0d–0e), not from “dev-loop green” alone.

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
`artifacts/live-readiness/` (never commit secrets). Prefer redacted logs.

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
| Snapshot freshness process | **Capture present** | `artifacts/live-readiness/openapi-recheck.log` (+ scripts) |
| OAuth / mock | **Evidence (code)** | fixtures · `MockTossClients` · stub token path tests |
| Live HTTP clients (read) | **Code + stub tests** | `LiveToss*Client` · `TossReadOnlyFactory` |
| Live HTTP guard | **Evidence (code)** | `LiveHttpGuard` · `TOSS_ALLOW_LIVE_HTTP` default false |
| accounts / holdings / prices / calendar | **Mock evidence (code)** | Fixtures · `ReadOnlyPortfolioServiceTests` · `TossDtoMapperTests` |
| No order HTTP on read path | **Evidence (stub)** | `LiveTossHttpClientTests` excludes `orders` |
| Order client disabled / gated | **Evidence (code)** | `BlockedTossOrderClient` · `GatedLiveOrderRouter` · safety scan |
| Real sandbox smoke | **Residual** | `toss-read-smoke-residual.md` present · redacted real smoke log optional |
| rate limit / errors | **Partial** | Notes; dedicated tests thin |
| redaction | **Evidence (code)** | Domain + Observability |

### C. Risk / orders

| Checklist item | Status | How / where |
|----------------|--------|-------------|
| Risk gate core + tests | **Evidence (pre-live code)** | `RiskGate` · `LiveOrderGate` · `UsMarketSessionGuard` · Risk.Tests |
| Full live limits / reconcile | **Missing ops/design** | Still blocks claiming live-ready limits |
| Dry-run stable (unit) | **Evidence (code)** | `DryRunOrderRouter` · `DryRunLedgerTests` · `OrderRouterTests` |
| Paper ledger (unit) | **Evidence (code)** | `PaperOrderRouter` · `PaperLedgerTests` · `PHASE_05_paper.md` |
| Evidence aggregation | **Evidence (code)** | `EvidenceBuilder` · `LiveModePresent == false` |
| **Multi-session export** | **Present** | `artifacts/live-readiness/paper-multi-session-export.txt` — **≠ multi-calendar-day real ops** |
| **Multi-calendar-day real ops** | **Missing (residual)** | Separate dated notes only if calendar-day ops actually ran |
| Manual approval UX | **Partial** | Flag + lock UI; no auto unlock (by design) |
| Idempotency field | **Present (code)** | `ClientOrderIdFactory` in pipeline |
| Idempotency / duplicate store | **Present** | `ClientOrderIdIndex` + gated live reuse |
| Live impl gated | **Evidence (code)** | `GatedLiveOrderRouter` · defaults block · readiness + safety scripts |

### D. UX / operations

| Checklist item | Status | How / where |
|----------------|--------|-------------|
| Live lock / kill switch clear | **Evidence (code + unit)** | `CockpitSnapshot` · Web/Avalonia safety surfaces · Ui.Tests |
| Owner understands state | **Partial (owner residual)** | `OWNER_WALKTHROUGH.md` — optional dated completion note |
| Incident response drill | **Present** | `artifacts/live-readiness/incident-drill-record.md` (2026-07-09) |
| Owner live-approval signature | **UNSIGNED template** | `owner-unlock-signoff.md` — human must replace (Phase 7) |

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
| KILL_SWITCH=false | **Blocked** (default true) — owner must set |
| ALLOW_LIVE_ORDERS=true | **Blocked** (default false) — owner must set |
| ORDER_MODE=live | **Blocked** (default dry_run) — owner must set |
| manual approval exists | Flag only; ops flow incomplete |
| session open | Unit guard only |
| market data fresh | Unit staleness rule only |
| risk gate pass | Pre-live core unit rules |
| dry-run pass | Unit OK; multi-session export ops missing |
| paper trading stable | Unit OK; **multi-session export / multi-day missing** |
| valid Toss credential | Local owner secret; **no committed smoke** |
| account reconciled | Not evidenced |
| limits ok | Partial Max* settings |
| duplicate guard pass | Dry-run/paper evidenced (`ClientOrderIdempotencyTests`) |
| idempotency key present | Factory + index on practice routers |
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
| `tests/TradingBot.Orders.Tests` | Dry-run / paper ledgers · blocked live router · evidence export never live |
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
| Evidence export | `src/TradingBot.Orders/TradingEvidenceExporter.cs` | Multi-session export payload (`live_orders=false`) |
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
- **Pre-live code complete** + **dev-loop green** still means **live blocked under defaults**; with capture set present unlock may be **`ready_for_owner_unlock`**.
- `ready_for_owner_unlock` (if ever set after ops artifacts) still means **default launch dry-run**, not auto-live.

---

## 7. Explicit non-goals

- Do not treat `LIVE_READINESS_STATUS=blocked_as_expected` as live permission.
- Do not treat `LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock` as auto-live or `LIVE_READY=true`.
- Do not flip source defaults or `.env.example` to live “for testing”.
- Do not add unguarded `SubmitOrderAsync` under `src` until live implementation is explicitly approved and gated.
- Do not commit tokens, account numbers, or raw `.env`.
- Do not claim multi-calendar-day real ops from unit tests alone.
- Do not claim multi-calendar-day real ops from **multi-session export** alone — name the substitute correctly.
- Do not claim real Toss connectivity from mock/stub tests alone.
- Do not mark multi-day paper, owner Phase 7 signature, or real Toss smoke as done when artifacts/signatures are absent.
- Do not reclassify remaining ops/owner blockers as “development incomplete” when the practice pipeline code already exists.
- Do not claim scripts auto-true `LIVE_READY` after owner configures env — repository automation expectation under defaults remains **false**.

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
5. Save redacted log under artifacts/live-readiness/toss-read-smoke-redacted.log
```

### Multi-session export (ops evidence — not multi-day real ops)

```text
1. Run paper/dry-run sessions only (never live)
2. Use TradingEvidenceExporter (or ops generator) to dump JSON/text
3. Save as one of:
   artifacts/live-readiness/paper-multi-session-export.txt
   artifacts/live-readiness/multi-session-export.json
   artifacts/live-readiness/multi-session-export.txt
4. Confirm export contains live_orders=false and LiveSubmissionEnabled=false
5. Label checklist as "multi-session export" — do NOT claim multi-calendar-day real ops
6. Optional separate multi-day-ops-*.md only if calendar-day operation actually occurred
7. owner-unlock-signoff.md may exist as UNSIGNED TEMPLATE — that is not Phase 7 approval
```

### Owner unlock path (still not auto-live)

```text
1. LIVE_OWNER_UNLOCK_STATUS may become ready_for_owner_unlock only after agreed artifacts exist
2. LIVE_READY remains false under defaults; scripts do not auto-true it
3. Owner sets flags locally only after Phase 7 signature + gate E review
4. Default product launch remains dry-run even when code path can open under gates
5. Any single gate failure → block
```

**Bottom line:** Pre-live **code** + **ops capture set** are in place.  
Live trading under **defaults** remains **not open** (`LIVE_READY=false`).  
Owner unlock status is **`ready_for_owner_unlock`**.  
Phase 7: gated path available **when owner sets flags + signs**; **default launch still dry-run**. Auto-live is **never** claimed.
