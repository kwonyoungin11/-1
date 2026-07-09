# ADR 0006: Owner goal — live trading + user-centric UI

## Status
Accepted — 2026-07-09 (owner statement)

## Context
Owner clarified project intent:
1. The purpose is **real trading (live)**.
2. UI/UX must be **user-centered** (non-developer owner first).

## Decision
- Destination = live trading with Toss Open API (NASDAQ).
- dry-run / paper / risk gates / audits are the **path to live**, not a permanent substitute.
- UI is an owner cockpit: status, risk, blocked reasons, approvals, kill switch — not a developer console.
- Live remains closed until readiness evidence exists (fail-closed). Opening live is an explicit later milestone, not abandoned.

## Consequences
- Roadmap prioritizes features that unlock safe live: read-only truth, risk, paper proof, then live.
- UX docs and implementation prioritize owner comprehension over technical density.
- Marketing/profit-guarantee language still forbidden; software safety remains mandatory.
