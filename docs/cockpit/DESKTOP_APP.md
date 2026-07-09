# 자동매매 콕핏 — Mac 데스크톱 앱

## 화면 배치
- **위 2/3**: 차트 (ChartFanatics 스타일 — 다크 캔들 + **규모별 초록/빨강 버블**)
  - **버블 크기 = 체결 규모** (`수량 × 가격` = 거래대금 개념). 원이 클수록 큰 체결.
  - 초록 = 매수 표시, 빨강 = 매도 표시 (연습 마커 · 주문 버튼 아님)
- **아래 1/3**: 자동매매 조작 UI만
  - 대상 주식 종류 (카탈로그 라벨 — 아래 **트레이더 프리셋**)
  - 차트 종목 (워치리스트에서 선택)
  - 매매 전략 (관망만 / 단순연습 / 추세추종 / 평균회귀 / 모멘텀돌파)
  - 자동매매 시작 / 종료
  - 잔액 · 수익률

## 트레이더 프리셋 (대상 주식 콤보)

**데이터 소스만** — Avalonia 레이아웃 변경 없음.

| 항목 | 동작 |
|------|------|
| 콤보 옵션 | `WatchlistCatalog.KindLabels` ← `AllKinds` 기반 자동 생성 |
| ViewModel | `MainWindowViewModel.StockKindOptions` 가 카탈로그를 그대로 바인딩 |
| 기본 선택 | Domain에 **`나스닥코어3`** 가 있으면 코어3, 없으면 **나스닥** |
| 코어3 유니버스 | Domain 병합 시 **QQQ · NVDA · AAPL** 3종 (연습 유니버스) |
| 탐지 방식 | `MainWindowViewModel.TryGetCore3Kind` — enum 문자열 `"나스닥코어3"` (병합 전 컴파일 안전) |

### 나스닥코어3 (트레이더 코어 프리셋)

- **이름:** `StockMarketKind.나스닥코어3` (Domain `feature/pw08-universe-core` 병합 후)
- **심볼:** QQQ, NVDA, AAPL (정확히 3개)
- **설명(Domain):** 나스닥 코어 3종 — 연습 유니버스
- **용도:** 좁은 관심 목록으로 연습 세션·시그널 파이프라인 검증
- **아님:** 투자 권유 · 매수/매도 추천 · 수익 보장 · 실주문 대상 목록

Domain 이 `AllKinds` / `KindLabels` / `ResolveSymbols` 에 코어3를 넣으면 **추가 UI 코드 없이** 콤보에 나타납니다.

현재 카탈로그 종류 예 (Domain 기준, 코어3 병합 전후):

- 나스닥 / 나스닥테크 / **나스닥코어3**(병합 시) / 미국주식 / 미국ETF / 국내주식

## 전략 요약 (모두 연습 · 투자 조언 아님)
| 전략 | 동작 |
|------|------|
| 관망만 | 후보 없음 |
| 단순연습전략 | 매수 후보 고정 (파이프라인 검증) |
| 추세추종 | 모멘텀 방향 매수/매도 |
| 평균회귀 | 반대 방향 후보 |
| 모멘텀돌파 | 강한 신호만 · **수량(규모)↑ → 버블↑** |

## 실행
```bash
cd "/Users/kwon/Documents/c#"
dotnet run --project src/TradingBot.App
```

## 패키지 호환 (앱이 안 열릴 때)
- **Avalonia 11.3.x** + **LiveCharts 2.0.x** 조합을 사용합니다.
- Avalonia 12 + LiveCharts Avalonia 는 `MissingFieldException: PinchEvent` 로 **창이 즉시 종료**됩니다.
- 차트 라이브러리가 Avalonia 12를 공식 지원하면 그때 올려도 됩니다.

## 토스 연결 (Phase 2)
- 기본: **mock** (`TOSS_ALLOW_LIVE_HTTP=false`)
- 실 읽기: `.env`에 키 + `TOSS_ALLOW_LIVE_HTTP=true` (주문 API 여전히 없음)
- UI: 상단 뱃지 `mock` / `토스 읽기` + 하단 연결 문구 (레이아웃 변경 없음)

## 실 포트폴리오 → UI 세션 바인딩 (pw10)

`AppHarness.GetDashboardAsync` 가 `ConnectionStatus.LiveReadOnlyConnected` 일 때만:

| 항목 | 동작 |
|------|------|
| 잔액 | `CashBuyingPowerUsd` / `CashBuyingPower`(병합 시) → 없으면 `MarketValueUsdSummary` 파싱 → 없으면 연습 유지 |
| 잔액 라벨 | `잔액 X (실계좌 읽기)` · mock 은 `잔액 X (연습)` |
| StartingBalance | 첫 실잔액 1회만 설정 (기본 연습 100k 일 때) |
| 워치 심볼 | 보유 심볼 ∪ 현재 워치 (보유 없으면 카탈로그 유지) |
| FocusSymbol | 첫 보유 → 시세 있는 첫 종목 → 워치 첫 종목 |
| 차트 | 실 캔들 API 있으면 캐시 사용, 없으면 **실시세 last price seed** 로 mock 봉 |
| SafetyNote | 여전히 **실주문 차단** |
| ConnectionPill | 실 HTTP 모드 → **토스 읽기** |

세션 API (`AutoTradeSessionService`):

- `ApplyExternalBalance(decimal, setStartingIfUnset)`
- `ApplyExternalWatchSymbols(string[])`
- `SetDataSourceLabel(string)` — `"연습"` / `"실계좌 읽기"`

**아님:** 실주문 · live 기본 해제 · 투자 권유.

## 안전
- 시작/종료 = **연습 세션** 제어 (실주문 차단 유지)
- 차트 매수/매도 = **표시 마커** (주문 실행 버튼 아님)
- 잔액·수익률 = mock 시 **연습** · Live 읽기 시 **실계좌 읽기** 라벨 (주문 실행 아님)
- 상태/헤더 카피: **연습 전용 · 투자 조언 아님 · 실주문 없음**
- 프리셋·전략 선택은 **자동매매 연습 설정**일 뿐, 종목 추천이 아님
