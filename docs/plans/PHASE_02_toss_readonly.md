# Phase 2 — 토스 읽기 전용 연결

**상태:** mock-first 구현 진행 (2026-07-09)  
**돈 위험:** 매우 낮음 — **주문 create/modify/cancel 호출 금지**  
**실거래 연결:** 실거래의 재료(계좌·시세·장시간)를 정확히 읽는 단계

---

## 1. 목표

1. OAuth2 Client Credentials로 토큰 발급 (메모리 캐시, 로그 마스킹)  
2. `GET /api/v1/accounts` → accountSeq 확보 가이드  
3. holdings / prices / US market calendar 등 **읽기**  
4. cockpit S01/S06에 **마스킹된** 스냅샷 표시 가능하도록 projection 연결  
5. 실패·stale·미연결 시 UI/도메인이 **block 메시지** (fail-closed)

## 2. 하지 않을 것

- `POST /api/v1/orders` 및 정정·취소  
- 조건주문  
- live unlock  
- 키·토큰·계좌 원문 로그/화면 출력  

## 3. 공식 문서 SoT

- `docs/TOSS_OPENAPI_NOTES.md`  
- `artifacts/openapi/toss-openapi.snapshot.json`  
- 변경 시 `scripts/grok/check-toss-openapi-diff.sh`

## 4. 구현 순서 (개발 루프)

1. Options/settings (env) + redaction tests  
2. Auth client (mock 우선)  
3. Account + holdings read (mock contract test)  
4. Market data read (NASDAQ 심볼)  
5. Integration flag 없으면 실 API 스킵  
6. CockpitSnapshot 필드 채우기 (연결/계좌 요약)  
7. dev-loop PASS  

## 5. 오너 관여

- `.env`에 TOSS_CLIENT_ID/SECRET 이미 있음 (값 출력 금지)  
- accountSeq는 첫 성공 조회 후 오너가 `.env`에 넣을 수 있음  
- 실 API 호출 켜기 여부: 별도 승인 (`TOSS_ALLOW_LIVE_HTTP=false` 기본 권장)

## 6. 통과 기준

- [ ] 주문 경로 코드 없음 (safety scan)  
- [ ] mock contract tests PASS  
- [ ] secret redaction tests PASS  
- [ ] cockpit에 “미연결/연결됨/오류” 구분  
- [ ] dev-loop PASS  
- [ ] (선택) 오너 승인 하에 실 read-only 1회 스모크  

## 7. 롤백

read client 비활성 플래그로 즉시 끄기. 주문과 무관.


## 구현 체크 (진행 중)

- [x] TossOptions + TOSS_ALLOW_LIVE_HTTP=false 기본
- [x] DTO mapper + fixture contract tests
- [x] Mock auth/account/market clients
- [x] LiveHttpGuard blocks outbound when flag false
- [x] ReadOnlyPortfolioService + Cockpit projector
- [x] Order client still disabled
- [ ] Owner-approved live read-only HTTP smoke (optional, later)
