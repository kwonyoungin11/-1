# 종목 전환 칩 (VMAR / SPCX) — 오너·디자이너 가이드

**투자 조언 아님 · 실주문 기본 잠금 · 종목 전환 ≠ 매수/매도.**

관련: `docs/VMAR_1M_SPLIT_SCALP_PRACTICE.md` · `docs/FINAL_SPCX_STRATEGY.md` · `docs/cockpit/SCREEN_MAP.md`

---

## 한 줄 요약

상단·전략 카드의 **종목 칩**으로 **VMAR(비전마린)** ↔ **SPCX(스페이스X)** 를 고른다.  
전환 시 **전략·시간봉 프리셋**이 적용되고 차트·뉴스·상단 티커가 갱신된다.  
**Live Lock / Kill Switch / 차단 사유는 바뀌지 않는다.**

---

## 무엇인가?

| 칩 라벨 | 심볼 | 오너용 의미 |
|---------|------|-------------|
| **VMAR · 비전마린** | `VMAR` | 연습 기본 · 분할 스캘프 프리셋 |
| **SPCX · 스페이스X** | `SPCX` | 공식 추세 프리셋 (SPCX 전용) |

- 임의 종목 추가 UI가 **아님** — 카탈로그 2종만
- 모르는 티커는 거절·정규화

---

## 어디에 있나?

1. **차트 상단** — 시간봉 칩(`tf-btn`) 근처 · 차트 제목 옆 종목 칩 줄  
2. **전략 카드** — “종목 / 전략” 조작 구역 (하단 자동매매 패널)

둘 다 같은 세션 상태(`FocusSymbol` / `StockKind`). 한쪽 선택 → 다른 쪽 표시 동기화.

---

## 누르면 무엇이 바뀌나?

| 항목 | 동작 |
|------|------|
| 전략 | 해당 종목 **프리셋 전략** 자동 적용 |
| 시간봉 | 프리셋 봉(기본 **15m**) · 칩 선택 동기화 |
| 차트 | 실봉 캐시 무효화 → **새로 로드** |
| 뉴스 | 포커스 심볼 기준 제목·헤드라인 갱신 |
| 상단 티커 필 | `VMAR` ↔ `SPCX` 표시 갱신 |
| 차트 제목 | `{심볼} · {시간봉}` |

**바뀌지 않음:** Live Lock · Kill Switch · `ORDER_MODE` / `ALLOW_LIVE_ORDERS` · Risk 차단 사유 · 원클릭 실주문(없음)

---

## 프리셋 대응

| 종목 | 프리셋 | 전략 | 봉 | 용도 |
|------|--------|------|----|------|
| **VMAR** | `VmarOneMinuteScalpPreset` | 일분분할스캘프 (연습) | **15m** | dry-run / paper 기본 |
| **SPCX** | `SpacexOfficialStrategyPreset` | **추세추종** | **15m** | 공식 추세·지정가 브래킷 골격 |

- VMAR: 분할 지정가 연습 · **실주문 잠금 유지**  
- SPCX: LIMIT + ATR/R 파라미터 **표시** · 후보·계획 ≠ 체결  
- 코드: `AppHarness.SetStockKind` · ViewModel 종목 바인딩

---

## 오너 확인 순서

1. 상단 **Kill / Live Lock / Mode** 필이 그대로인가  
2. 종목 칩이 원하는 심볼인가  
3. 전략 카드 요약이 프리셋 설명과 맞는가  
4. 차트가 갱신되는가 (실패해도 실주문 안 열림)  
5. 뉴스 영역이 해당 심볼인가  

이상 시 **중지** 유지 · Audit · 칩 재선택.

---

## 디자이너 토큰 · 클래스

| 클래스 | 용도 |
|--------|------|
| `.stock-switch` | 종목 칩 **그룹** 컨테이너 |
| `.stock-btn` | 개별 종목 칩 |
| `.stock-btn.selected` | 현재 선택 (`.tf-btn.selected` 톤 맞춤) |

토큰: `color.surface` / `color.textPrimary` (기본), 선택 강조는 safe/accent — **danger(실주문) 색 금지**.

| 가능 | 불가 |
|------|------|
| 색·간격·radius·아이콘(라벨 유지)·배치 | 선택 시 Live 해제 |
| 차트 상단 / 전략 카드 안 재배치 | 차단 사유 숨김 · 원클릭 실주문 · “바로 매수” 카피 |

시간봉 참고: `Button.tf-btn` (`App.axaml`).

---

## 카피 · 금지

**권장:** 종목 전환 · 연습 프리셋 · 공식 추세 프리셋 · 차트 갱신 · 실주문 잠금 유지 · 투자 조언 아님  

**금지:** 바로 매수/매도 · 자동 실거래 시작 · 수익 보장 · 종목 추천 문장  

**안전 금지**

1. 종목 전환으로 live 경로 개방  
2. 게이트/리스크/연결 실패 문구 숨기기  
3. 칩이 Live Lock / Kill 필을 가리거나 대체  
4. 미검증 심볼 자유 입력으로 후보 우회  

---

## 구현 메모 (개발)

- `WatchlistCatalog` · `StockMarketKind.비전마린` / `스페이스X`  
- `AutoTradeSessionService.FocusSymbol` / `StockKind`  
- `SetStockKind` → 프리셋 + 봉 캐시 무효화 · 뉴스 `RefreshNewsAsync`  
- 테스트: `AppHarnessTests.SetStockKind_*`

---

## 오너 요약

| 항목 | 값 |
|------|-----|
| 역할 | 연습/관찰용 **종목·프리셋 전환** |
| 안전 | Live 잠금 · Kill 유지 · fail-closed |
| live order | **불가** (전환만으로 열리지 않음) |
| 기본 연습 | **VMAR** 분할 스캘프 |
| SPCX | 공식 **추세** 프리셋 (후보·계획) |
