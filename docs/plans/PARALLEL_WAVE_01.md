# Parallel Wave 01 — 2026-07-09

## Streams

### A — dry-run ledger (Phase 4 start)
- Paths: `src/TradingBot.Orders/`, `src/TradingBot.Domain/` (audit types only), `tests/TradingBot.Orders.Tests/`, `docs/plans/PHASE_04_dry_run.md`
- Do not touch: Observability/*, Ui/*, Infrastructure.Toss/*

### B — observability
- Paths: `src/TradingBot.Observability/**`, `tests/` only if Observability.Tests created
- Do not touch: Orders/*, Ui/*, Infrastructure.Toss/*

### C — cockpit UI models
- Paths: `src/TradingBot.Ui/**`, `tests/TradingBot.Ui.Tests/**`, `docs/cockpit/**` (additive)
- Do not touch: Orders/*, Observability/*, Infrastructure.Toss/*

## Shared constraints
- No live orders, no Toss order HTTP
- ALLOW_LIVE_ORDERS remains false by default
- No .env secrets in output
- Run unit tests for owned projects
