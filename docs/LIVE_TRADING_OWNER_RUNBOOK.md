# 실거래 오너 실행 가이드 (CERS · VMAR)

**투자 조언 아님. 손실 가능. 자동매매 실주문.**

## 1. `.env` (이미 오너 로컬에 설정돼 있을 수 있음)

```text
ALLOW_LIVE_ORDERS=true
KILL_SWITCH=false
ORDER_MODE=live
TOSS_ALLOW_LIVE_HTTP=true
TOSS_CLIENT_ID=…
TOSS_CLIENT_SECRET=…
# 권장: TOSS_ACCOUNT_SEQ=…  (없으면 계좌 목록 첫 값 시도)
```

저장소 기본 `.env.example`은 **차단** 상태. 실거래는 **오너 로컬 .env**만.

## 2. 앱 실행

```bash
dotnet run --project src/TradingBot.App -c Release
```

## 3. 화면에서 확인할 것

| UI | 의미 |
|----|------|
| 필 `live` / `실거래 ON` | 설정상 실주문 경로 |
| **실거래 게이트** 카드 | OK/BLOCK 체크리스트 |
| **실주문 승인** 체크 | 필수. 없으면 시작 거부 |
| 전략 `CERS비용회귀` · 봉 `1m` · 종목 `VMAR` | 오너 확정 전략 |
| expected / threshold | 진입 조건 |
| **실거래 시작** | 15초 루프 시작 |
| Last live route | 전송/차단 메시지 |

## 4. 순서

1. 새로고침 → 토스 실연결·실봉 확인  
2. **실주문 승인** 체크  
3. 게이트 줄에 BLOCK 없는지 확인  
4. **실거래 시작**  
5. 미국 장중 + CERS 신호 시 토스 `POST api/v1/orders`  
6. 이상 시 **중지** 또는 `KILL_SWITCH=true`

## 5. 여전히 주문이 안 나갈 때

- 장 마감 / 세션 게이트  
- expected ≤ 0.006 (관망)  
- 시세 스테일 · 연결 오류  
- 승인 체크 안 함  
- TOSS 계정/자격증명 문제  

## 6. 안전

- 기본 코드 기본값은 여전히 fail-closed  
- 체크리스트 자동화 `LIVE_READY=false` 유지 (정책)  
- 시크릿·계좌번호 git 커밋 금지  
