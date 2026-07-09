# CERS 앱 통합 플랜 (TDD · 실매매 가능 경로)

**투자 조언 아님. 백테스트 ≠ 미래 수익. Live 기본 차단 · fail-closed.**

| 항목 | 값 |
|------|-----|
| 작성 브랜치 | `feature/pw14-cers-plan` |
| 오너 한 장 | [`docs/CERS_TRADING_STRATEGY.md`](../CERS_TRADING_STRATEGY.md) |
| 방법론 | [`docs/BACKTEST_CUSTOM_CERS.md`](../BACKTEST_CUSTOM_CERS.md) |
| 연구 리포트 | `artifacts/vmar_cers_6m_backtest_report.md` |
| 백테스트 수학 소스 | `TradingBot.Backtesting` · `CustomMathIndicators.Cers` / `CersStrategy` |

**목표:** 백테스트 1위 `CERS` (연구 구간: **+738.1% / MDD 51.7% / 556거래** · VMAR 1m · 과거 시뮬) 설정을 **그대로**  
도메인 계산 → 신호 → 브래킷 → 차트 → UI → dry-run / paper / **gated live** 에 연결한다.

**실매매 가능의 의미:** 게이트·체크리스트·오너 승인 통과 시 `GatedLiveOrderRouter`로 **동일 후보**가 나갈 수 있게 배선한다.  
**기본값을 live로 바꾸지 않는다.** fail-closed.

---

## 0. 오너가 알아야 할 한 줄

> “가격이 단기 평균보다 충분히 아래로 빠졌고, 비용(약 0.3% 왕복)의 2배 이상 여지로 보이면 롱 후보를 만들고,  
> 손절 1.2% · 익절(진입 시 expected×1.5) · 평균 복귀 · 40봉 시간청산으로 나간다.  
> 후보는 dry-run/paper부터. 실주문은 잠금이 풀리기 전까지 불가능.”

UI 카피·상태 문구는 오너 문서와 동일 톤을 쓴다. 수익 보장 문구 금지.

---

## 1. 백테스트와 동일한 세밀 설정 (단일 소스)

앱·도메인·백테스트가 **같은 숫자**를 써야 한다. 도메인 `CersPreset`이 SSOT가 되고, 백테스트 `CersStrategy` 상수와 패리티 테스트로 고정한다.

| 키 | 값 | 용도 |
|----|-----|------|
| `EmaPeriod` | **21** | 평균 기준선 |
| `RsiPeriod` | **14** | 과매도 가산 |
| `AtrPeriod` | **14** | 정규화 변동(진단) |
| `VolSmaPeriod` | **20** | 거래량 z |
| `AutocorrWindow` | **30** | κ (평균회귀 강도) |
| `FeeRatePerSide` | **0.001** (0.1%) | 비용 모델 |
| `SlippageRatePerSide` | **0.0005** (0.05%) | 비용 모델 |
| `RoundTripCost` | **0.003** = 2×(f+s) | 임계 기준 |
| `ThresholdMultiple` | **2.0** | 진입: expected > 2×C_rt |
| `EntryThreshold` | **0.0060** | = 2.0 × 0.003 |
| `StopLossPct` | **0.012** (1.2%) | 손절 |
| `TakeProfitExpectedMultiple` | **1.5** | TP% = entryExpected × 1.5 |
| `MaxHoldBars` | **40** | 시간 청산 |
| `Timeframe` | **1m** (`ChartTimeframe.분봉1`) | 신호 봉 |
| `Side` | **Long only** | 숏 없음 |
| `Fill model (sim)` | next open ± slip | 백테스트; 실주문은 토스 LIMIT |
| `Cooldown after exit` | **3 bars** | 재진입 쿨다운 (엔진/세션) |

공식 (도메인 `CersMath.ComputeExpectedEdge` ← `CustomMathIndicators.Cers` 패리티):

```text
mu = EMA(close, 21)
dev = max(0, (mu - close) / close)
kappa = clamp(1 + rho_lag1(returns, 30), 0.25, 2.0)
rsi_boost = max(0, (35 - RSI14) / 35)
vol_z = clip((vol - SMA20(vol)) / stdev20(vol), 0, 3)
quality = 0.45 + 0.35*rsi_boost + 0.20*min(1, vol_z/2)
expected = kappa * dev * quality * (0.85 + 0.15*lower_wick)
enter if expected > 2.0 * 0.003          // thr = 0.0060
exit if ret <= -1.2%
     OR ret >= expected_entry * 1.5
     OR close >= EMA21
     OR hold >= 40
cooldown 3 bars after exit
```

**주의 (오너·구현 공통):**

- 연구 리포트 체결 = **다음 봉 시가 ± 슬리피지**. 라이브 LIMIT은 체결 지연·미체결·부분체결이 다름.
- VMAR 연구 구간은 **급락장** 롱 스캘프 시뮬. 다른 종목·다른 구간에서 깨질 수 있음.
- 백테스트 1위 ≠ live readiness 통과.

---

## 2. 아키텍처 (Clean)

```text
Domain
  TradingStrategyKind.CERS비용회귀          (또는 기존 Kind 확장)
  CersPreset (상수·OwnerSummary·Version)
  CersMath (expected 시리즈 · 순수 함수)
  CersEvaluator (봉 단위 진입/청산 상태기)
  CersBracketPlanner (LIMIT entry / SL / TP)
  (선택) CersPosition 스냅샷 값 객체

Application
  CersSignalGenerator : IStrategySignalGenerator
  PracticeStrategyContext.Candles + CersPosition?
  StrategySignalRouter + OrderCandidatePipeline 배선
  (실행중 루프) 후보 → dry-run / paper / GatedLiveOrderRouter

App (Avalonia)
  전략 콤보에 CERS비용회귀
  차트: EMA21 + CERS expected 범례/워터마크 + ENTRY/SL/TP
  하단 패널: expected, threshold, 상태, SL/TP, max hold, 비용
  VMAR 기본 연습 프리셋 → CersPreset (1m)  [오너 추천 기본]

Risk / Orders / Observability
  기존 RiskGate · LiveOrderGate · ClientOrderId · Audit 유지
  CERS 후보는 다른 전략과 동일 게이트
  기본: ALLOW_LIVE_ORDERS=false · KILL_SWITCH=true · ORDER_MODE=dry_run
```

### 2.1 기존 코드와의 관계

| 기존 | 역할 | CERS 통합 시 |
|------|------|----------------|
| `CustomMathIndicators.Cers` | 백테스트 expected | Domain `CersMath`와 **수치 패리티** |
| `CersStrategy` | 백테스트 진입/청산 | Domain `CersEvaluator`와 **규칙 패리티** |
| `VmarOneMinuteScalpPreset` / 분할스캘프 | UI 연습 경로 | **별개 전략**. CERS 선택 시 덮어쓰지 않음 |
| `SpacexOfficialStrategyPreset` | SPCX 추세 | 종목 전환 시 프리셋 분리 유지 |
| `OrderCandidatePipeline` | 후보 생성 | CERS generator 브랜치 추가 |
| `GatedLiveOrderRouter` | 실주문 경로 | 기본 차단; 게이트 전부 통과 시에만 |

### 2.2 금지

- 백테스트 thr/SL/TP를 UI 편의로 느슨하게 바꾸기
- live를 기본 ON으로 바꾸기
- RiskGate / LiveOrderGate 우회 전용 경로
- 시크릿·계좌번호 로그
- “수익 보장 / 백테스트대로 돈 벌어줌” UI 카피

---

## 3. 병렬 TDD 웨이브 (구역 · 겹침 금지)

구현은 **별 worktree 에이전트**가 담당. 이 문서는 계약서다. 각 웨이브: **실패 테스트 → 최소 구현 → 통과 → 리팩터.**

| Wave | Zone (경로) | RED tests first | 산출물 |
|------|-------------|-----------------|--------|
| **A Domain** | `src/TradingBot.Domain/Strategy/`, `tests/TradingBot.Domain.Tests/` | preset 상수 = 백테스트; math 패리티; evaluator enter/exit/SL/TP/cooldown | `CersPreset`, `CersMath`, `CersEvaluator`, `CersBracketPlanner` |
| **B Application** | `src/TradingBot.Application/Strategy/`, `tests/TradingBot.Application.Tests/` | Buy only when edge; pipeline with candles; no signal without candles; Hold on NaN | `CersSignalGenerator`, router/pipeline 배선 |
| **C Chart** | Domain chart helpers + App chart builder / tests | `ForStrategy(CERS)` → EMA21 + CERS line + bracket overlays | 지표 시리즈 바인딩 모델 |
| **D Harness** | `src/TradingBot.App/Services/`, App tests | `SetStrategy(CERS)` → 1m; bracket SL 1.2%; panel labels; live lock 유지 | 프리셋 전환 · 패널 텍스트 |
| **E UI** | `Views/`, `ViewModels/`, styles | bindings: expected/threshold/state; 전략 옵션 표시; 카피 가이드 준수 | 콤보·상태줄·지정가 카드 |

### 3.1 의존 순서

```text
A Domain  ──►  B Application  ──►  D Harness
      │                │
      └────► C Chart ──┴──►  E UI
```

A 없이 B/E 금지. C는 A의 시리즈 계약만 있으면 병렬 가능.

### 3.2 검증 명령 (웨이브 완료 게이트)

```bash
dotnet test tests/TradingBot.Domain.Tests -c Release --filter Cers
dotnet test tests/TradingBot.Application.Tests -c Release --filter Cers
dotnet test tests/TradingBot.App.Tests -c Release --filter Cers
# 선택: 백테스트 패리티 (Domain math == CustomMathIndicators.Cers)
# 통합 전: bash scripts/grok/dev-loop.sh  (safety BLOCK 우회 금지)
```

---

## 4. UI / 차트 체크리스트 (구현 완료 기준)

- [ ] 전략 드롭다운: **`CERS비용회귀`** (표시명 고정)
- [ ] 선택 시 설명(오너 톤):  
      `비용인식 평균회귀 · 1분봉 · 문턱=왕복비용×2(0.60%) · 손절 1.2% · 실주문 기본 차단`
- [ ] 차트 오버레이: **EMA21**, CERS expected(선택 표시), 진입/손절/익절 점선(브래킷)
- [ ] 상태 줄 예:  
      `CERS expected=0.0072 thr=0.0060 · 진입가능` / `관망` / `보유 12/40봉`
- [ ] 지정가 카드:  
      Entry · SL = entry×**0.988** · TP = entry×(1 + expected_entry×1.5)
- [ ] 시작 버튼: dry-run/paper 또는 (게이트 통과 시에만) gated live — **기존 잠금 라벨 유지**
- [ ] 킬스위치·실주문 잠금 필 유지 (`ALLOW_LIVE_ORDERS=false` 기본)
- [ ] 봉/시세 없음·stale·API 오류 → **신호 없음 + 차단 사유 표시** (fail-closed)

카피 가이드: [`docs/COPYWRITING_GUIDE.md`](../COPYWRITING_GUIDE.md)

---

## 5. 주문 후보 · 브래킷 계약

| 단계 | 동작 |
|------|------|
| 신호 없음 | 후보 0 · UI `관망` |
| 진입 조건 | expected > 0.0060 · long only · 쿨다운 아님 · 세션 허용 |
| 진입 후보 | LIMIT buy @ 신호 기준가(정책: 종가 또는 mid — Domain에 명시) |
| SL | entry × (1 − 0.012) |
| TP | entry × (1 + entryExpected × 1.5) |
| 청산 신호 | SL/TP 터치 가설 + close≥EMA21 + hold≥40 → 매도 후보 |
| ClientOrderId | 기존 factory · 전략 접두 `cers-` 권장 |
| 라우팅 | dry_run → paper → (게이트) live |

실호가·부분체결은 백테스트와 다름을 UI 힌트에 한 줄로 명시.

---

## 6. 안전

| 조건 | 동작 |
|------|------|
| 시세/봉 없음 · stale | 신호 없음 · 주문 차단 |
| expected NaN / thr 미달 | Hold |
| Symbol warning / daily loss / kill / session | 기존 게이트 |
| live readiness 미충족 | live 경로 불가 |
| 시크릿 | 로그·UI 노출 금지 |
| 카피 | 수익 보장 금지 · “후보 ≠ 체결” |

Live 게이트 전체: `docs/LIVE_READINESS_CHECKLIST.md` · `docs/DECISIONS/0004-live-order-gate-policy.md`  
기본 안전값:

```text
ALLOW_LIVE_ORDERS=false
KILL_SWITCH=true
ORDER_MODE=dry_run
```

---

## 7. 수락 기준 (Definition of Done)

1. **수치 패리티:** Domain expected 시리즈가 동일 캔들에서 `CustomMathIndicators.Cers`와 허용 오차 내 일치.
2. **규칙 패리티:** 고정 fixture 캔들에서 enter/exit 사유가 `CersStrategy`와 동일 패턴.
3. **앱 경로:** CERS 선택 → 1m 차트 · EMA21 · 브래킷 · 패널 expected/thr/상태.
4. **후보 경로:** 조건 충족 시 OrderCandidate 생성 → dry-run 또는 paper 장부 기록.
5. **차단 경로:** 봉 없음 / thr 미달 / kill / live 미승인 → **주문 없음**.
6. **문서:** 오너 한 장 [`docs/CERS_TRADING_STRATEGY.md`](../CERS_TRADING_STRATEGY.md)와 UI 라벨 불일치 없음.
7. **테스트:** 위 filter `Cers` 테스트 통과; 기존 테스트 삭제·약화 없음.
8. **Live 기본값 불변:** CreateDefault / harness 기본이 live-open이 아님.

---

## 8. 오너 결정 (기본 추천 적용)

| 항목 | 추천 | 이유 |
|------|------|------|
| VMAR 기본 전략 | **CERS비용회귀** | 백테스트 1위 연구 설정 이식 (연습 기본) |
| 기본 시간봉 | **1m** | 백테스트와 동일 |
| Live 기본 | **차단** | 헌법 · 증거 전 live 금지 |
| 실매매 | 게이트 전부 통과 시에만 | 동일 파이프라인 · 우회 없음 |
| 분할스캘프 연습 | 유지(별 전략) | CERS와 목적 다름 — 혼동 금지 |

오너 운영 시나리오: [`docs/cockpit/OWNER_WALKTHROUGH.md`](../cockpit/OWNER_WALKTHROUGH.md)

---

## 9. 관련 문서

| 문서 | 용도 |
|------|------|
| [`docs/CERS_TRADING_STRATEGY.md`](../CERS_TRADING_STRATEGY.md) | 오너용 콕핏 한 장 (이 플랜의 UI 계약) |
| [`docs/BACKTEST_CUSTOM_CERS.md`](../BACKTEST_CUSTOM_CERS.md) | 수식·6m 방법론 |
| [`docs/VMAR_1M_SPLIT_SCALP_PRACTICE.md`](../VMAR_1M_SPLIT_SCALP_PRACTICE.md) | 별개 연습 전략 |
| [`docs/TOSS_US_FEES.md`](../TOSS_US_FEES.md) | 비용 가정 |
| [`docs/LIVE_READINESS_CHECKLIST.md`](../LIVE_READINESS_CHECKLIST.md) | live 증거 |
| `artifacts/vmar_cers_6m_backtest_report.md` | 연구 성적 (과거 시뮬) |

---

*플랜 문서 완료 후 구현은 병렬 에이전트(domain / application / chart / harness / ui)가 TDD로 진행. 이 브랜치(pw14-cers-plan)는 docs only.*
