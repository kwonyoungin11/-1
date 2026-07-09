# SPCX 지정가 · 익절 · 손절 (전문 트레이더 프레임)

**투자 조언 아님.** 실주문은 게이트 잠금. UI/Domain은 **계획 브래킷**만.

## 개념

| 용어 | 의미 | 본 앱 |
|------|------|--------|
| 지정가 진입 LIMIT | 원하는 가격 이하/이상만 체결 | `TradeBracketPlan.EntryLimit` |
| 손절 SL | 손실 한도 청산 가격 | ATR×1.5 또는 % 폴백 |
| 익절 TP | 목표 이익 지정 청산 | 손절 거리 × TakeProfitR (기본 2R) |
| R:R | 보상/위험 비율 | `RewardRiskRatio` |

## SPCX 기본 파라미터 (`SpacexRiskParameters`)

| 항목 | 기본 |
|------|------|
| 1회 리스크 | equity 1% |
| 손절 | ATR(14)×1.5 |
| 익절 | 2R |
| 지정가 오프셋 | ATR×0.1 (last 아래) |
| 폴백 손절 | 2% |

## 차트

- ENTRY 파랑 · SL 빨강 · TP 초록 수평선
- 수량·리스크$·R:R 패널
- 실주문 잠금 배지 유지

## 실주문 (미래)

토스 조건주문 OCO/OTO는 **오너 잠금 해제 후** 별도 웨이브. 현재 `GatedLiveOrderRouter` 미사용.
