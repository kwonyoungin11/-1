# Automatic Trading Cockpit — UX/UI Guide

## 오너 확정 (2026-07-09)

- **Phase 1 화면 구조: 승인** (2026-07-09) — `docs/cockpit/PHASE_01_APPROVAL.md`
- **최종 목적:** 실거래(live trading)
- **UI 원칙:** 사용자 중심 — 오너가 “지금 뭐 하는지 / 왜 막혔는지 / 다음에 뭘 하면 되는지”를 바로 이해
- dry-run·paper 화면은 연습용이 아니라 **실거래를 열기 위한 검증 단계 UI**

## Phase 1 산출물

- 상세 플랜: `docs/plans/PHASE_01_cockpit.md`
- 화면 지도: `docs/cockpit/SCREEN_MAP.md`
- 와이어프레임: `docs/cockpit/WIREFRAME.md`
- 오너 시나리오: `docs/cockpit/OWNER_WALKTHROUGH.md`

## 이 UI는 무엇인가?

수동 주문 앱이 아닙니다. **자동매매 상태 판단 cockpit** 입니다.

오너가 한눈에 알아야 할 것:

- 봇이 지금 무엇을 하는가?
- 왜 주문이 막혔는가?
- live order가 열려 있는가? (기본: 아니오)
- kill switch 위치

## 필수 화면 (18)

1. Overview Dashboard  
2. Bot State  
3. API Connection  
4. Account Snapshot (masked)  
5. Market Session  
6. Watchlist / NASDAQ Symbols  
7. Strategy Signal  
8. Risk Gate  
9. Order Candidate  
10. Dry-run Result  
11. Paper Trading Ledger  
12. Live Lock  
13. Manual Approval  
14. Kill Switch  
15. Audit Log  
16. Error / Warning Center  
17. Settings / API Key Guide  
18. Backtest / Replay Report  

## UX 원칙

- 버튼보다 **상태와 근거** 먼저
- 위험은 색만으로 전달 금지 (텍스트 필수)
- live mode는 의도적으로 불편 (2단계 승인)
- kill switch 항상 접근 가능
- 접근성(screen reader) 고려
- audit log는 사람이 읽는 문장

## 금지 문구

실주문 실행 / 바로 매수 / 즉시 매도 / 자동 실거래 시작 / 돈 벌기 시작 / 수익 보장

## 권장 문구

자동매매 상태 / 전략 신호 / 주문 후보 / dry-run 검증 / paper trading / live blocked / manual approval required / risk gate blocked / kill switch active / read-only connected / account masked

## UI 기술 (오너 확정 2026-07-09)

- **주 호스트: C# 데스크톱 앱** — Avalonia (`src/TradingBot.App`), Mac 창 실행
- 부차: Blazor Web (`src/TradingBot.Web`) 선택 유지

## UI 기술 (이전 메모)



추천: 브라우저 dashboard (Blazor Web App)  
대안: Mac 앱 (Avalonia) / API+별도 웹  
결정 전 구현 확정 금지.
