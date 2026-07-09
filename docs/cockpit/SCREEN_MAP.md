# Cockpit 화면 지도 (사용자 중심)

## 설계 원칙

1. **홈 먼저** — 운영의 90%는 Overview에서 끝낸다.  
2. **상태 > 버튼** — 설명 없는 위험 버튼 금지.  
3. **실거래는 깊은 곳 + 다단계** — 실수로 도달하기 어렵게.  
4. **18개 전체는 메뉴에 두되, MVP는 핵심 8개.**

## 정보 우선순위 (홈에 반드시)

| 순위 | 정보 | 오너가 얻는 것 |
|------|------|----------------|
| 1 | 실거래 잠금 (Live Lock) | 지금 돈이 나가는 모드인가? |
| 2 | Kill Switch | 긴급 정지 ON/OFF |
| 3 | 봇 상태 (한 문장) | 지금 뭐 하는 중? |
| 4 | 오늘 차단/경고 요약 | 왜 안 돌아갔나? |
| 5 | 연결 상태 (API/읽기) | 토스·시세 살아 있나? |
| 6 | 계좌 요약 (마스킹) | 대략 얼마·보유 느낌 |
| 7 | 다음 행동 카드 | 내가 할 일 |
| 8 | 최근 감사 로그 3줄 | 방금 무슨 일이 |

## 네비게이션 (제안)

```text
[홈 Overview]
  ├─ 상태 상세 (Bot / Risk / Live Lock)
  ├─ 시장·관심종목
  ├─ 주문 후보·검증 (dry-run / paper)
  ├─ 기록 (Audit / Paper ledger / Replay)
  └─ 설정·도움말 (키 가이드, 용어)
```

모바일/좁은 화면: 하단 5탭 가능  

`홈 | 상태 | 후보 | 기록 | 더보기`

## MVP 화면 (Phase 1 승인 대상) — 8개

| ID | 화면 | 필수 이유 |
|----|------|-----------|
| S01 | Overview (홈) | 10초 파악 |
| S02 | Bot State | 단계 설명 |
| S03 | Live Lock + Kill Switch | 돈 보호 |
| S04 | Risk Gate | 왜 막혔는지 |
| S05 | Order Candidate | 후보 ≠ 실주문 |
| S06 | Account Snapshot | 읽기 결과 (Phase 2 데이터) |
| S07 | Audit Log | 신뢰 |
| S08 | Settings / Key Guide | 키는 여기 안내만 |

## 전체 18화면 매핑 (메뉴에 존재, MVP 이후 채움)

| # | 화면 | MVP | 채우는 Phase |
|---|------|-----|--------------|
| 1 | Overview | Y | 1 |
| 2 | Bot State | Y | 1 |
| 3 | API Connection | 홈 요약 | 2 |
| 4 | Account Snapshot | Y | 2 데이터 |
| 5 | Market Session | 홈 배지 | 2 |
| 6 | Watchlist | later | 2–3 |
| 7 | Strategy Signal | later | 3 |
| 8 | Risk Gate | Y | 1 골격 / 3 데이터 |
| 9 | Order Candidate | Y | 3 |
| 10 | Dry-run Result | later | 4 |
| 11 | Paper Ledger | later | 5 |
| 12 | Live Lock | Y (S03) | 1 |
| 13 | Manual Approval | later | 6–7 |
| 14 | Kill Switch | Y (S03) | 1 |
| 15 | Audit Log | Y | 1 골격 |
| 16 | Error Center | 홈 경고 | 2 |
| 17 | Settings | Y | 1 |
| 18 | Backtest/Replay | later | 5+ |

## 화면 간 이동 규칙

- 홈의 “왜 막혔나?” → Risk Gate  
- 홈의 “주문 후보 N건” → Order Candidate (**실주문 버튼 없음**)  
- Live Lock “잠금 해제 시도” → 경고 + Phase 6 전 **불가** 안내  
- Kill Switch 켜기: 1단계 / 끄기: 2단계 이상 (향후 구현)

## 디자이너 수정 가능 / 불가

| 가능 | 불가 |
|------|------|
| 색, 간격, 카드 배치 | Live Lock 기본값 의미 변경 |
| 아이콘 | 차단 사유 숨기기 |
| 타이포 | 원클릭 실주문 추가 |
