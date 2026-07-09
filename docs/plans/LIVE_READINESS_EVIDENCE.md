# Live Readiness Evidence — How Evidence Is Collected

**Status: LIVE REMAINS BLOCKED (`LIVE_READY=false`)**  
**Date baseline: 2026-07-09**  
**Worktree snapshot: `feature/pw05-phase6` (docs-only Phase 6 map)**

This document explains **how** readiness evidence is collected and verified, and maps  
**current codebase artifacts** to [`docs/LIVE_READINESS_CHECKLIST.md`](../LIVE_READINESS_CHECKLIST.md).

It does **not** authorize live orders. Until every checklist item has reproducible evidence  
**and** the owner signs phase-7 approval, live is impossible.

---

## 0. Owner snapshot (한국어)

| 항목 | 값 |
|------|-----|
| 실거래 가능? | **아니오** |
| 기계 게이트 | `check-live-readiness.sh` → `LIVE_READY=false` + exit 0 (정상 차단) |
| 안전 스캔 | `check-trading-safety.sh` PASSED |
| 단위 테스트 | `dotnet test` 실패 0 (2026-07-09, 이 worktree) |
| 주문 API | **미구현** · `SubmitOrderAsync` 없음 · `BlockedLiveOrderRouter` / `BlockedTossOrderClient` |
| 다음 | paper 기간 증거 · (선택) 읽기 스모크 · 오너 Phase 7 승인 전 개방 금지 |

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
| .NET SDK + `dotnet test` | **Evidence** | `dotnet --info` (SDK 10.x) · `dotnet test TradingBot.sln` · or `bash scripts/grok/dev-loop.sh` |
| `.env` untracked | **Evidence** | `git check-ignore -v .env` · `.gitignore` lines `.env` / `.env.*` · `check-secrets.sh` |
| Secret scan | **Evidence** | `bash scripts/grok/check-secrets.sh` |
| gitleaks/trivy | **Partial** | `verify.sh` runs if installed, **non-fatal**. Install: see `docs/MACOS_SETUP.md` |
| No secret/account in logs | **Evidence (unit)** | `tests/TradingBot.Domain.Tests/TradingSafetyDefaultsTests.cs` (`SecretRedactor`) · `tests/TradingBot.Observability.Tests/AuditMessageRedactorTests.cs` · `DomainTossRedactor` |

### B. Toss connection (read-only)

| Checklist item | Status | How / where |
|----------------|--------|-------------|
| Official OpenAPI snapshot | **Evidence** | `artifacts/openapi/toss-openapi.snapshot.json` (v **1.2.2**) · notes: `docs/TOSS_OPENAPI_NOTES.md`, `docs/TOSS_SPEC_SNAPSHOT.md` |
| Snapshot freshness process | **Partial** | `scripts/grok/fetch-toss-openapi-spec.sh` + `check-toss-openapi-diff.sh` — re-run and attach log when refreshing |
| OAuth / mock | **Evidence** | `tests/.../Fixtures/oauth_token.json` · `MockTossClients` · `LiveTossHttpClientTests` stub token path |
| Live HTTP clients (read) | **Code + stub tests** | `src/TradingBot.Infrastructure.Toss/Http/LiveTossAuthClient.cs` · `LiveTossAccountClient.cs` · `LiveTossMarketDataClient.cs` · factory `TossReadOnlyFactory` |
| Live HTTP guard | **Evidence** | `LiveHttpGuard` · `TOSS_ALLOW_LIVE_HTTP` default false · tests in `ReadOnlyPortfolioServiceTests`, `LiveTossHttpClientTests` |
| accounts / holdings / prices / calendar | **Mock evidence** | Fixtures under `tests/TradingBot.Infrastructure.Toss.Tests/Fixtures/` · `ReadOnlyPortfolioServiceTests` · `TossDtoMapperTests` |
| No order HTTP on read path | **Evidence (stub)** | `LiveTossHttpClientTests` asserts paths exclude `orders` |
| Order client disabled | **Evidence** | `BlockedTossOrderClient` · `BlockedOrderClientTests` · safety scan `IsLiveSubmissionEnabled => false` |
| Real sandbox smoke | **Missing ops log** | Owner-local only: set credentials in `.env`, `TOSS_ALLOW_LIVE_HTTP=true`, run read-only smoke, save **redacted** log to `artifacts/` — never commit tokens |
| rate limit / errors | **Partial** | Documented notes; dedicated retry/rate-limit tests still thin |
| redaction | **Evidence** | Domain + Observability redactor tests |

### C. Risk / orders

| Checklist item | Status | How / where |
|----------------|--------|-------------|
| Risk gate rules + tests | **Partial (solid unit core)** | `src/TradingBot.Risk/RiskGate.cs` · `LiveOrderGate.cs` · `UsMarketSessionGuard.cs` · tests: `OrderCandidateRiskTests`, `LiveOrderGateTests`, `UsMarketSessionGuardTests` |
| Dry-run stable (unit) | **Evidence** | `DryRunOrderRouter` · `InMemoryDryRunLedger` · `tests/TradingBot.Orders.Tests/DryRunLedgerTests.cs` · `OrderRouterTests` |
| Paper ledger (unit) | **Evidence** | `PaperOrderRouter` · `InMemoryPaperLedger` · `PaperLedgerTests` · phase note `docs/plans/PHASE_05_paper.md` |
| Evidence aggregation | **Evidence** | `EvidenceBuilder` · `EvidenceBuilderTests` asserts `LiveModePresent == false` |
| Multi-day paper ops | **Missing** | Need dated paper ledger export / session notes under `artifacts/` (no secrets, no profit claims) |
| Manual approval UX | **Partial** | Gate flag `ManualApprovalPresent` · UI: `LiveLock.razor`, safety strip — **no live unlock control** (by design) |
| Idempotency / duplicate | **Partial / missing guard** | `OrderCandidate.ClientOrderId` generated in `OrderCandidatePipeline` · no dedicated duplicate-submit store/tests yet |
| Live impl still disabled | **Evidence** | `BlockedLiveOrderRouter` · `LiveOrderGate` + `LiveImplementationEnabled=false` · `check-live-readiness.sh` + `check-trading-safety.sh` · no `SubmitOrderAsync` in `src` |

### D. UX / operations

| Checklist item | Status | How / where |
|----------------|--------|-------------|
| Live lock / kill switch clear | **Evidence (UI code + unit)** | `src/TradingBot.Ui/CockpitSnapshot.cs` (`IsLiveTradingVisuallyOpen` requires all open flags) · Web `Components/Pages/LiveLock.razor` · `SafetyStrip.razor` · Avalonia projector messages · `TradingBot.Ui.Tests` |
| Owner understands state | **Partial** | `docs/cockpit/OWNER_WALKTHROUGH.md` · Korean owner copy — needs owner completion note with date |
| Incident response drill | **Missing date** | Procedure: `docs/INCIDENT_RESPONSE.md` — add dated rehearsal record when done |
| Owner live-approval signature | **Missing** | Not created by automation; required only for Phase 7 |

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
| risk gate pass | Core unit rules; full live limits incomplete |
| dry-run pass | Unit OK |
| paper trading stable | Unit OK; multi-day missing |
| valid Toss credential | Local owner secret; no committed smoke |
| account reconciled | Not evidenced |
| limits ok | Partial Max* settings |
| duplicate guard pass | Not evidenced |
| idempotency key present | ClientOrderId field only |
| audit log enabled | `InMemoryAuditLog` unit |
| operator confirms live | No signature |

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
| `tests/TradingBot.Domain.Tests` | Fail-closed defaults · secret mask |
| `tests/TradingBot.Risk.Tests` | LiveOrderGate blocks · candidate risk · session guard |
| `tests/TradingBot.Orders.Tests` | Dry-run / paper ledgers · blocked live router · evidence builder never live |
| `tests/TradingBot.Infrastructure.Toss.Tests` | Mock portfolio · live HTTP guard · stub read path without orders · blocked order client |
| `tests/TradingBot.Observability.Tests` | Audit redaction |
| `tests/TradingBot.Ui.Tests` / `Web.Tests` / `App.Tests` / `Runner.Tests` | Cockpit/harness surfaces still show blocked live |

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

---

## 7. Explicit non-goals

- Do not treat `LIVE_READINESS_STATUS=blocked_as_expected` as live permission.
- Do not flip source defaults or `.env.example` to live “for testing”.
- Do not add `SubmitOrderAsync` under `src` until live implementation is explicitly approved and gated.
- Do not commit tokens, account numbers, or raw `.env`.
- Do not claim multi-day paper stability from unit tests alone.
- Do not claim real Toss connectivity from mock/stub tests alone.

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

**Bottom line:** Evidence collection is automated for *blocking integrity*.  
Live trading remains **not ready** until human checklist A–E is fully evidenced and owner-approved.
