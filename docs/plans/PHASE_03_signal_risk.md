# Phase 3 — 전략 신호 + 리스크 게이트 (주문 후보)

**상태:** mock/scaffold 구현 (2026-07-09)  
**돈 위험:** 없음 — **실주문 없음**, dry-run 후보만  
**투자 조언 아님:** scaffold 신호는 교육·파이프라인 검증용

## 목표

1. 시세 → 전략 신호  
2. 신호 → 리스크 평가  
3. 통과 시 주문 **후보** (client order id 포함)  
4. 실패 시 차단 사유 코드  

## 구현

- `SimpleNasDaqSignalGenerator` — scaffold  
- `RiskGate.EvaluateOrderCandidate` — stale/missing/한도/장세션  
- `OrderCandidatePipeline` — 조합  
- Live path 기본 차단 유지  

## 하지 않음

- Toss order API  
- 수익 보장 문구  
- live unlock  
