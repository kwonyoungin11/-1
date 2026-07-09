# Incident Response (사고 대응)

## 언제 쓰는가?

- 의심스러운 주문/체결
- API 키 유출 의심
- 봇이 이상 상태
- market data stale / unknown state 반복
- 프로그램 강제 종료 후 불일치

## 즉시 행동 (돈 보호 우선)

1. **프로그램 중지** (Runner / 대시보드 종료)
2. **KILL_SWITCH=true** 유지 또는 설정
3. **ALLOW_LIVE_ORDERS=false**, **ORDER_MODE=dry_run** 확인
4. 필요 시 Mac 네트워크 일시 차단
5. 토스증권 앱/WTS에서 주문·체결 육안 확인

## 그다음

6. 로그 수집 (`logs/` — 비밀값 마스킹 확인)
7. 시간대(KST/US ET) 기록
8. `docs/DECISIONS/` 또는 사고 노트에 재발 방지 기록
9. 원인 해결 전 live 재개 금지

## 키 유출이 의심되면

- 토스 Open API 키 재발급/폐기 (WTS 설정)
- `.env` 교체
- git history에 키가 들어갔는지 검사 (gitleaks)
- 채팅/클라우드에 붙여넣은 기록 삭제

## 하지 말 것

- “일단 다시 켜서 확인”
- kill switch 끄고 재시도
- 문제 있는 채로 실주문 테스트
