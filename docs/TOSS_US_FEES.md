# 토스증권 미국주식 수수료 (계획용)

**계좌·공지 요율이 우선.** 이벤트/협의 요율 가능. 투자 조언 아님.

## 공개 표준 (요약)

| 항목 | 요율 |
|------|------|
| 위탁 매수 | 체결대금 **0.1%** ($0.01 미만 절사) |
| 위탁 매도 | 체결대금 **0.1%** |
| SEC (매도) | 매도대금 약 **0.00206%**, 최소 약 **$0.01** (공시 변경) |
| $10 이하 | 주문 체결 **$10 이하** 시 위탁 0 (Open API 안내) |
| 환전 | 별도 (USD 잔고면 매매와 분리) |

왕복 위탁만 ≈ **0.2%** of notional.

## 앱 반영

`TossUsEquityCommissionSchedule` + 브래킷 `EstimatedCommissionUsd` / `NetRewardRiskRatio`.

## 확인

토스 앱 명세서·[업무안내 수수료](https://corp.tossinvest.com)·Open API 공지.
