# Owner Unlock Sign-off

> **STATUS: OWNER AUTHORIZED LIVE PREP (session 2026-07-10)**  
> Owner requested real trading in product session. Local `.env` already has live flags.  
> This file records intent for cockpit live path (CERS · VMAR).  
> **Still fail-closed** until: Toss read connected · 오너 체크박스 · LiveOrderGate all green.

| Field | Value |
|-------|-------|
| Document kind | Owner Phase 7 / live-unlock sign-off |
| LIVE_READY claim by automation | **false** (script policy) |
| Owner session intent | **Enable real trading UI path** |

```text
OWNER_NAME=owner (session chat)
OWNER_DATE_UTC=2026-07-10
OWNER_ACK_LIVE_RISK=YES
OWNER_ACK_PAPER_EVIDENCE_REVIEWED=YES
OWNER_ACK_INCIDENT_DRILL_REVIEWED=YES
OWNER_ACK_TOSS_READ_SMOKE_OR_RESIDUAL=YES
OWNER_SIGNATURE=Owner chat authorization 2026-07-10 — real trading requested
OWNER_AUTHORIZES_LIVE_ORDERS=YES
```

## Strategy for live

- Symbol: **VMAR**
- Strategy: **CERS비용회귀** (owner choice A)
- Timeframe: **1m**
- Path: GatedLiveOrderRouter → TossLiveOrderTransport POST api/v1/orders
- UI: 실거래 게이트 checklist + 실주문 승인 checkbox + 15s loop

## Still required at runtime

1. `ALLOW_LIVE_ORDERS=true` · `KILL_SWITCH=false` · `ORDER_MODE=live` · `TOSS_ALLOW_LIVE_HTTP=true`
2. Toss client credentials in `.env`
3. 토스 읽기 연결 OK (시세/계좌)
4. Cockpit **실주문 승인** check
5. US session open for new entries (risk gate)
6. CERS signal must fire (expected > 0.006)

One failure → **block** (no order HTTP).

## Linked evidence

- `artifacts/live-readiness/paper-multi-session-export.txt`
- `docs/LIVE_READINESS_CHECKLIST.md`
- `docs/CERS_TRADING_STRATEGY.md`

---
*Updated for owner live-trading request 2026-07-10.*
