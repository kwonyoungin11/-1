# 자동매매 콕핏 — Mac 데스크톱 앱

## 형태
- **C# Avalonia 네이티브 창 앱** (브라우저 아님)
- 프로젝트: `src/TradingBot.App`
- UI: **글래스모피즘** · **한국어만** · 자동매매에 필요한 상태만 표시

## 화면에 있는 것 (필수만)
- 긴급정지 / 실거래 잠금 / 연습 모드
- 봇 상태 · 지금 할 일
- 계좌(마스킹) · 연결 · 시장
- 리스크 검사 · 주문 후보(실주문 아님)
- 모의 기록 · 가상 체결 건수
- **새로고침**만 (매수·매도 실행 버튼 없음)

## 실행
```bash
cd "/Users/kwon/Documents/c#"
dotnet run --project src/TradingBot.App
```

## 목업 이미지
- `docs/cockpit/mockups/cockpit-macos-glass-ko.jpg`
