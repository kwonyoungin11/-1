# Phase 5 — Paper trading ledger (가상 체결)

**상태:** scaffold 시작 (2026-07-09)  
**돈 위험:** 없음 — **실주문 없음**, Toss order HTTP 미호출  
**투자 조언 아님:** paper fill은 연습·시뮬레이션 검증용

## 목표

1. order candidate를 **가상 체결(fill)** 로 paper ledger에 남긴다  
2. 체결가 = limit 또는 reference price (네트워크 없음)  
3. mode = `Paper` — live 절대 비활성  
4. dry-run ledger와 **타입 분리** (혼동 금지)

## 구현 (Agent A)

| 항목 | 위치 |
|------|------|
| `PaperFillRecord` | `src/TradingBot.Domain/` |
| `IPaperLedger` | `src/TradingBot.Orders/` |
| `InMemoryPaperLedger` | `src/TradingBot.Orders/` |
| `PaperOrderRouter` | `src/TradingBot.Orders/` |
| 단위 테스트 | `tests/TradingBot.Orders.Tests/PaperLedgerTests.cs` |

## 동작 요약

- `PaperOrderRouter` → `IOrderRouter`  
- 수락 시 `PaperFillRecord` append (symbol, side, qty, price, filledAt, clientOrderId, note)  
- price: `LimitPrice` 우선, 없으면 optional reference resolver  
- price 없으면 reject (Accepted=false) but Mode still `Paper`, live 아님  
- 기존 `DryRunOrderRouter` / `BlockedLiveOrderRouter` 유지

## 하지 않음

- Toss order create/modify/cancel API  
- 영속(DB/파일) paper ledger  
- 포지션/PnL 전체 시뮬레이션 (후속)  
- live unlock / 실주문  
- Risk / UI / Runner 연결 (본 에이전트 범위 밖)

## 통과 기준 (요약)

- paper accept 시 fill append  
- 다건 fill 순서 보존  
- mode = Paper, live 아님  
- `dotnet test tests/TradingBot.Orders.Tests` 통과  

## 다음

- Runner에 `OrderMode.Paper` 분기 + paper ledger 스냅샷  
- cockpit paper ledger 화면 (UI 별 작업)  
- paper 포지션 / cash 잔고 모델  
