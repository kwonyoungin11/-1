# Architecture Overview

Clean / Hexagonal structure under `src/`.

- **Domain**: money-safe types, defaults, redaction, candidates
- **Application**: use cases (scaffold)
- **Infrastructure.Toss**: official REST adapter (interfaces first)
- **Risk**: fail-closed gates
- **Orders**: dry-run default, blocked live stub
- **Backtesting / Observability / Ui / Runner**: scaffolds

Trading flow (target):

```text
Market/Account read → Signal → RiskGate → OrderCandidate → DryRun/Paper router
                                                      ↘ Live router (blocked)
```
