# CERS · 커스텀 지표 & 6개월 백테스트 방법론

**투자 조언 아님. 과거 시뮬레이션 ≠ 미래 수익. 백테스트는 실주문을 내지 않습니다.**

대상 독자: 비개발자 오너  
관련 코드 구역(구현 웨이브): `src/TradingBot.Backtesting/`, Runner `backtest` 서브커맨드  
기존 산출물 예시: `artifacts/vmar_6m_backtest_report.*`, `artifacts/vmar_1m_6m_backtest_report.*`  
연습 경로 교차: [`docs/VMAR_1M_SPLIT_SCALP_PRACTICE.md`](VMAR_1M_SPLIT_SCALP_PRACTICE.md)

---

## 1. CERS가 뭔가? (한 줄)

**CERS (Cost-aware Edge Reversion Score)** =  
“가격이 평균(기준선) 아래로 얼마나 빠졌는지”를 보고 **평균회귀(되돌림) 여지**를 점수로 만든 뒤,  
그 점수가 **왕복 거래비용보다 충분히 큰지** 검사하는 **비용 인식 평균회귀 점수**입니다.

- **Cost-aware**: 수수료·슬리피지를 빼고도 남을 만큼의 여지만 신호로 인정  
- **Edge**: “통계적으로 유리해 보이는 여지” (수익 보장 아님)  
- **Reversion**: 과하게 빠진 뒤 기준선 쪽으로 되돌아올 수 있다는 가설  
- **Score**: 0 근처면 무시, 높을수록 “비용 대비 여지”가 큼 (여전히 가설)

> 연구 리포트의 **MathEdge** (`edge > 2 × 왕복비용`)와 같은 계열입니다.  
> CERS는 그 아이디어를 **프로젝트 표준 이름·공식**으로 고정한 커스텀 지표입니다.

---

## 2. 수식 (오너용 쉬운 설명 + 정의)

### 2.1 기본 가정 (비용)

| 항목 | 기호 | 기본값 | 의미 |
|------|------|--------|------|
| 수수료 편도 | \(f\) | **0.1%** = 0.001 | 토스 미국주식 위탁 모델 근사 (`docs/TOSS_US_FEES.md`) |
| 슬리피지 편도 | \(s\) | **0.05%** = 0.0005 | 체결가 불리 가정 |
| 왕복 비용 | \(C_{rt}\) | \(2 \times (f + s)\) ≈ **0.30%** = **0.003** | 매수+매도 각 1회 |

SEC·환전 스프레드 등은 기본 모델에서 **제외**(보수적으로 해석할 때 실제 비용은 더 클 수 있음).

### 2.2 구성 요소 (롱 평균회귀 관점)

봉 \(t\)의 종가 \(P_t\), 기준 이동평균 \(\mathrm{EMA}_n\) (기본 **EMA21**), RSI, 거래량을 사용합니다.

1. **기준선 대비 하락 거리 (되돌림 여지)**  
   \[
   d_t = \max\!\left(0,\; \frac{\mathrm{EMA}_n - P_t}{P_t}\right)
   \]  
   - 가격이 EMA **아래**일 때만 양수 (롱 회귀 여지).  
   - 위에 있으면 0 → 롱 CERS 신호 없음.

2. **RSI 과매도 계수** \(R_t \in [0, 1]\)  
   - RSI가 낮을수록 1에 가깝게 (과매도일수록 회귀 가설 강화).  
   - 예: 과매도 기준 \(L=30\) 근처에서  
     \(R_t = \mathrm{clamp}\big((L - \mathrm{RSI}_t)/L,\; 0,\; 1\big)\).  
   - RSI가 높으면 0에 가까워져 점수 축소.

3. **거래량 계수** \(V_t \ge 0\)  
   - 최근 평균 대비 거래량(또는 z-score를 0 이상으로 스케일)으로 “이상 매도 압력 후”를 약하게 반영.  
   - 기본 구현은 1 근처 정규화; 급등 거래량일 때 소폭 가산.

4. **원시 엣지 (비용 전)**  
   \[
   E_t = d_t \times R_t \times V_t
   \]  
   단위: 가격 비율(대략 %). “지금 가격이 기준보다 얼마나 싸 보이고, 과매도·거래량이 그걸 뒷받침하는지”.

### 2.3 CERS 점수 (비용 인식)

\[
\mathrm{CERS}_t = \frac{E_t}{C_{rt}}
\quad\text{where}\quad
C_{rt} = 2(f + s)
\]

| CERS | 오너 해석 |
|------|-----------|
| \(< 1\) | 추정 여지가 **왕복 비용보다 작음** → 거래하면 비용에 먹힐 가능성 큼 → **미진입** |
| \(1 \sim 2\) | 비용과 비슷·약간 큼 → 기본적으로 **보수 미진입** (노이즈) |
| \(\ge 2\) (기본 임계값 \(k=2\)) | “엣지가 왕복 비용의 약 2배 이상” 가설 → **신호 후보** (실주문 아님) |

**진입 규칙 (백테스트 기본):**

\[
\text{Enter long candidate iff }\mathrm{CERS}_t \ge k,\quad k = 2
\]

이는 MathEdge의  
`edge > 2 × roundtrip_cost`  
와 **동일한 문턱**입니다: \(E_t \ge k \cdot C_{rt}\) ⇔ \(\mathrm{CERS}_t \ge k\).

### 2.4 청산 (백테스트 기본 스케치)

- **익절(TP)** / **손절(SL)**: 전략 프로필에 따름 (예: 고정 % 또는 ATR 배수).  
- **시간 청산**: 너무 오래 회귀 없으면 종료.  
- **쿨다운**: 청산 후 N봉(1분봉 연구 예: 3분) 재진입 금지.  
- 상세 파라미터는 리포트 JSON의 `assumptions` / 전략 이름을 따릅니다.

### 2.5 오너가 기억할 한 장 요약

```text
1) 가격이 EMA보다 많이 빠졌나?  → 거리 d
2) RSI가 과매도 쪽인가?         → 계수 R
3) 거래량이 이상 매도를 암시?   → 계수 V
4) 곱한 값 E = d × R × V
5) 왕복 비용 C ≈ 0.30% 로 나눔  → CERS = E / C
6) CERS ≥ 2 일 때만 “후보” (비용의 2배 여지 가설)
7) 후보 ≠ 실주문. 과거 ≠ 미래.
```

---

## 3. 6개월 백테스트가 돌아가는 방식

### 3.1 공통 공정 가정 (전략 간 비교용)

| 항목 | 값 |
|------|-----|
| 기간 | 약 **6개월** (리포트에 기재된 봉·일자 구간) |
| 초기 자본 | 예: **$10,000** |
| 포지션 | 기본 **롱 온리** (숏·레버리지 없음) |
| 신호 시점 | 해당 봉 **종가**로 지표·CERS 계산 |
| 체결 | **다음 봉 시가 (next-open fill)** — 룩어헤드 방지 |
| 수수료 | 편도 **0.1%** |
| 슬리피지 | 편도 **0.05%** |
| 왕복 대략 | **0.30%** |
| 실주문 | **없음** (시뮬레이션 전용) |

체결가 근사:

- 매수 체결 ≈ 다음 시가 × (1 + f 반영 전 슬리피지 가산) 등 — 엔진이 편도 수수료·슬리피지를 **현금/PnL에 차감**  
- 매도 동일하게 편도 비용 적용

### 3.2 일봉(1d) vs 1분봉(1m)

| | 일봉 6m | 1분봉 6m |
|--|---------|----------|
| 용도 | 다전략 스크리닝·MDD 감 | 스캘프·커스텀(CERS/MathEdge 등) 연구 |
| 봉 수 | 거래일 ~120대 | 수만 봉 (정규장 필터 가능) |
| 비용 민감도 | 중 | **매우 큼** (왕복 0.3%가 엣지를 쉽게 잠식) |
| 산출물 예 | `artifacts/vmar_6m_backtest_report.md` | `artifacts/vmar_1m_6m_backtest_report.md` |

### 3.3 엔진이 하는 일 (개념)

```text
캔들 로드 → 지표/CERS 계산(종가)
    → 신호(롱 후보) → 다음 봉 시가 체결 가정
    → 수수료 0.1% + 슬리피지 0.05% 반영
    → TP/SL/시간청산 → 거래 로그
    → 총수익·MDD·Sharpe·승률·PF·노출 집계
    → artifacts/*.json + *.md 리포트
```

**실패 시(데이터 없음·구간 불명·API 오류):** 백테스트는 결과를 “좋은 전략”으로 포장하지 않고 **실패/스킵**해야 합니다. 실거래 게이트와 별개이지만, **모르는 상태를 수익으로 위장하지 않음**.

---

## 4. 안전 (필수)

| 규칙 | 내용 |
|------|------|
| 투자 조언 아님 | CERS·백테스트 순위는 **매수/매도 추천이 아님** |
| Past ≠ future | 특정 6개월·특정 종목(예: VMAR 급락 구간) 결과는 **다른 구간에서 깨질 수 있음** |
| 실주문 차단 | 백테스트 경로에서 **Toss 주문 API 호출 금지**. live 게이트와 무관하게 주문 생성 없음 |
| 과최적화 | 커스텀 지표가 한 구간에 맞춰지면 점수만 좋게 나옴 → 워크포워드·다른 구간 재검증 필요 |
| 유동성 | 저유동 종목은 가정 슬리피지 0.05%보다 **훨씬 나쁜 체결** 가능 |
| 기본 안전값 | `ALLOW_LIVE_ORDERS=false`, `KILL_SWITCH=true`, `ORDER_MODE=dry_run` 유지 |

백테스트 “1위”가 나와도:

1. paper / dry-run 후보 연구 재료일 뿐  
2. live readiness 체크리스트와 **무관**  
3. 오너 승인·실계좌 키·다중 게이트 없이는 **실주문 불가능** (`docs/LIVE_READINESS_CHECKLIST.md`)

---

## 5. 실행 방법

> CLI·스크립트는 백테스트 웨이브에서 Runner에 연결됩니다.  
> 아래는 **표준 진입점**입니다. 구현 전이면 빌드/도움말 오류가 날 수 있으며, 그 경우 기존 `artifacts/` 리포트로 해석 연습을 합니다.

### 5.1 Runner (권장)

저장소 루트에서:

```bash
# 도움말 (서브커맨드 확인)
dotnet run --project src/TradingBot.Runner -- backtest --help

# 예: VMAR 일봉 약 6개월, CERS 포함 다전략
dotnet run --project src/TradingBot.Runner -- backtest \
  --symbol VMAR \
  --interval 1d \
  --lookback 6m \
  --fee 0.001 \
  --slip 0.0005 \
  --fill next-open \
  --indicator CERS \
  --out artifacts/

# 예: 1분봉 연구 (비용 민감)
dotnet run --project src/TradingBot.Runner -- backtest \
  --symbol VMAR \
  --interval 1m \
  --lookback 6m \
  --fee 0.001 \
  --slip 0.0005 \
  --fill next-open \
  --indicator CERS \
  --out artifacts/
```

의미:

| 인자 | 의미 |
|------|------|
| `--symbol` | 티커 (연습 예: VMAR) |
| `--interval` | 봉 간격 `1d` / `1m` / `15m` 등 |
| `--lookback 6m` | 약 6개월 구간 |
| `--fee 0.001` | 편도 수수료 0.1% |
| `--slip 0.0005` | 편도 슬리피지 0.05% |
| `--fill next-open` | 신호 종가 → **다음 시가 체결** |
| `--indicator CERS` | 커스텀 CERS 포함 |
| `--out` | 리포트 출력 디렉터리 |

### 5.2 스크립트 (있으면)

```bash
# 예: 프로젝트 표준 래퍼 (추가 시)
bash scripts/grok/backtest-cers-6m.sh
# 또는
bash scripts/backtest/run-6m.sh --symbol VMAR --indicator CERS
```

스크립트는 내부적으로 위 `dotnet run ... backtest` 를 호출하고,  
`artifacts/` 에 JSON+Markdown을 남기는 것이 목표입니다.

### 5.3 안전 확인

- 실행 로그에 **order submit / live** 가 없어야 함.  
- 기본 환경: live 차단 유지. 백테스트가 환경 변수를 바꿔 **실주문을 켜지 않음**.

---

## 6. 리포트 산출물 읽는 법

### 6.1 파일 쌍

| 파일 | 용도 |
|------|------|
| `artifacts/*_backtest_report.json` | 기계 판독: 가정, 전략 배열, 거래 샘플 |
| `artifacts/*_backtest_report.md` | 오너용 표·결론·한계 |

예시 이름:

- `artifacts/vmar_6m_backtest_report.md` — 일봉 다전략  
- `artifacts/vmar_1m_6m_backtest_report.md` — 1분봉 + 커스텀(MathEdge/CERS 계열)

### 6.2 JSON에서 먼저 볼 키

| 키 | 의미 |
|----|------|
| `symbol`, `interval`, `period`, `bars` | 무엇을·언제·몇 봉 |
| `start_price`, `end_price`, `buy_hold_ret_pct` | 단순 보유 기준선 |
| `assumptions.brokerage_per_side` | 수수료 편도 (0.001 = 0.1%) |
| `assumptions.slippage_per_side` | 슬리피지 편도 (0.0005 = 0.05%) |
| `assumptions` 의 fill 문구 | **next open** 인지 확인 |
| `custom_indicators` / CERS 정의 | 점수 공식이 리포트와 문서와 같은지 |
| `strategies[]` 또는 `ranking_*` | 전략별 수익·MDD·거래수 |
| `disclaimer` | 투자 조언 아님·past≠future |

### 6.3 성과 숫자 해석 (함정 포함)

| 지표 | 오너 질문 |
|------|-----------|
| 총수익 % | 이 구간에서 계좌가 얼마나 변했나? (미래 보장 아님) |
| MDD % | 중간에 최대 얼마나 깎였나? |
| Sharpe | 변동 대비 수익 느낌 — 표본 짧으면 과장 |
| 거래수 | 0거래 = “수익 1위”가 아니라 **안 사서 원금 보존**일 수 있음 |
| 승률·PF | 거래 적을 때 운 영향 큼 |
| 노출 % | 시장에 묶여 있던 시간 비중 |

**정직한 읽기 순서**

1. Buy&Hold 기준선과 시장 방향(상승/급락) 확인  
2. 가정이 문서와 같은지 (0.1% / 0.05% / next-open)  
3. **0거래 전략**을 “최고 전략”으로 오해하지 않기  
4. 커스텀 CERS/MathEdge가 좋아 보여도 **과최적화·호가 두께** 의심  
5. “paper 후보 연구” 이상으로 올리지 않기  

### 6.4 Markdown 섹션 가이드

리포트 공통 골격:

1. 데이터·기간  
2. 공통 가정  
3. 전략 성적 표  
4. 결론 요약 (1위·BuyHold)  
5. 매매 로그 샘플  
6. 한계  
7. 산출물 경로  

---

## 7. 다른 문서와의 관계

| 문서 | 관계 |
|------|------|
| [`VMAR_1M_SPLIT_SCALP_PRACTICE.md`](VMAR_1M_SPLIT_SCALP_PRACTICE.md) | VMAR 분할 스캘프 **연습(UI dry-run/paper)**. 백테스트 CERS와 목적이 다름 |
| [`TOSS_US_FEES.md`](TOSS_US_FEES.md) | 수수료 0.1% 근사 근거 |
| [`LIVE_READINESS_CHECKLIST.md`](LIVE_READINESS_CHECKLIST.md) | 실주문 가능 여부 — 백테스트와 **분리** |
| [`OWNER_PLAYBOOK.md`](OWNER_PLAYBOOK.md) | 오너 안전 기본값 |

---

## 8. 한계 (정직)

1. CERS는 **가설 점수**이지 예측기가 아님.  
2. EMA·RSI 구간·임계값 \(k\) 를 바꾸면 순위가 바뀜 (과최적화).  
3. next-open 가정은 실제 지정가/시장가·호가잔량과 다름.  
4. VMAR 같은 급락·저유동 구간에서는 “회귀” 가설이 깨지기 쉬움.  
5. 백테스트 성공 ≠ live readiness.  

---

*문서 버전: 2026-07-09 · pw13-bt-docs · 실주문 경로 없음 · 투자 조언 아님*
