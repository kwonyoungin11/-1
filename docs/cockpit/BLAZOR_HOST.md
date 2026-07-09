# Blazor Host (TradingBot.Web)

Read-only cockpit host for the automatic-trading UI. **Live orders are blocked by default.**

## Prerequisites

- .NET SDK **10.0** (see repo `global.json`)
- Repo root as working directory

## Run

```bash
dotnet run --project src/TradingBot.Web
```

Or from the project folder:

```bash
cd src/TradingBot.Web
dotnet run
```

Then open the HTTPS URL printed in the console (typically `https://localhost:7xxx` from `Properties/launchSettings.json`).

## Build only

```bash
dotnet build src/TradingBot.Web
```

## What this host does

| Area | Behavior |
|------|----------|
| Portfolio | **Mock** Toss read-only (`ReadOnlyPortfolioService.CreateMock`) |
| Safety | `AllowLiveOrders=false`, `KillSwitch=true`, `OrderMode=DryRun` |
| Orders | Dry-run + paper ledgers only; `BlockedLiveOrderRouter` for live path |
| HTTP | Toss order HTTP **not** registered; `TossOptions.AllowLiveHttp=false` |
| Secrets | Never bound to UI; no tokens/account numbers in logs or pages |

Composition lives in `TradingBot.Web.Services.WebHarness` (does **not** reference `TradingBot.Runner`).

## Project references

- TradingBot.Domain
- TradingBot.Application
- TradingBot.Infrastructure.Toss
- TradingBot.Risk
- TradingBot.Orders
- TradingBot.Observability
- TradingBot.Ui

## Owner notes

- Header always shows **실거래 잠김** / **긴급정지 ON** / **dry_run**.
- Home loads a mock cockpit dashboard; components from other agents can replace body content later.
- Do not enable live orders from this host without `docs/LIVE_READINESS_CHECKLIST.md` evidence and owner approval.

## Safety defaults (hard-coded in DI)

```text
ALLOW_LIVE_ORDERS=false
KILL_SWITCH=true
ORDER_MODE=dry_run
TOSS_ALLOW_LIVE_HTTP=false
```
