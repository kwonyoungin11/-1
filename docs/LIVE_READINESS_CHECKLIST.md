# Live Readiness Checklist

**현재 상태: NOT READY — live order 불가능**  
**LIVE_READY = false (고정 기대값 — 이 문서/자동화가 true로 바꾸지 않음)**  
**LIVE_OWNER_UNLOCK_STATUS = blocked_missing_evidence (ops 증거 미완 · 오너 언락 불가)**  
**기준일: 2026-07-09 · WAVE 07 docs honesty (`feature/pw07-docs-live`)**

모든 항목이 재현 가능한 증거와 함께 완료되고, 오너가 Phase 7 승인서를 서명하기 전까지  
**실주문(live order)은 열지 않습니다.**

> **오너 한 줄:** 사전 실거래 **코드 개발(Phase 0–5 엔지니어링)** 은 이 worktree 기준으로 완료된 상태입니다.  
> 남은 것은 **운영·오너 증거**입니다. 개발 게이트 통과 ≠ 실거래 허용.  
> **LIVE_READY=false 가 정답입니다.**  
> **ready_for_owner_unlock ≠ auto-live.** 언락 가능 상태여도 기본 실행은 dry-run·차단입니다.

범례:

| 표시 | 의미 |
|------|------|
| `[x]` | 이 worktree 기준으로 **재현 증거 있음** |
| `[~]` | **부분** — 코드/단위테스트는 있으나 운영 기간·실 API·오너 서명 등 미완 |
| `[ ]` | **미완** — live 전에 반드시 채워야 함 (대개 **ops/owner**, “dev incomplete” 아님) |

상세 증거 경로: [`docs/plans/LIVE_READINESS_EVIDENCE.md`](plans/LIVE_READINESS_EVIDENCE.md)

---

## Live-capable path is GATED (not auto-live)

실주문 **코드 경로**가 나중에 존재하더라도, **기본 실행은 항상 차단**입니다.  
“경로가 있다” ≠ “실주문이 열린다”.

| 레이어 | 기본값 / 동작 | 오너가 바꿔야 하는가? |
|--------|---------------|----------------------|
| `ALLOW_LIVE_ORDERS` | `false` | **예** — 명시 true 없으면 차단 |
| `KILL_SWITCH` | `true` | **예** — false로 끄기 전 전 게이트 충족 |
| `ORDER_MODE` | `dry_run` | **예** — `live`는 오너 의도 + 승인 후에만 |
| `TOSS_ALLOW_LIVE_HTTP` | `false` | 읽기 스모크 시에만 오너 로컬 설정 |
| Live submit path | 없거나 게이트 뒤 | Phase 7 구현·승인 전 사용 금지 |
| 런처 / harness | fail-closed 하드코딩 | 기본 실행 = dry-run · live 불가 |
| 자동화 스크립트 | `LIVE_READY=false` 고정 출력 | **스크립트가 true로 올리지 않음** |

**규칙:**

1. **Defaults block.** 저장소 기본값·`.env.example`·composition root는 live를 열지 않는다.
2. **Owner must set flags.** live를 고려하려면 오너가 로컬 env/플래그를 **의도적으로** 설정한다 (에이전트/스크립트 자동 설정 금지).
3. **Gated path ≠ open live.** 게이트가 열린 “가능 경로”가 코드에 있어도, **default launch는 dry-run** 이고 kill switch·allow·mode·승인·리스크가 전부 통과하기 전엔 실주문 불가.
4. **No auto-live.** `ready_for_owner_unlock` 이어도 프로세스가 스스로 live로 전환하지 않는다. 오너 언락 + 수동 확인이 필요하다.
5. **Script never auto-trues `LIVE_READY`.** `check-live-readiness.sh` 는 기본값이 안전한 동안 **항상** `LIVE_READY=false` 를 출력한다. 오너 env 구성과 무관하게 **문서/자동화 기대값은 false**.

```text
# 기본 실행 (항상 이 쪽)
ALLOW_LIVE_ORDERS=false
KILL_SWITCH=true
ORDER_MODE=dry_run
LIVE_READY=false          ← 스크립트/문서가 true로 바꾸지 않음

# 오너 언락 후에만 논의 가능 (지금도 기본 아님 · Phase 7 전 금지)
# ALLOW_LIVE_ORDERS=true + KILL_SWITCH=false + ORDER_MODE=live
# + 체크리스트 A–E ops 증거 + 오너 서명 + 전 게이트 동시 통과
```

---

## LIVE_OWNER_UNLOCK_STATUS meanings

기계/문서에서 쓰는 **오너 언락 준비 상태**입니다.  
**실주문 허용 여부와 다릅니다.**

| 값 | 의미 | live order? | 다음 행동 |
|----|------|-------------|-----------|
| `blocked_missing_evidence` | ops/owner 증거가 부족해 **오너 언락을 권하지 않음** | **불가** | multi-session export · (선택) redacted 스모크 · incident drill · 서명 준비 |
| `ready_for_owner_unlock` | 합의된 ops 증거가 모여 **오너가 언락을 검토할 수 있음** | **여전히 불가(기본 차단)** | 오너가 플래그·승인서를 **수동**으로 처리. **auto-live 아님** |
| (없음 / 미출력) | 평가 전 또는 스크립트 미연동 | **불가** | readiness eval 후 위 둘 중 하나로 기록 |

### 정직 구분 (혼동 금지)

| 상태 문자열 | auto-live? | 기본 런치 | 오너 조치 필요? |
|-------------|------------|-----------|-----------------|
| `LIVE_READY=false` | 아니오 | dry-run · 차단 | 항상 (live를 원할 때만) |
| `LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence` | 아니오 | dry-run · 차단 | ops 증거 먼저 |
| `LIVE_OWNER_UNLOCK_STATUS=ready_for_owner_unlock` | **아니오** | **여전히 dry-run · 차단** | 플래그 + Phase 7 서명 + gate E |
| `LIVE_READY=true` | **문서/스크립트 기본 경로에서는 나오지 않음** | — | 스크립트는 기본 안전 시 true로 올리지 않음 |

**`ready_for_owner_unlock` 은 “열어도 된다”가 아니라 “증거가 모여 오너 검토 가능”입니다.**  
언락 후에도 kill switch·allow·mode·세션·리스크·한도·멱등·감사·수동 확인이 **전부** 통과해야 하며, 하나라도 실패하면 **block**.

**현재 (WAVE 07):** `LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence`  
이유: multi-session / multi-day ops 로그, 오너 서명, incident drill 날짜, 실 스모크 등 미첨부.

---

## Pre-live development complete (code)

아래는 **실거래 개방 전 엔지니어링 범위**에서 “코드 + 단위/스텁 테스트로 끝난 것”입니다.  
**이 섹션이 채워져도 LIVE_READY는 false입니다.**  
**ready_for_owner_unlock 도 자동으로 되지 않습니다** — ops 증거가 필요합니다.  
다일 paper 운영·실 토스 스모크·오너 서명은 여기 포함하지 **않습니다**.

| 영역 | 완료 항목 (코드/단위) | 주요 앵커 |
|------|----------------------|-----------|
| Harness / safety defaults | fail-closed 기본값 · 기계 게이트 | `TradingSafetyDefaults` · `ALLOW_LIVE_ORDERS=false` · `KILL_SWITCH=true` · `ORDER_MODE=dry_run` · `check-live-readiness.sh` / `check-trading-safety.sh` |
| Mock Toss read | accounts / holdings / prices / calendar mock · DTO 매핑 | `MockTossClients` · fixtures · `ReadOnlyPortfolioServiceTests` · `TossDtoMapperTests` |
| Live HTTP read (gated) | OAuth/account/market clients + `LiveHttpGuard` · 주문 경로 미호출 스텁 | `LiveToss*Client` · `TOSS_ALLOW_LIVE_HTTP=false` default · `LiveTossHttpClientTests` |
| Order path blocked | live submit 없음 · 주문 클라이언트 비활성 | **no `SubmitOrderAsync` in `src`** · `BlockedTossOrderClient.IsLiveSubmissionEnabled => false` · `BlockedLiveOrderRouter` |
| Strategy + candidates | 신호 카탈로그 · 후보 파이프라인 | `StrategyCatalog` · `OrderCandidatePipeline` · Application tests |
| Risk gate | stale/missing/session/notional 등 후보 차단 · LiveOrderGate | `RiskGate` · `LiveOrderGate` · `UsMarketSessionGuard` · Risk.Tests |
| Dry-run | dry-run 라우터 + in-memory ledger | `DryRunOrderRouter` · `InMemoryDryRunLedger` · `DryRunLedgerTests` |
| Paper (unit) | paper 라우터 + in-memory ledger · evidence `LiveModePresent=false` | `PaperOrderRouter` · `InMemoryPaperLedger` · `PaperLedgerTests` · `EvidenceBuilderTests` |
| Idempotency (present) | `ClientOrderId` 생성 + dry-run/paper 중복 거부 | `ClientOrderIdFactory` · `ClientOrderIdIndex` · `ClientOrderIdempotencyTests` (실 라우터+ledger) |
| Evidence export (code) | dry-run+paper 스냅샷 export · `live_orders=false` | `TradingEvidenceExporter` · `EvidenceBuilder` · Orders.Tests |
| Cockpit UX | live lock / kill switch / safety strip 표시 | Avalonia + Web `LiveLock` / `SafetyStrip` · `CockpitSnapshot` · Ui/Web tests |
| Observability | audit redaction · in-memory audit | `SecretRedactor` · `AuditMessageRedactor` · `InMemoryAuditLog` |
| OpenAPI snapshot | 공식 스펙 스냅샷 1.2.2 | `artifacts/openapi/toss-openapi.snapshot.json` |

**코드 결론:** Phase 0–5 + pre-live 엔지니어링 골격은 **완료**.  
**실주문 구현 없음 · 기본값 차단 유지 · 단위 테스트 통과.**  
운영 기간 증거·오너 서명·실 API 스모크는 **“dev incomplete”가 아니라 live ops 게이트**입니다.

---

## Still blocks live (ops/owner — not "dev incomplete")

아래가 **지금 live를 막는 것**입니다. 대부분 코드 미완이 아니라 **운영 로그 / 오너 행위 / 기간 증거**입니다.  
체크리스트에서 이들을 “개발 미완”으로 오해하지 마십시오.

| 차단 항목 | 상태 | 왜 live를 막는가 |
|-----------|------|------------------|
| **Multi-session export** 및/또는 **multi-day paper ops** | **미첨부** | 단위 paper ≠ 운영 안정. export는 `artifacts/live-readiness/` 에 날짜·세션 표시. **multi-session export ≠ multi-calendar-day real ops** (아래 정직 규칙) |
| **Owner-approved redacted Toss read smoke** | **미첨부** (선택이나 “live-ready 주장” 시 필요) | mock/stub ≠ 실 연결. 오너 로컬 키 + redacted 로그 1회 |
| **OpenAPI snapshot 주기 재검증 로그** | 프로세스만 있음 | 스냅샷 존재하나 운영 re-fetch 로그 미첨부 |
| **gitleaks/trivy hard gate** | optional | 설치 시 hard gate로 고정하려면 운영/CI 증거 |
| **Idempotency / duplicate on live path** | dry-run/paper 가드 완료 · **live 경로 없음** | 연습 경로 `ClientOrderIdIndex` 완료. live submit 추가 시 동일 가드 재사용 필요 (Phase 7) |
| **Account reconcile / full live limits** | 미증거 | 실거래 전 계좌 정합·한도 전체 세트 |
| **Incident drill date** | **날짜 없음** | `docs/INCIDENT_RESPONSE.md` 절차만 존재 · 리허설 기록 미첨부 |
| **Owner cockpit walkthrough sign-off** | 문서만 | walkthrough 존재 · 오너 완료 서명/날짜 미첨부 |
| **Owner Phase 7 signature** | **없음 (의도)** | 없으면 실거래 개방 금지 |
| **Final gate E simultaneous true** | **미충족** | kill/allow/mode 기본 차단 + 위 ops 전부 |
| **Owner env unlock flags** | **기본 차단 유지** | 오너가 플래그를 켜기 전엔 gated path 사용 불가 |

**LIVE_READY=false 유지 이유:** 위 ops/owner 항목이 비어 있고, 기본 안전 플래그가 의도적으로 차단이며, 주문 submit 경로가 코드에 없음.  
**LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence** 동일 이유.

### Multi-session export vs multi-calendar-day real ops (정직 명명)

| 이름 | 무엇인가 | multi-day real ops로 주장 가능? |
|------|----------|----------------------------------|
| **multi-session export** | 여러 paper/dry-run **세션** 결과를 `TradingEvidenceExporter` 등으로 모아 `artifacts/live-readiness/` 에 저장한 것 | **아니오** — 세션 묶음 export일 뿐 |
| **multi-calendar-day real ops** | 실제 달력 일수에 걸친 paper(또는 합의 기간) 운영 로그·노트 | 날짜 찍힌 기간 로그만 **예** (여전히 live 아님) |
| unit paper tests | `dotnet test` in-memory ledger | **아니오** |

**규칙:** multi-session export 만 있을 때 **“다일 실운영 완료”라고 쓰지 않는다.**  
체크리스트·증거 문서에는 **`multi-session export`** 라고 명시한다.  
달력 일수 실운영을 주장하려면 별도 dated multi-day 로그가 필요하다.

---

## Capturable ops artifacts (`artifacts/live-readiness/`)

운영 증거는 **secrets 없이** 아래 디렉터리에 모은다.  
`artifacts/live-readiness/` 는 **캡처 위치**이며, 디렉터리가 비어 있으면 **증거 없음**으로 본다.

| 아티팩트 (권장 / 관측 파일명) | 내용 | 채우면 도움 되는 게이트 | 정직 한계 |
|------------------------------|------|-------------------------|-----------|
| `paper-multi-session-export.txt` (또는 `multi-session-export.json`/`.txt`) | multi-session export · `live_orders=false` | C 세션 묶음 증거 | **≠ multi-calendar-day real ops** |
| `paper-session-notes-YYYYMMDD.md` | 세션 노트 (심볼·모드·이슈) · 이익 보장 금지 | multi-session 맥락 | 노트 ≠ 달력 일수 실운영 |
| `multi-day-ops-YYYYMMDD-YYYYMMDD.md` | **달력 기간** 실 paper 운영 요약 (있을 때만) | multi-calendar-day real ops | export와 **별개** · 없으면 주장 금지 |
| `toss-read-smoke-redacted.log` | 오너 로컬 **실** 읽기 스모크 · 마스킹 | B real connectivity | 필수 시 실 스모크만 인정 |
| `toss-read-smoke-residual.md` | mock/stub 잔여 리스크 메모 (실 스모크 전) | B 잔여 위험 문서화 | **≠ 실 Toss 스모크 완료** |
| `openapi-recheck.log` / `openapi-reverify-YYYYMMDD.log` | snapshot re-fetch/diff | B snapshot freshness | 로그 없으면 미완 |
| `incident-drill-record.md` / `incident-drill-YYYYMMDD.md` | 리허설 날짜·kill switch 확인 | D incident drill | 날짜 없으면 미완 |
| `owner-walkthrough-signoff.md` | 콕핏 walkthrough 완료 날짜·서명 | D owner understanding | 미서명 = 미완 |
| `owner-unlock-signoff.md` | Phase 7 언락 템플릿/서명 | E / Phase 7 | **UNSIGNED TEMPLATE ≠ 승인** · 자동 생성 금지(실서명) |
| `phase7-owner-approval.md` | 별도 승인서 (오너 작성) | E / Phase 7 | 자동화 생성 금지 |
| `gate-e-snapshot-YYYYMMDD.txt` | 게이트 E 체크 시점 메모 | E 동시 충족 기록 | 플래그 값은 alias만 |
| `README.md` (optional) | 폴더 인덱스 | 탐색 | 비밀 금지 |

**금지:** 토큰, `.env`, 계좌번호, 미마스킹 API 응답, 수익 보장 문구.  
**현재 (이 docs worktree):** `artifacts/live-readiness/` 캡처 세트 **미첨부** → `blocked_missing_evidence`.  
ops 웨이브가 파일을 생성해도: multi-session export·unsigned template·residual smoke 만으로 **auto-live / multi-day real ops / 실 스모크 완료**를 주장하지 않는다.

---

## Automation (기계 검증 — live 허용 아님)

아래 자동화는 **live를 열지 않습니다.**  
통과 = “기본값이 여전히 차단 + 체크리스트/증거 문서 존재 + src에 live submit 없음”.

| 항목 | 명령 | 통과 의미 | live 허용? |
|------|------|-----------|------------|
| Live readiness automation | `bash scripts/grok/check-live-readiness.sh` | `LIVE_READY=false`, 안전 기본값 intact | **아니오** |
| Trading safety scan | `bash scripts/grok/check-trading-safety.sh` | fail-closed 기본값·금지 패턴 유지 | **아니오** |
| Owner readiness | `bash scripts/grok/check-owner-readiness.sh` | harness/docs 존재 | **아니오** |
| Secret protection | `bash scripts/grok/check-secrets.sh` | `.env` 미추적·gitignore | **아니오** |
| Dev loop | `bash scripts/grok/dev-loop.sh` | 개발 검증 루프 (실주문 루프 아님) | **아니오** |

- 증거 수집 방법: [`docs/plans/LIVE_READINESS_EVIDENCE.md`](plans/LIVE_READINESS_EVIDENCE.md)
- `check-live-readiness.sh`는 **flags를 live로 바꾸지 않음**
- **`LIVE_READY` 를 true로 올리지 않음** (오너 env 구성과 무관 · 스크립트 기대값 항상 false under defaults)
- exit 0 + `LIVE_READY=false` = 정상(차단 유지). exit 1 = 안전이 깨졌거나 필수 문서 누락
- dev-loop / verify 에 포함되어 있어도 **live order는 여전히 불가능**
- `LIVE_OWNER_UNLOCK_STATUS` 는 **별도 평가** (증거 폴더·체크리스트). `blocked_as_expected` ≠ unlock ready

### 이 worktree에서 확인된 기계 결과 (2026-07-09)

```text
LIVE_READY=false
LIVE_SAFETY_INTACT=true
LIVE_READINESS_STATUS=blocked_as_expected
LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence
trading safety scan PASSED
owner readiness PASSED (docs/harness present; live still blocked)
secret scan PASSED
dotnet test: 실패 0 (Domain/Risk/Orders/Toss/Application/Ui/Web/Runner/App/Obs)
```

- [x] `scripts/grok/check-live-readiness.sh` 존재 및 통과 시 `LIVE_READY=false` 출력
- [x] `docs/plans/LIVE_READINESS_EVIDENCE.md` 존재
- [ ] 섹션 A–E **ops/owner** 전 항목 완료 (아래 — **미완료, live 불가**; 코드 섹션과는 별개)
- [ ] `artifacts/live-readiness/` 에 capturable 증거 세트 없음 → unlock blocked

---

## A. 환경 / 보안

| 상태 | 항목 | 현재 증거 (요약) | 분류 |
|------|------|------------------|------|
| [x] | .NET SDK 설치 및 `dotnet test` 통과 | SDK 10.0.301 · `dotnet test TradingBot.sln` 실패 0 (2026-07-09, 이 worktree) | code/CI |
| [x] | `.env` git 미추적 | `git check-ignore -v .env` → `.gitignore:2:.env` · `check-secrets.sh` PASS | code |
| [x] | secret scan 통과 | `bash scripts/grok/check-secrets.sh` PASS | code |
| [~] | gitleaks/trivy (가능 시) 통과 | `verify.sh`에서 **optional** (`\|\| true`). 미설치 시 warning만. hard gate 아님 | **ops/CI** |
| [x] | 로그에 secret/계좌 미노출 테스트 | `TradingSafetyDefaultsTests.SecretRedactor_*` · `AuditMessageRedactorTests` · `DomainTossRedactor` | code |

**A 결론:** 기본 secret/test 게이트(코드) 양호. gitleaks/trivy hard gate는 **ops**. **live 불가 원인 아님(다른 섹션 ops 미완).**

---

## B. Toss 연결 (read-only)

| 상태 | 항목 | 현재 증거 (요약) | 분류 |
|------|------|------------------|------|
| [x] | 공식 OpenAPI snapshot 존재 | `artifacts/openapi/toss-openapi.snapshot.json` (OpenAPI **1.2.2**) | code artifact |
| [~] | snapshot “최신” 재검증 | fetch/diff 스크립트 존재. **주기 재fetch 운영 로그 미첨부** | **ops** |
| [x] | OAuth token **mock** + HTTP stub 경로 | fixtures · `LiveTossHttpClientTests` (stub handler) | code |
| [~] | (승인 후) sandbox/read-only **실연결** | 클라이언트 + 게이트 코드 있음. **오너 실 스모크 로그(redacted) 미첨부** | **ops/owner** |
| [x] | accounts / holdings / prices / US calendar (mock) | mock fixtures · mapper/portfolio tests | code |
| [x] | live HTTP 시 orders 경로 미호출 (stub) | `Live_read_path_uses_oauth_accounts_prices_without_orders` | code |
| [x] | live HTTP 기본 차단 | `TOSS_ALLOW_LIVE_HTTP=false` · `LiveHttpGuard` | code |
| [x] | 주문 클라이언트 live submit 비활성 | `BlockedTossOrderClient` · safety scan | code |
| [~] | rate limit / error model 처리 | 문서 수준 · 전용 테스트 thin | design/ops (not multi-day paper) |
| [x] | redaction 테스트 | Domain + Observability | code |

**B 결론:** **mock + stub 읽기 = pre-live code 완료.** 실 토스 스모크·snapshot 재검증 로그 = **ops/owner** (완료로 표시하지 않음).  
**주문 API 구현 없음 → 실주문 경로 없음.**

---

## C. Risk / Orders

| 상태 | 항목 | 현재 증거 (요약) | 분류 |
|------|------|------------------|------|
| [x] | risk gate **코어** 구현 + 단위 테스트 | `RiskGate` · `LiveOrderGate` · `UsMarketSessionGuard` · Risk.Tests PASS | code (pre-live) |
| [~] | live용 전 한도·계좌 정합 세트 | Max* partial · reconcile 미증거 | **ops / live-prep design** |
| [x] | dry-run 단위 안정 | `DryRunOrderRouter` · ledger tests · mode ≠ Live | code |
| [x] | paper **단위** ledger | `PaperOrderRouter` · `PaperLedgerTests` · `EvidenceBuilderTests` | code |
| [~] | paper **multi-session export** (세션 묶음) | **코드 exporter 있음 · `artifacts/live-readiness/` 미첨부** — multi-day real ops로 주장 금지 | **ops (blocks unlock)** |
| [ ] | paper **multi-calendar-day real ops** | **미첨부** — multi-session export 와 **별개** | **ops (blocks live claims)** |
| [~] | manual approval 2단계 UX | 게이트 플래그 + Live Lock 표시. **실거래 2단계 승인 UX 없음(의도적)** | code partial + **owner Phase 7** |
| [x] | idempotency / duplicate guard (dry-run/paper) | `ClientOrderIdFactory` + `ClientOrderIdIndex` + 실 라우터 테스트 PASS | code (pre-live) |
| [x] | live implementation 명시 비활성 | `BlockedLiveOrderRouter` · no `SubmitOrderAsync` · scripts PASS | code |

**C 결론:** dry-run/paper **단위 = pre-live code 완료**.  
multi-session export 파일·multi-day 기간 로그 = **ops**.  
**실주문 라우터는 의도적으로 막혀 있음. multi-session export 를 multi-day 완료로 체크하지 않음.**

---

## D. UX / 운영

| 상태 | 항목 | 현재 증거 (요약) | 분류 |
|------|------|------------------|------|
| [x] | cockpit에서 live lock / kill switch 명확 | Avalonia + Web safety strip · snapshot tests | code |
| [~] | 오너가 상태 설명 이해 가능 | `OWNER_WALKTHROUGH.md` · 한국어 문구. **오너 완료 서명/날짜 미첨부** | **owner** |
| [ ] | incident response 리허설 | `INCIDENT_RESPONSE.md` 존재. **날짜 찍힌 리허설 기록 없음** | **ops** |
| [ ] | 오너 서명: live 전환 승인서 | **없음** (Phase 7 전제). 자동화로 생성 금지 | **owner Phase 7** |

**D 결론:** UI “잠김” 표시 = code 완료. 리허설·오너 서명 = **ops/owner** → **live 불가**.

---

## E. 최종 게이트 (모두 필요)

아래는 **전부 동시에** 만족해야 하며, 이 문서의 automation은 이들을 true로 바꾸지 않습니다.

```text
KILL_SWITCH=false                    ← 현재 기본 true (차단)
ALLOW_LIVE_ORDERS=true               ← 현재 기본 false (차단)
ORDER_MODE=live                      ← 현재 기본 dry_run (차단)
manual approval exists               ← 플래그만, 운영 승인 흐름 미완
session open                         ← UsMarketSessionGuard 단위 수준
market data fresh                    ← RiskGate stale 규칙 단위 수준
risk gate pass                       ← 후보 단위 규칙 (pre-live core)
dry-run pass                         ← 단위 OK / multi-session export ops partial
paper trading stable                 ← 단위 OK / multi-session·multi-day 증거 없음 ← ops gap
valid Toss credential                ← 로컬 .env / 실스모크 미첨부 ← owner/ops
account reconciled                   ← 미증거
limits ok                            ← 일부 Max* 설정만
duplicate guard pass                 ← dry-run/paper ClientOrderIdIndex 완료 (live 경로 없음)
idempotency key present              ← ClientOrderIdFactory + 검증
audit log enabled                    ← InMemoryAuditLog 단위
operator confirms live mode          ← 오너 Phase 7 서명 없음
```

| 상태 | 게이트 | 현재 |
|------|--------|------|
| [ ] | E 전체 동시 충족 | **미충족** |
| [x] | 기본 config fail-closed 유지 | `ALLOW_LIVE_ORDERS=false` · `KILL_SWITCH=true` · `ORDER_MODE=dry_run` |
| [x] | Live-capable path GATED | defaults block · owner must set flags · no auto-live |
| [x] | `LIVE_READY=false` under defaults | 스크립트 never auto-true |

하나라도 실패 → **block**.  
**현재: 다수 실패(의도된 차단 + ops/owner 갭) → live 불가능. LIVE_READY=false.**  
**LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence.**

---

## 오너 요약 (WAVE 07 honesty)

```text
오너 요약:
- 현재 단계: Phase 0–5 + pre-live 엔지니어링 완료 · Phase 6 ops 갭 · Phase 7 코드 경로(게이트 시)만 논의 가능 · 기본 실행 dry-run
- 안전 상태: 차단 유지 (LIVE_READY=false, LIVE_SAFETY_INTACT=true, LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence)
- live order 가능 여부: 불가능 (gated · auto-live 없음)
- 오늘 해야 할 결정: multi-session export 수집 여부 / multi-day 기간 합의 / (선택) 읽기 스모크 / incident 리허설 일정
- 내가 추천하는 선택: 실주문 계속 잠금. artifacts/live-readiness/ 에 multi-session export 와 리허설 기록만 쌓기
- 이유: 코드는 “안전하게 막힌 연습 파이프”까지 끝남. unlock ready를 주장할 ops 증거가 없음
- 다음 승인 항목: Phase 7 실거래 개방 여부 (지금은 승인하지 말 것 · ready_for_owner_unlock 전)
```

---

## 다음에 채울 증거 (우선순위 — 전부 ops/owner)

1. **`artifacts/live-readiness/multi-session-export.*`** — 세션 묶음 export (**multi-calendar-day real ops 로 표기 금지**)
2. (권장) 오너 합의 **multi-calendar-day** paper 노트 — export와 별도 파일
3. (선택·live-ready 주장 시 필요) 오너 승인 **redacted Toss read smoke**
4. OpenAPI snapshot 주기 재검증 로그 → `artifacts/live-readiness/`
5. gitleaks/trivy hard gate (설치 시)
6. 멱등/중복 가드 저장소 설계·테스트 (여전히 live submit 없이)
7. **Incident 리허설 날짜** 기록
8. **Phase 7 오너 승인서** — **마지막 · 없음** · 자동 생성 금지

**Bottom line:** Pre-live **code** is complete. Phase 6 remaining work is **ops/owner evidence**.  
**지금은 LIVE_READY=false 가 정답입니다.**  
**LIVE_OWNER_UNLOCK_STATUS=blocked_missing_evidence.**  
**ready_for_owner_unlock ≠ auto-live.** Phase 7 기본 실행은 계속 dry-run · owner unlock required.
