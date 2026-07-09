# MASTER PLAN — 토스 나스닥 자동매매 (실거래 목적)

**오너 목표:** 실거래 + 사용자 중심 UI/UX  
**개발 방식:** 모든 작업은 git worktree  
**안전:** live order 기본 차단, fail-closed  
**날짜:** 2026-07-09

## 한 줄

토스 Open API로 나스닥 자동매매를 **실거래까지** 하되, 오너가 cockpit으로 상태·위험·승인을 이해하며 운영한다.

## Phase 지도

| Phase | 이름 | 돈 위험 | 통과 기준 (요약) |
|-------|------|---------|------------------|
| 0 | 하네스 | 없음 | build/test/scan, live 차단 (대부분 완료) |
| 1 | 사용자 cockpit 설계 | 없음 | 오너가 화면 구조 승인 |
| 2 | 토스 읽기 전용 연결 | 매우 낮음 | 계좌·시세 읽기 + 마스킹 |
| 3 | 신호 + 리스크 게이트 | 없음(후보만) | 위험 후보 block 테스트 |
| 4 | dry-run | 없음 | 파이프 안정·감사 로그 |
| 5 | paper trading | 없음 | 기간 증거·오너 이해 |
| 6 | live readiness | 없음(아직 잠금) | 체크리스트 전부 증거 |
| 7 | 실거래 개방 (좁게) | 있음 | 소액·한도·kill switch |
| 8 | 운영 안정화 | 관리 | 장애 대응·개선 |

## 현재 위치

- Phase 0–1: 완료 (하네스 + 콕핏 UI 고정 — Avalonia 데스크톱 현재 레이아웃 유지)
- Phase 2: **완료 (코드)** — mock 기본 + live HTTP 읽기 클라이언트  
  - 실 토스 스모크는 오너가 `TOSS_ALLOW_LIVE_HTTP=true` 일 때만
- Phase 3–5: 완료 (신호·리스크·dry-run·paper 골격)
- Live readiness automation: `check-live-readiness.sh` (LIVE_READY=false expected)
- **UI/UX:** 현재 차트+하단 조작 고정 · 플랜 진행은 백엔드/안전/토스 연결
- **실거래: 계속 차단** (오너 Phase 7 승인 전 개방 없음)

## 작업 규칙

1. worktree에서만 구현
2. 단계 통과 증거 없이 다음 phase로 강제 이동 금지
3. 실거래(Phase 7) 전 Phase 6 필수
4. **UI/UX는 현재 데스크톱 콕핏 유지** (레이아웃 대변경 금지, 상태 표시만 추가)
5. 병합 전 `bash scripts/grok/dev-loop.sh`

## 다음 실행

1. (선택) 오너 승인 후 `TOSS_ALLOW_LIVE_HTTP=true` 로 **읽기 전용** 실 API 1회 스모크
2. Phase 3–5 전략/증거 보강 (UI 유지)
3. Phase 6 live readiness 체크리스트 증거 채우기 — **여전히 실주문 잠금**
4. Phase 7은 오너 명시 승인 전까지 진행 금지

## 개발 루프 (오너 확정 2026-07-09)

모든 Phase 구현은 **개발 루프**로 검증한다. 상세: `docs/DEV_LOOP.md`

| Phase | 루프 통과 조건 (추가) |
|-------|----------------------|
| 0 하네스 | secret + safety + test PASS |
| 1 cockpit | 위 + 오너가 화면 구조 승인 (주관 게이트는 오너) |
| 2 읽기 연결 | 위 + mock/contract test, 주문 API 미호출 |
| 3 신호·리스크 | 위 + risk gate unit tests |
| 4 dry-run | 위 + dry-run 시나리오 test |
| 5 paper | 위 + paper ledger 검증 (기간은 오너 합의) |
| 6 readiness | 위 + LIVE_READINESS 체크리스트 증거 |
| 7 실거래 | **개발 루프만으로 열지 않음** — 별도 오너 승인 + 체크리스트 |
| 8 운영 | 장애 리허설 문서 + 스캔 |

**규칙:** Phase 7 실주문 자동화 루프는 이 문서의 개발 루프와 다르다. 개발 루프 성공 ≠ 실거래 허용.

