# Live Readiness Checklist

**현재 상태: NOT READY — live order 불가능**

모든 항목이 증거와 함께 체크되기 전까지 live order는 열지 않습니다.

## A. 환경 / 보안

- [ ] .NET SDK 설치 및 `dotnet test` 통과
- [ ] `.env` git 미추적
- [ ] secret scan 통과
- [ ] gitleaks/trivy (가능 시) 통과
- [ ] 로그에 secret/계좌 미노출 테스트 통과

## B. Toss 연결 (read-only)

- [ ] 공식 OpenAPI snapshot 최신
- [ ] OAuth token mock + (승인 후) sandbox/read-only 연결
- [ ] accounts / holdings / prices / US calendar 조회 검증
- [ ] rate limit / error model 처리
- [ ] redaction 테스트

## C. Risk / Orders

- [ ] risk gate 전체 규칙 구현 + 테스트
- [ ] dry-run 안정
- [ ] paper trading 기간 및 ledger 검증
- [ ] manual approval 2단계 UX
- [ ] idempotency / duplicate guard
- [ ] live implementation 여전히 명시 승인 전 비활성

## D. UX / 운영

- [ ] cockpit에서 live lock / kill switch 명확
- [ ] 오너가 상태 설명 이해 가능
- [ ] incident response 리허설
- [ ] 오너 서명: live 전환 승인서 (날짜/조건)

## E. 최종 게이트 (모두 필요)

```text
KILL_SWITCH=false
ALLOW_LIVE_ORDERS=true
ORDER_MODE=live
manual approval exists
session open
market data fresh
risk gate pass
dry-run pass
paper trading stable
valid Toss credential
account reconciled
limits ok
duplicate guard pass
idempotency key present
audit log enabled
operator confirms live mode
```

하나라도 실패 → **block**.
