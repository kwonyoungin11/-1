# Parallel Wave 03 — Blazor cockpit home skeleton

Owner choice: browser dashboard (Blazor) home skeleton, 3 agents.

## Streams

### A — Host project
- Create `src/TradingBot.Web/` Blazor Web App (.NET 10)
- Program.cs, appsettings, register mock services / harness
- Do NOT implement all components (stub layout only)
- Avoid: detailed Razor component UI (B/C)

### B — Cockpit components
- Under `src/TradingBot.Web/Components/Cockpit/`
- SafetyStrip, RiskGateList, OrderCandidateList, NextActionCard
- Bind to TradingBot.Ui view models; IsLive always false; no buy/sell execute
- Avoid: Program.cs, project create, Pages

### C — Pages & navigation
- Overview home page, layout nav for MVP 8 screens (stubs OK)
- Owner-safe copy only
- Avoid: host DI rewrite if A owns Program.cs — only Pages/Layout

## Constraints
- No live orders, no Toss order HTTP
- Live lock default locked
- No secrets on screen
- Korean owner-facing text preferred
