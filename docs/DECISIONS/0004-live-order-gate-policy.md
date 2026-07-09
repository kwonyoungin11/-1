# ADR 0004: Live order gate policy

## Status
Accepted — 2026-07-09

## Decision
Fail-closed LiveOrderGate. Defaults: ALLOW_LIVE_ORDERS=false, KILL_SWITCH=true, ORDER_MODE=dry_run. Live implementation flag defaults false.
