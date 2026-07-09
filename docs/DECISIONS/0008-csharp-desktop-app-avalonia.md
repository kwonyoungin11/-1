# ADR 0008: C# desktop app form (Avalonia)

## Status
Accepted — 2026-07-09 (owner: must be C# app form)

## Context
Owner requires product to be developed as a **C# application**, not browser-only.
MacBook Pro is the primary device. Blazor Web remains optional secondary host.

## Decision
- Primary UI host: **Avalonia desktop app** (`src/TradingBot.App`) — runs as native desktop window on macOS (and Windows/Linux).
- Reuse existing Domain / Application / Risk / Orders / Ui models and mock harness.
- Blazor `TradingBot.Web` kept as secondary/dev dashboard, not the only UI.

## Consequences
- Owner launches desktop window via `dotnet run --project src/TradingBot.App`.
- Packaging to `.app` / installer can follow later; app form is desktop executable, not website-only.
- Live orders remain blocked.
