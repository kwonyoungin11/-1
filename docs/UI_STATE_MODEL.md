# UI State Model

**Phase 1 갱신:** 2026-07-09  
**원칙:** unknown / missing / stale / error → UI는 **blocked** 표현. allow 추정 금지.

## BotLifecycleState (봇이 지금 하는 일)

| State | 오너 문구 예 | Live |
|-------|--------------|------|
| Bootstrapping | 시작 중 | locked |
| HarnessReady | 기본 준비 완료 | locked |
| AwaitingReadOnlyConnect | 토스 읽기 연결 대기 | locked |
| ReadOnlyConnected | 계좌·시세 읽기 가능 | locked |
| SignalComputing | 전략 계산 중 | locked |
| RiskEvaluating | 리스크 검사 중 | locked |
| DryRunActive | 연습 실행 중 | locked |
| PaperActive | 가상 체결 중 | locked |
| LiveLocked | 실거래 잠김 (기본) | locked |
| LiveReadyPendingApproval | 실거래 자격 대기·승인 필요 | locked |
| LiveArmed | (미래) 무장 — 여전히 게이트 | gated |
| Degraded | 일부 장애 | locked |
| Error | 오류 — 거래 차단 | locked |
| KillSwitchActive | 긴급 정지 | locked |

## SafetyStrip (홈 상단 고정)

| Field | 의미 | 기본 |
|-------|------|------|
| LiveLock | 실주문 경로 잠금 | Locked |
| KillSwitch | 긴급 정지 | On |
| OrderMode | dry_run / paper / live | dry_run |
| AllowLiveOrders | 설정 플래그 | false |

## RiskGateRow

| Field | 설명 |
|-------|------|
| Code | 기계용 코드 |
| Title | 짧은 제목 |
| OwnerMessage | 쉬운 말 |
| Passed | true/false |
| Severity | info / warning / block |

## OrderCandidateCard

| Field | 설명 |
|-------|------|
| Symbol, Side, Qty, LimitPrice | 후보 내용 |
| ClientOrderId | 중복 방지 id |
| Status | Proposed / RiskBlocked / DryRunOk / ... |
| IsLive | **항상 false until Phase 7+** |

## NextActionCard

오너가 할 일 1~3개. 예:

- “지금은 할 일 없음 — 시스템이 안전하게 대기 중”  
- “토스 읽기 연결 승인 필요”  
- “경고 확인: 시세 지연”  

## CockpitViewModel (구현 projection)

코드: `TradingBot.Ui.CockpitSnapshot`  
홈 한 장에 필요한 필드만 담는다. UI 프레임워크와 독립.

## 규칙

1. SafetyStrip 없이 홈 렌더 금지 (구현 시)  
2. Live unlock CTA는 readiness 전 비활성 + 설명  
3. 색만으로 위험 전달 금지 — OwnerMessage 필수  
4. 비밀·계좌 원문 필드 금지 (masked only)  
