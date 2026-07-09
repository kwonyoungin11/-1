# Parallel Wave 04 — Complete remaining non-live development

## Streams (exclusive zones)

### A — Blazor MVP pages (no Home.razor rewrite of host)
- Paths: `src/TradingBot.Web/Components/Pages/{BotState,LiveLock,Risk,Candidates,Account,Audit,Settings}.razor` only
- Bind `@inject WebHarness Harness`, load `GetDashboardAsync()`, owner Korean UI, no buy/sell

### B — Paper/dry-run evidence domain+tests
- Paths: `src/TradingBot.Domain/*Evidence*` or `PaperEvidence*`, `src/TradingBot.Orders/` evidence helpers only if needed, `tests/TradingBot.Orders.Tests/`, `docs/plans/PHASE_05_paper.md` update
- Avoid Web/**

### C — Live readiness automation
- Paths: `scripts/grok/check-live-readiness.sh`, `docs/LIVE_READINESS_CHECKLIST.md`, `docs/plans/LIVE_READINESS_EVIDENCE.md`, update `check-owner-readiness.sh` to list new script
- Avoid src/** business logic except docs

### D — WebHarness page facades
- Paths: `src/TradingBot.Web/Services/**` only — add methods for audit lines, paper/dry counts, safety strip DTOs for pages
- Avoid Pages/**

### E — Ui evidence view models + tests
- Paths: `src/TradingBot.Ui/**`, `tests/TradingBot.Ui.Tests/**` — EvidenceSummaryViewModel etc.
- Avoid Web/**

## Constraints
- Live still blocked; no Toss order HTTP
- Orchestrator merges and runs dev-loop
