# 라이트 UI · TradingView 차트 · 최신 기술 메모

## UI

- Avalonia 11 + FluentTheme **Light**
- 배경 `#F4F7FB`, 카드 `#FFFFFF`, 보더 `#E2E8F0`
- 액센트 `#2563EB`, TV 상승 `#089981`, 하락 `#F23645`

## 차트 (LiveCharts2 2.0.5)

- TV 라이트 팔레트 캔들/거래량/그리드
- ENTRY/SL/TP 수평선, 버블(거래대금), SMA 오버레이
- `AnimationsSpeed=0`, Zoom X

## 최신 기술 로드맵 (연구)

| 기술 | 상태 | 비고 |
|------|------|------|
| LiveCharts2 dual Y | **적용** | ScalesYAt 가격/거래량 |
| TV Lightweight Charts WebView | 미적용 | 라이선스·Mac 호스트 복잡도 |
| 실봉 모멘텀/ADX 필터 | **다음** | 신호 엔진 고도화 |
| Skia custom draw crosshair | 선택 | LiveCharts 툴팁으로 대체 중 |
| OpenTelemetry UI metrics | 선택 | 관측성 스킬 연동 가능 |

완전 TV 패리티는 native LiveCharts로 근사; WebView LWC는 별도 제품 결정.
