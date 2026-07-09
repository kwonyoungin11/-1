# MASTER PLAN — 토스 나스닥 자동매매 (실거래 목적)

**오너 목표:** 실거래 + 사용자 중심 UI/UX  
**개발 방식:** 모든 작업은 git worktree  
**안전:** live order 기본 차단, fail-closed  
**날짜:** 2026-07-09  
**WAVE 06 스냅샷:** `feature/pw06-readiness` — pre-live **code 완료** vs **ops/owner 갭** 정직 분리, **LIVE_READY=false 유지**

## 한 줄

토스 Open API로 나스닥 자동매매를 **실거래까지** 하되, 오너가 cockpit으로 상태·위험·승인을 이해하며 운영한다.

## Phase 지도

| Phase | 이름 | 돈 위험 | 통과 기준 (요약) | 현재 (2026-07-09 · pw06) |
|-------|------|---------|------------------|--------------------------|
| 0 | 하네스 | 없음 | build/test/scan, live 차단 | **완료** |
| 1 | 사용자 cockpit 설계 | 없음 | 오너가 화면 구조 승인 | **완료** (Avalonia 레이아웃 고정) |
| 2 | 토스 읽기 전용 연결 | 매우 낮음 | 계좌·시세 읽기 + 마스킹 | **코드 완료** · mock 기본 · live HTTP 옵션 · **실 스모크 = ops (미완)** |
| 3 | 신호 + 리스크 게이트 | 없음(후보만) | 위험 후보 block 테스트 | **완료 (unit / pre-live)** |
| 4 | dry-run | 없음 | 파이프 안정·감사 로그 | **단위 완료** · 기간 운영은 Phase 6 ops |
| 5 | paper trading | 없음 | 기간 증거·오너 이해 | **unit/ledger 완료** · **다일 기간 증거 = ops 미완** |
| 6 | live readiness | 없음(아직 잠금) | 체크리스트 전부 증거 | **pre-live 엔지니어링 완료** · **ops/owner 갭 남음** · LIVE_READY=false |
| 7 | 실거래 개방 (좁게) | 있음 | 소액·한도·kill switch | **금지** (오너 승인 전 · 진행 금지) |
| 8 | 운영 안정화 | 관리 | 장애 대응·개선 | 미착수 |

## 현재 위치

**Phase 0–5 + pre-live engineering: complete (code).**  
**Phase 6: ops/owner gaps remain — not “dev incomplete” for the practice pipeline.**  
**Phase 7: forbidden.**

- Phase 0–1: 완료 (하네스 + 콕핏 UI 고정 — Avalonia 데스크톱 현재 레이아웃 유지)
- Phase 2: **코드 완료** — mock 기본 + gated live HTTP 읽기 클라이언트  
  - 실 토스 redacted 스모크는 **ops/owner** (미첨부; live-ready 주장 시 필요)  
  - 주문 API / `SubmitOrderAsync` **없음**
- Phase 3–5: **pre-live 엔지니어링 완료** (신호·리스크·dry-run·paper **단위** 통과; 다일 paper는 ops)
- **Phase 6 (지금):**  
  - 정직 분리: [`LIVE_READINESS_CHECKLIST.md`](../LIVE_READINESS_CHECKLIST.md) 의  
    **Pre-live development complete (code)** vs **Still blocks live (ops/owner)**  
  - `bash scripts/grok/check-live-readiness.sh` → `LIVE_READY=false` / `blocked_as_expected`  
  - `check-trading-safety.sh` · `check-secrets.sh` · `check-owner-readiness.sh` PASS  
  - 증거 문서: [`LIVE_READINESS_EVIDENCE.md`](LIVE_READINESS_EVIDENCE.md)  
  - **실거래: 계속 차단** (오너 Phase 7 승인 전 개방 없음)
- **UI/UX:** 현재 차트+하단 조작 고정 · 플랜 진행은 백엔드/안전/토스 연결·**ops 증거**

## Phase 6 — pre-live code vs remaining live blockers

### Pre-live development complete (code)

| 항목 | 상태 |
|------|------|
| Mock Toss read | **완료** |
| Strategy + risk gate (unit) | **완료** |
| Dry-run unit | **완료** |
| Paper unit ledger | **완료** |
| ClientOrderId (idempotency field) | **있음** (중복 저장소는 별도) |
| Cockpit live lock / safety defaults | **완료** |
| No live submit path | **완료 (의도적)** |
| 기계 차단 무결성 `LIVE_READY=false` | **OK** |

### Still blocks live (ops/owner — not "dev incomplete")

| 갭 | 상태 |
|----|------|
| Multi-day paper ops log | **미완** |
| Owner-approved redacted Toss read smoke | **미완** (선택·live-ready 주장 시 필요) |
| OpenAPI 주기 재검증 로그 | **미완** |
| Idempotency/duplicate **store** | **미완** (필드는 있음) |
| Incident 리허설 **날짜** | **미완** |
| Owner walkthrough 완료 서명 | **미완** |
| 오너 Phase 7 live 승인서 | **없음 (의도)** |
| Final gate E 동시 충족 | **미충족** |

상세 매핑: `LIVE_READINESS_EVIDENCE.md` §0b–0c, §4.

## 작업 규칙 — **필수 (MANDATORY)**

1. **worktree에서만 구현** (main 코딩 금지)
2. **병렬 worktree + 최대 에이전트 필수** — 독립 작업 2+ 이면 목표 5–8 에이전트 동시 (`docs/PARALLEL_AGENTS.md`)
3. 단계 통과 증거 없이 다음 phase로 강제 이동 금지
4. 실거래(Phase 7) 전 Phase 6 필수
5. 웨이브 도구: `bash scripts/grok/parallel-wave-setup.sh`
6. **UI/UX는 현재 데스크톱 콕핏 유지** (레이아웃 대변경 금지, 상태 표시만 추가)
7. 병합 전 `bash scripts/grok/dev-loop.sh`
8. 병렬 에이전트·worktree 정책: `docs/PARALLEL_AGENTS.md`, `docs/WORKTREE_POLICY.md`

## 다음 실행

1. 오너 합의 **multi-day paper** + redacted ledger export (`artifacts/`) — **아직 안 함**  
2. (선택) 오너 승인 후 `TOSS_ALLOW_LIVE_HTTP=true` **읽기 전용** 실 API 1회 스모크 → redacted 로그 — **아직 안 함**  
3. Incident 리허설 **날짜** 기록 — **아직 안 함**  
4. 멱등/중복 가드 저장소 설계·테스트 (live submit 추가 없이)  
5. Phase 6 ops 항목 채우기 — **여전히 실주문 잠금 · LIVE_READY=false**  
6. Phase 7은 오너 명시 승인 전까지 **진행 금지**

## 개발 루프 (오너 확정 2026-07-09)

모든 Phase 구현은 **개발 루프**로 검증한다. 상세: `docs/DEV_LOOP.md`

| Phase | 루프 통과 조건 (추가) |
|-------|----------------------|
| 0 하네스 | secret + safety + test PASS |
| 1 cockpit | 위 + 오너가 화면 구조 승인 (주관 게이트는 오너) |
| 2 읽기 연결 | 위 + mock/contract test, 주문 API 미호출 |
| 3 신호·리스크 | 위 + risk gate unit tests |
| 4 dry-run | 위 + dry-run 시나리오 test |
| 5 paper | 위 + paper ledger 검증 (기간은 오너 합의 · **ops**) |
| 6 readiness | 위 + LIVE_READINESS 체크리스트 증거 진행 · **LIVE_READY=false 유지** |
| 7 실거래 | **개발 루프만으로 열지 않음** — 별도 오너 승인 + 체크리스트 전부 |
| 8 운영 | 장애 리허설 문서 + 스캔 |

**규칙:** Phase 7 실주문 자동화 루프는 이 문서의 개발 루프와 다르다.  
**개발 루프 성공 ≠ 실거래 허용.**  
**Pre-live code complete ≠ live ready.**

## 오너 요약

```text
오너 요약:
- 현재 단계: Phase 0–5 + pre-live 엔지니어링 완료 · Phase 6 ops 갭 · Phase 7 금지
- 안전 상태: fail-closed 기본값 유지, 주문 submit 경로 없음, LIVE_READY=false
- live order 가능 여부: 불가능
- 오늘 해야 할 결정: multi-day paper 기간 / (선택) 읽기 전용 실 API 스모크 / incident 리허설 일정
- 내가 추천하는 선택: 실주문 계속 잠금, ops 증거만 쌓기
- 이유: 코드는 연습 파이프까지 끝남. live를 열 조건(다일 paper·스모크·리허설·승인서)이 없음
- 다음 승인 항목: Phase 7 소액 실거래 — 지금은 승인하지 말 것
```
