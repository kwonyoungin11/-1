# Parallel Wave 02 — 2026-07-09

Owner choice: paper trading + US session guard + Runner/DI cleanup.

## Streams

### A — paper trading
- Paths: `src/TradingBot.Domain/` (paper types only), `src/TradingBot.Orders/` (paper ledger/router only; keep DryRun), `tests/TradingBot.Orders.Tests/`, `docs/plans/PHASE_05_paper.md`
- Avoid: Risk/**, Observability/**, Ui/**, Runner/**

### B — US market session guard
- Paths: `src/TradingBot.Risk/**`, `tests/TradingBot.Risk.Tests/**`, optional `src/TradingBot.Domain/ReadOnly/` small types only if needed
- Avoid: Orders/**, Ui/**, Runner/**, Observability/**

### C — Runner / DI
- Paths: `src/TradingBot.Runner/**`, optional `src/TradingBot.Application/DependencyInjection*.cs` or Infrastructure extension for mock registration
- Avoid: Risk core logic, Orders ledger internals (only consume), Ui models

## Constraints
- No live orders / no Toss order HTTP
- Live defaults stay blocked
- No secrets in logs
