# Phase 4 — Dry-run ledger (감사 기록)

**상태:** scaffold 구현 (2026-07-09)  
**돈 위험:** 없음 — **실주문 없음**, Toss order HTTP 미호출  
**투자 조언 아님:** dry-run 기록은 파이프라인·감사 검증용

## 목표

1. dry-run 라우트가 수락한 후보를 **원장(ledger)** 에 남긴다  
2. 감사/리플레이 기반 (메모리 구현)  
3. live path 기본 차단 유지  
4. 파이프 안정 시나리오 테스트  

## 구현 (Agent A)

| 항목 | 위치 |
|------|------|
| `DryRunLedgerEntry` | `src/TradingBot.Domain/` |
| `IDryRunLedger` | `src/TradingBot.Orders/` |
| `InMemoryDryRunLedger` | `src/TradingBot.Orders/` |
| `DryRunOrderRouter` + optional ledger inject | `src/TradingBot.Orders/` |
| 단위 테스트 | `tests/TradingBot.Orders.Tests/DryRunLedgerTests.cs` |

## 하지 않음

- Toss order create/modify/cancel API  
- 영속(DB/파일) ledger  
- paper trading 포지션 시뮬레이션 (Phase 5)  
- live unlock / 실주문  

## 통과 기준 (요약)

- dry-run accept 시 ledger append  
- mode = DryRun, live 아님  
- `dotnet test` Orders.Tests 통과  
- 기존 live 차단 동작 유지  

## 다음

- Runner/cockpit에 ledger 스냅샷 연결 (UI 비변경 범위면 Application 어댑터)  
- Phase 5 paper ledger (별도 타입, dry-run과 혼동 금지)  
