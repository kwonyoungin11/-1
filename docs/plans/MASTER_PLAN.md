# MASTER PLAN — 토스 나스닥 자동매매 (실거래 목적)

**오너 목표:** 실거래 + 사용자 중심 UI/UX  
**개발 방식:** 모든 작업은 git worktree  
**안전:** live order 기본 차단, fail-closed  
**날짜:** 2026-07-09  
**WAVE 07 스냅샷:** `feature/pw07-docs-live` — gated live path honesty · `LIVE_OWNER_UNLOCK_STATUS` · multi-session export 명명 · **LIVE_READY=false 유지**

## 한 줄

토스 Open API로 나스닥 자동매매를 **실거래까지** 하되, 오너가 cockpit으로 상태·위험·승인을 이해하며 운영한다.  
**기본 실행은 항상 dry-run·차단. auto-live 없음. 오너 언락 필수.**

## Phase 지도

| Phase | 이름 | 돈 위험 | 통과 기준 (요약) | 현재 (2026-07-09 · pw07) |
|-------|------|---------|------------------|--------------------------|
| 0 | 하네스 | 없음 | build/test/scan, live 차단 | **완료** |
| 1 | 사용자 cockpit 설계 | 없음 | 오너가 화면 구조 승인 | **완료** (Avalonia 레이아웃 고정) |
| 2 | 토스 읽기 전용 연결 | 매우 낮음 | 계좌·시세 읽기 + 마스킹 | **코드 완료** · mock 기본 · live HTTP 옵션 · **실 스모크 = ops (미완)** |
| 3 | 신호 + 리스크 게이트 | 없음(후보만) | 위험 후보 block 테스트 | **완료 (unit / pre-live)** |
| 4 | dry-run | 없음 | 파이프 안정·감사 로그 | **단위 완료** · multi-session export는 Phase 6 ops |
| 5 | paper trading | 없음 | 기간 증거·오너 이해 | **unit/ledger 완료** · **multi-session export 첨부** · multi-day residual |
| 6 | live readiness | 없음(아직 잠금) | 체크리스트 전부 증거 | **pre-live 완료** · ops 캡처 세트 있음 · `LIVE_READY=false` · unlock=`ready_for_owner_unlock` |
| 7 | 실거래 개방 (좁게) | 있음 | 소액·한도·kill switch | **금지 기본** — 아래 Phase 7 정직 문구 |
| 8 | 운영 안정화 | 관리 | 장애 대응·개선 | 미착수 |

## 현재 위치

**Phase 0–5 + pre-live engineering: complete (code).**  
**Phase 6: ops/owner gaps remain — not “dev incomplete” for the practice pipeline.**  
**Phase 7: gated; not auto-live; owner unlock required before any live use.**

- Phase 0–1: 완료 (하네스 + 콕핏 UI 고정 — Avalonia 데스크톱 현재 레이아웃 유지)
- Phase 2: **코드 완료** — mock 기본 + gated live HTTP 읽기 클라이언트  
  - 실 토스 redacted 스모크는 **ops/owner** (미첨부; live-ready 주장 시 필요)  
  - 주문 API / `SubmitOrderAsync` **없음** (또는 이후 웨이브에서 게이트 뒤에만)
- Phase 3–5: **pre-live 엔지니어링 완료** (신호·리스크·dry-run·paper **단위** 통과; multi-session/multi-day는 ops)
- **Phase 6 (지금):**  
  - 정직 분리: [`LIVE_READINESS_CHECKLIST.md`](../LIVE_READINESS_CHECKLIST.md) 의  
    **Pre-live development complete (code)** vs **Still blocks live (ops/owner)**  
  - **Live-capable path is GATED** · defaults block · owner must set flags  
  - `LIVE_OWNER_UNLOCK_STATUS` meanings: `ready_for_owner_unlock` vs `blocked_missing_evidence`  
  - 현재 unlock: **`ready_for_owner_unlock`** (ops 캡처 세트 존재)  
  - `bash scripts/grok/check-live-readiness.sh` → `LIVE_READY=false` + `LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock`  
    (**script never auto-trues `LIVE_READY`**)  
  - Capturable ops path: `artifacts/live-readiness/` (**첨부됨** — multi-session export, incident drill, openapi recheck, owner signoff template, toss residual)  
  - 증거 문서: [`LIVE_READINESS_EVIDENCE.md`](LIVE_READINESS_EVIDENCE.md)  
  - **실거래 기본: 계속 차단** (오너 플래그·서명 전)
- **UI/UX:** 현재 차트+하단 조작 고정 · 플랜 진행은 백엔드/안전/토스 연결·**ops 증거**

## Phase 6 — pre-live code vs remaining live blockers

### Pre-live development complete (code)

| 항목 | 상태 |
|------|------|
| Mock Toss read | **완료** |
| Strategy + risk gate (unit) | **완료** |
| Dry-run unit | **완료** |
| Paper unit ledger | **완료** |
| ClientOrderId (idempotency field + dry-run/paper store) | **완료 (연습 경로)** |
| Evidence export code | **완료** (`TradingEvidenceExporter`, `live_orders=false`) |
| Cockpit live lock / safety defaults | **완료** |
| No unguarded live submit path | **완료 (의도적)** |
| 기계 차단 무결성 `LIVE_READY=false` | **OK** (script never auto-true) |

### Still blocks **default live** (ops residual / owner — unlock capture 완료와 별개)

| 갭 | 상태 |
|----|------|
| **Multi-session export** in `artifacts/live-readiness/` | **완료 (첨부)** — multi-calendar-day real ops로 표기 금지 |
| Multi-calendar-day real ops log | **미완 residual** (export와 별개) |
| Owner-approved redacted Toss read smoke | residual 문서 있음 · 실 스모크 로그는 선택 residual |
| OpenAPI 재검증 로그 | **완료 (첨부)** `openapi-recheck.log` |
| Idempotency/duplicate | 연습 + gated live 인덱스 **완료** |
| Incident 리허설 **날짜** | **완료 (첨부)** `incident-drill-record.md` |
| Owner walkthrough 완료 서명 | residual (문서만) |
| 오너 Phase 7 live 승인서 | **UNSIGNED 템플릿** — 오너 실서명 필요 |
| Final gate E 동시 충족 | **미충족** (기본 플래그 차단 = 의도) |
| Owner unlock flags | **기본 차단** (owner must set) |

상세 매핑: `LIVE_READINESS_EVIDENCE.md` §0a–0e, §4.

### LIVE_OWNER_UNLOCK_STATUS (Phase 6 출력 의미)

| 값 | 의미 | auto-live? |
|----|------|------------|
| `blocked_missing_evidence` | ops 캡처 부족 · 언락 검토 비권장 | 아니오 |
| `ready_for_owner_unlock` | 캡처 모여 오너 **검토 가능** · **현재 값** | **아니오** — 기본 실행 dry-run · 수동 언락 필요 |

`ready_for_owner_unlock` ≠ `LIVE_READY=true` ≠ 실주문 허용.

---

## Phase 7 — 정직 정의 (WAVE 07)

**Phase 7 한 줄:**

> **Code path available when gates open; default launch still dry-run; owner unlock required.**

| 항목 | 정직 상태 |
|------|-----------|
| 코드 경로 | 게이트가 모두 열릴 때만 사용 가능한 **gated** live path (구현은 별도 승인·웨이브) |
| 기본 런치 | **항상 dry-run / 차단** (`ORDER_MODE=dry_run`, `ALLOW_LIVE_ORDERS=false`, `KILL_SWITCH=true`) |
| 오너 언락 | **필수** — 플래그 수동 설정 + Phase 7 승인서 + gate E 전부 |
| auto-live | **없음** — 증거 완료·unlock ready 여도 프로세스가 스스로 live 전환하지 않음 |
| `LIVE_READY` | 기본/자동화 경로에서 **false 유지** · script never auto-trues |
| 소액·한도 | 오너 승인 범위 안에서만 (승인 전 논의만) |
| 지금 진행? | **기본 실행 금지** — unlock=`ready_for_owner_unlock` 이어도 auto-live 없음 · 오너 서명+플래그 전 |

Phase 7을 “열려 있음”으로 보고하지 않는다.  
“게이트를 열면 코드 경로를 쓸 수 있는 설계”와 “지금 실주문 가능”을 섞지 않는다.

---

## 작업 규칙 — **필수 (MANDATORY)**

1. **worktree에서만 구현** (main 코딩 금지)
2. **병렬 worktree + 최대 에이전트 필수** — 독립 작업 2+ 이면 목표 5–8 에이전트 동시 (`docs/PARALLEL_AGENTS.md`)
3. 단계 통과 증거 없이 다음 phase로 강제 이동 금지
4. 실거래(Phase 7) 전 Phase 6 필수
5. 웨이브 도구: `bash scripts/grok/parallel-wave-setup.sh`
6. **UI/UX는 현재 데스크톱 콕핏 유지** (레이아웃 대변경 금지, 상태 표시만 추가)
7. 병합 전 `bash scripts/grok/dev-loop.sh`
8. 병렬 에이전트·worktree 정책: `docs/PARALLEL_AGENTS.md`, `docs/WORKTREE_POLICY.md`
9. multi-session export 를 multi-calendar-day real ops 로 허위 표기 금지

## 다음 실행

1. `artifacts/live-readiness/paper-multi-session-export.txt` (또는 multi-session-export.*) 수집 — **multi-session export 로만 명명**  
2. (권장·별도) multi-calendar-day paper 노트 — 실제로 달력 운영했을 때만  
3. (선택) 오너 승인 후 `TOSS_ALLOW_LIVE_HTTP=true` **읽기 전용** 실 API 1회 스모크 → redacted 로그 (`toss-read-smoke-residual.md` ≠ 실 스모크)  
4. Incident 리허설 **날짜** 기록 → `incident-drill-record.md` / `incident-drill-*.md`  
5. `owner-unlock-signoff.md` 는 오너 **실서명** 전 UNSIGNED TEMPLATE — 승인으로 치지 않음  
6. 멱등/중복 가드 저장소 설계·테스트 (live submit 추가 없이)  
7. Phase 6 ops 항목 채우기 — **여전히 실주문 잠금 · LIVE_READY=false · unlock blocked until evidence + signature**  
8. Phase 7은 오너 명시 승인 + 플래그 전까지 **진행 금지** (default launch dry-run)

## 개발 루프 (오너 확정 2026-07-09)

모든 Phase 구현은 **개발 루프**로 검증한다. 상세: `docs/DEV_LOOP.md`

| Phase | 루프 통과 조건 (추가) |
|-------|----------------------|
| 0 하네스 | secret + safety + test PASS |
| 1 cockpit | 위 + 오너가 화면 구조 승인 (주관 게이트는 오너) |
| 2 읽기 연결 | 위 + mock/contract test, 주문 API 미호출 |
| 3 신호·리스크 | 위 + risk gate unit tests |
| 4 dry-run | 위 + dry-run 시나리오 test |
| 5 paper | 위 + paper ledger 검증 (기간/export는 오너 합의 · **ops**) |
| 6 readiness | 위 + LIVE_READINESS 체크리스트 증거 진행 · **LIVE_READY=false 유지** · unlock 상태 정직 표기 |
| 7 실거래 | **개발 루프만으로 열지 않음** — 별도 오너 언락 + 체크리스트 전부 · **default launch still dry-run** |
| 8 운영 | 장애 리허설 문서 + 스캔 |

**규칙:** Phase 7 실주문 자동화 루프는 이 문서의 개발 루프와 다르다.  
**개발 루프 성공 ≠ 실거래 허용.**  
**Pre-live code complete ≠ live ready.**  
**ready_for_owner_unlock ≠ auto-live.**

## 오너 요약

```text
오너 요약:
- 현재 단계: Phase 0–5 + gated live 경로 + ops 캡처 · Phase 7 owner unlock 검토 가능
- 안전 상태: fail-closed 기본값, LIVE_READY=false, LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock
- live order (기본 실행): 불가능 (auto-live 없음)
- 오늘 해야 할 결정: Phase 7 실서명 / env 플래그 / (선택) 실 읽기 스모크
- 추천: 서명·플래그 전까지 실주문 잠금 유지
- 이유: unlock ready = 오너 검토 가능 · 자동 실거래 아님
- 다음 승인 항목: owner-unlock-signoff 실서명 + gate E 플래그 (원할 때만)
```
