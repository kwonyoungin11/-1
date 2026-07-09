# Cockpit 홈 바인딩 모델 (오너용)

**날짜:** 2026-07-09  
**범위:** 화면이 어떤 **데이터 상자**에 붙는지 설명. 실주문 버튼 없음.

---

## 한 줄 요약

홈 화면은 **`CockpitDashboardModel`** 하나를 보면 됩니다.

- 위쪽 안전 줄·연결·할 일 → `Snapshot` (`CockpitSnapshot`)
- 리스크 게이트 목록 → `RiskGates`
- 주문 후보 목록 → `OrderCandidates` (**항상 실주문 아님**)

---

## 상자 설명

| 바인딩 | 의미 | 기본(안전) |
|--------|------|------------|
| `Snapshot.LiveLock` | 실거래 잠금 | **Locked** |
| `Snapshot.KillSwitchActive` | 긴급 정지 | **true (ON)** |
| `Snapshot.OrderMode` | dry_run / paper / live | **dry_run** |
| `Snapshot.AllowLiveOrders` | 실거래 허용 플래그 | **false** |
| `Snapshot.SafetyHeadline` | 홈 상단 한 문장 | “실거래 잠김…” |
| `RiskGates[]` | 왜 막혔는지 줄 목록 | kill switch 등 차단 표시 |
| `OrderCandidates[]` | 주문 **후보** 줄 | 비어 있음 |
| `OrderCandidates[].IsLive` | 실주문 여부 | **항상 false** |

### RiskGate 한 줄

| 필드 | 설명 |
|------|------|
| `Code` | 기계용 코드 (예: `kill_switch_active`) |
| `Title` | 짧은 제목 (예: 긴급 정지) |
| `OwnerMessage` | 쉬운 말 설명 |
| `Passed` | 통과 여부 (false = 막힘) |
| `Severity` | info / warning / block |

### OrderCandidate 한 줄

| 필드 | 설명 |
|------|------|
| `Symbol` | 종목 |
| `Side` | BUY / SELL 등 (후보 방향) |
| `Quantity` | 수량 |
| `LimitPrice` | 지정가 (있으면) |
| `Status` | dry-run 허용 / risk 차단 등 |
| `IsLive` | **코드상 항상 false** — UI가 “실주문”으로 표시하면 안 됨 |

---

## 오너가 기억할 규칙

1. **후보 ≠ 주문.** 후보 목록에 종목이 보여도 실제 매수/매도가 아닙니다.  
2. **잠금이 기본.** Live Lock Locked + Kill Switch ON이면 실거래 불가.  
3. **홈에 “매수 실행 / 실주문” 버튼이 없습니다.** (의도된 설계)  
4. 데이터 없음·오류·지연 → **막힘 표시** (추측 허용 없음).  
5. 디자이너는 색·배치를 바꿔도, **IsLive=false / LiveLock=Locked 의미는 바꾸지 않습니다.**

---

## 코드 위치

| 파일 | 역할 |
|------|------|
| `src/TradingBot.Ui/CockpitDashboardModel.cs` | 홈 합성 모델 |
| `src/TradingBot.Ui/CockpitSnapshot.cs` | 안전 스트립·요약 |
| `src/TradingBot.Ui/RiskGateRowViewModel.cs` | 리스크 한 줄 |
| `src/TradingBot.Ui/OrderCandidateRowViewModel.cs` | 후보 한 줄 (`IsLive => false`) |
| `src/TradingBot.Ui/CockpitDashboardMapper.cs` | Domain → 화면 행 매핑 |

도메인 입력 예: `EvaluatedOrderCandidate`, `RiskDecision`, `BlockedReason`, `TradingSafetySettings`.

---

## 관련 문서

- `docs/UI_STATE_MODEL.md` — 상태·필드 정의  
- `docs/cockpit/WIREFRAME.md` — 화면 배치  
- `docs/LIVE_READINESS_CHECKLIST.md` — 실거래 전까지 live 불가  
