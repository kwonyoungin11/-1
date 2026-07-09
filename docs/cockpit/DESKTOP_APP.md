# TradingBot 데스크톱 앱 (C# Avalonia)

## 형태
- **C# 데스크톱 앱** (Mac / Windows / Linux)
- 프로젝트: `src/TradingBot.App`
- 브라우저 전용이 아님. 창이 뜨는 애플리케이션.

## 실행 (Mac)

```bash
cd "/Users/kwon/Documents/c#"
dotnet run --project src/TradingBot.App
```

## 안전
- 실거래 잠김 기본
- 매수/매도 실행 버튼 없음
- mock 읽기 + dry-run/paper 연습

## 웹과의 관계
- `TradingBot.Web` = 선택적 브라우저 콕핏
- **주 UI 형태 = 데스크톱 앱 (TradingBot.App)**
