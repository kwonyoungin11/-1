# VMAR 15분봉 분할매수·분할매도 연습 (dry-run/paper)

**투자 조언 아님. 실주문 기본 차단.**

## 대상
- 종목: **VMAR** (비전 마린 테크놀로지 / Vision Marine Technologies)
- 전략: **일분분할스캘프**
- 시간봉: **15m**
- 경로: dry-run → paper 만 (live 불가)

## 동작
1. UI 종목 종류에서 **비전마린** 선택
2. 자동 설정: 심볼 VMAR · 전략 일분분할스캘프 · 시간봉 15m
3. 연습 시작 시 시세 기준 **총 수량**을 리스크 사이징 후 **3레그 지정가**로 분할
   - 매수: 기준가, 기준가×(1−0.1%), 기준가×(1−0.2%)
   - 매도: 대칭 상단 스텝
4. 각 레그는 서로 다른 ClientOrderId (idempotency)
5. 토스 미국주식 왕복 수수료 ~0.2% — 1분 단타는 비용이 큼 (연습 목적)

## 코드
- `VmarOneMinuteScalpPreset`
- `SplitOrderLegPlanner`
- `OneMinuteSplitScalpSignalGenerator`
- `OrderCandidatePipeline` multi-leg branch

## 안전
- ALLOW_LIVE_ORDERS=false
- KILL_SWITCH=true
- ORDER_MODE=dry_run
