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
- `LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock` 를 live 허용으로 해석
- multi-session export 만으로 multi-calendar-day 실운영 완료 주장

## 리허설 (ops drill — live 허용 아님)

사고 대응 **절차 연습**이다. 리허설을 해도 live는 열리지 않는다.

| 항목 | 내용 |
|------|------|
| 목적 | kill switch / allow-live off / dry-run 복귀 손동작 확인 |
| 기록 위치 | `artifacts/live-readiness/incident-drill-record.md` 또는 `incident-drill-YYYYMMDD.md` |
| 필수 기록 | 날짜(KST), 참석(오너), 시나리오 1줄, kill switch 확인, live 미개방 확인 |
| 금지 | 실주문, 토큰 로그, 수익 보장 문구 |
| 체크리스트 연결 | `docs/LIVE_READINESS_CHECKLIST.md` 섹션 D — **날짜 없으면 drill 미완** |

### 최소 리허설 체크리스트

```text
[ ] Runner / 앱 중지 연습
[ ] KILL_SWITCH=true 확인
[ ] ALLOW_LIVE_ORDERS=false 확인
[ ] ORDER_MODE=dry_run 확인
[ ] (가상) 키 유출 시 재발급 절차 말로 확인
[ ] artifacts/live-readiness/incident-drill-record.md (또는 incident-drill-YYYYMMDD.md) 작성
[ ] LIVE_READY 가 여전히 false 임을 확인 (스크립트 never auto-true)
[ ] multi-session export 를 multi-calendar-day real ops 로 허위 표기하지 않음
```

**현재:** 날짜 찍힌 리허설 기록 **있음** — `artifacts/live-readiness/incident-drill-record.md` (2026-07-09).  
`LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock` 와 정합. **그래도 `LIVE_READY=false` · auto-live 아님.**
