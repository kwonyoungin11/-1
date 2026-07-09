# Live Readiness Checklist

**현재 상태: NOT READY — live order 불가능**  
**LIVE_READY = false (고정 기대값)**  
**기준일: 2026-07-09 · Phase 6 증거 스냅샷 (pw05-phase6)**

모든 항목이 재현 가능한 증거와 함께 완료되고, 오너가 Phase 7 승인서를 서명하기 전까지  
**실주문(live order)은 열지 않습니다.**

> **오너 한 줄:** 지금 코드는 “안전하게 막혀 있음”이 증명된 상태입니다.  
> 개발 게이트 통과 ≠ 실거래 허용.

범례:

| 표시 | 의미 |
|------|------|
| `[x]` | 이 worktree 기준으로 **재현 증거 있음** |
| `[~]` | **부분** — 코드/단위테스트는 있으나 운영 기간·실 API·오너 서명 등 미완 |
| `[ ]` | **미완** — live 전에 반드시 채워야 함 |

상세 증거 경로: [`docs/plans/LIVE_READINESS_EVIDENCE.md`](plans/LIVE_READINESS_EVIDENCE.md)

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
- exit 0 + `LIVE_READY=false` = 정상(차단 유지). exit 1 = 안전이 깨졌거나 필수 문서 누락
- dev-loop / verify 에 포함되어 있어도 **live order는 여전히 불가능**

### 이 worktree에서 확인된 기계 결과 (2026-07-09)

```text
LIVE_READY=false
LIVE_SAFETY_INTACT=true
LIVE_READINESS_STATUS=blocked_as_expected
trading safety scan PASSED
owner readiness PASSED (docs/harness present; live still blocked)
secret scan PASSED
dotnet test: 실패 0 (Domain/Risk/Orders/Toss/Application/Ui/Web/Runner/App/Obs)
```

- [x] `scripts/grok/check-live-readiness.sh` 존재 및 통과 시 `LIVE_READY=false` 출력
- [x] `docs/plans/LIVE_READINESS_EVIDENCE.md` 존재
- [ ] 섹션 A–E 전 항목 사람/운영 증거 완료 (아래 — **미완료, live 불가**)

---

## A. 환경 / 보안

| 상태 | 항목 | 현재 증거 (요약) |
|------|------|------------------|
| [x] | .NET SDK 설치 및 `dotnet test` 통과 | SDK 10.0.301 · `dotnet test TradingBot.sln` 실패 0 (2026-07-09, 이 worktree) |
| [x] | `.env` git 미추적 | `git check-ignore -v .env` → `.gitignore:2:.env` · `check-secrets.sh` PASS |
| [x] | secret scan 통과 | `bash scripts/grok/check-secrets.sh` PASS |
| [~] | gitleaks/trivy (가능 시) 통과 | `verify.sh`에서 **optional** (`|| true`). 미설치 시 warning만. hard gate 아님 |
| [x] | 로그에 secret/계좌 미노출 테스트 | `TradingSafetyDefaultsTests.SecretRedactor_*` · `AuditMessageRedactorTests` · `DomainTossRedactor` |

**A 결론:** 기본 secret/test 게이트는 양호. gitleaks/trivy를 CI hard gate로 고정하려면 설치 + non-optional 실행 증거가 더 필요. **live 불가 원인 아님(다른 섹션 미완).**

---

## B. Toss 연결 (read-only)

| 상태 | 항목 | 현재 증거 (요약) |
|------|------|------------------|
| [x] | 공식 OpenAPI snapshot 존재 | `artifacts/openapi/toss-openapi.snapshot.json` (OpenAPI **1.2.2**, title 토스증권 Open API) |
| [~] | snapshot “최신” 재검증 | `scripts/grok/check-toss-openapi-diff.sh` / `fetch-toss-openapi-spec.sh` 존재. 주기 재fetch 운영 로그는 미첨부 |
| [x] | OAuth token **mock** + HTTP stub 경로 | fixtures `tests/.../Fixtures/oauth_token.json` · `LiveTossHttpClientTests` (stub handler) |
| [~] | (승인 후) sandbox/read-only **실연결** | 코드: `LiveTossAuthClient` / `LiveTossAccountClient` / `LiveTossMarketDataClient` + `TOSS_ALLOW_LIVE_HTTP` 게이트. **오너 실 스모크 로그(redacted) 미첨부** |
| [x] | accounts / holdings / prices / US calendar (mock) | `ReadOnlyPortfolioServiceTests` · mock fixtures · `TossDtoMapperTests` |
| [x] | live HTTP 시 orders 경로 미호출 (stub) | `LiveTossHttpClientTests.Live_read_path_uses_oauth_accounts_prices_without_orders` |
| [x] | live HTTP 기본 차단 | `TOSS_ALLOW_LIVE_HTTP=false` default · `LiveHttpGuard` · `TossOptions.FromEnvironment` 테스트 |
| [x] | 주문 클라이언트 live submit 비활성 | `BlockedTossOrderClient.IsLiveSubmissionEnabled => false` · safety scan 검사 |
| [~] | rate limit / error model 처리 | 문서 `docs/TOSS_OPENAPI_NOTES.md` 참고 수준. 전용 rate-limit/retry 테스트 부족 |
| [x] | redaction 테스트 | Domain + Observability redactor 테스트 |

**B 결론:** **mock + contract/stub 읽기 경로는 코드 완료.** 실 토스 sandbox 1회 스모크(오너 승인·키 로컬)와 rate-limit 운영 증거는 미완.  
**주문 API 구현 없음 → 실주문 경로 없음.**

---

## C. Risk / Orders

| 상태 | 항목 | 현재 증거 (요약) |
|------|------|------------------|
| [~] | risk gate 규칙 구현 + 테스트 | `RiskGate` (stale/missing/session/notional/position) · `LiveOrderGate` · `UsMarketSessionGuard` · `TradingBot.Risk.Tests` (17 tests PASS). **전 한도 규칙·계좌 정합 등 live용 전체 세트는 미완** |
| [x] | dry-run 단위 안정 | `DryRunOrderRouter` + `InMemoryDryRunLedger` · `DryRunLedgerTests` · `OrderRouterTests` · mode ≠ Live |
| [~] | paper trading 기간 및 ledger | `PaperOrderRouter` + `InMemoryPaperLedger` · `PaperLedgerTests` · `EvidenceBuilderTests` (LiveModePresent=false). **다일/기간 운영 ledger export 미첨부** |
| [~] | manual approval 2단계 UX | `LiveOrderContext.ManualApprovalPresent` 게이트 + cockpit Live Lock 표시. **실거래 2단계 승인 UX/버튼 없음(의도적)** |
| [~] | idempotency / duplicate guard | `ClientOrderId` 필드·후보 생성 있음. **중복 제출 차단·멱등 키 저장소 전용 테스트/구현 미완** |
| [x] | live implementation 명시 비활성 | `BlockedLiveOrderRouter` 항상 block/not implemented · `LiveImplementationEnabled` default false · **no `SubmitOrderAsync` in src** · safety + readiness scripts PASS |

**C 결론:** dry-run/paper **골격·단위 증거 OK**. 기간 paper 안정·멱등/중복 가드·live 구현 없음 유지.  
**실주문 라우터는 의도적으로 막혀 있음.**

---

## D. UX / 운영

| 상태 | 항목 | 현재 증거 (요약) |
|------|------|------------------|
| [x] | cockpit에서 live lock / kill switch 명확 | Avalonia safety strip 문구 · Web `LiveLock.razor` / `SafetyStrip.razor` · `CockpitSnapshot` / `LiveLockState` 테스트 |
| [~] | 오너가 상태 설명 이해 가능 | `docs/cockpit/OWNER_WALKTHROUGH.md` · 한국어 문구. **오너 완료 서명/날짜 미첨부** |
| [ ] | incident response 리허설 | 문서 `docs/INCIDENT_RESPONSE.md` 존재. **날짜 찍힌 리허설 기록 없음** |
| [ ] | 오너 서명: live 전환 승인서 | **없음** (Phase 7 전제). 자동화로 생성 금지 |

**D 결론:** UI는 “잠김”을 보여 주는 수준까지 구현. 운영 리허설·오너 서명 전 **live 불가**.

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
risk gate pass                       ← 후보 단위 규칙 partial
dry-run pass                         ← 단위 OK / 기간 운영 증거 partial
paper trading stable                 ← 단위 OK / 기간 증거 없음
valid Toss credential                ← 로컬 .env (에이전트 미열람) / 실스모크 미첨부
account reconciled                   ← 미구현/미증거
limits ok                            ← 일부 Max* 설정만
duplicate guard pass                 ← 미완
idempotency key present              ← ClientOrderId만, 저장소 가드 미완
audit log enabled                    ← InMemoryAuditLog 단위
operator confirms live mode          ← 오너 서명 없음
```

| 상태 | 게이트 | 현재 |
|------|--------|------|
| [ ] | E 전체 동시 충족 | **미충족** |
| [x] | 기본 config fail-closed 유지 | `ALLOW_LIVE_ORDERS=false` · `KILL_SWITCH=true` · `ORDER_MODE=dry_run` |

하나라도 실패 → **block**.  
**현재: 다수 실패(의도된 차단) → live 불가능.**

---

## 오너 요약 (Phase 6)

```text
오너 요약:
- 현재 단계: Phase 6 live readiness — 증거 매핑 진행 중 (자동화 PASS, 사람/운영 증거 미완)
- 안전 상태: 차단 유지 (LIVE_READY=false, LIVE_SAFETY_INTACT=true)
- live order 가능 여부: 불가능
- 오늘 해야 할 결정: (선택) 읽기 전용 실 API 스모크 허용 여부 / paper 운영 기간 합의
- 내가 추천하는 선택: 실주문은 계속 잠금. mock·dry-run·paper 단위 증거를 쌓고, 오너 승인 후에만 TOSS 읽기 스모크
- 이유: 돈 위험 경로(주문 API)가 코드에 없고, 체크리스트 A–E가 전부 끝나지 않음
- 다음 승인 항목: Phase 7 실거래 개방 여부 (지금은 승인하지 말 것)
```

---

## 다음에 채울 증거 (우선순위)

1. 오너 합의 paper 운영 기간 + redacted ledger export (`artifacts/`, secrets 금지)
2. (선택) `TOSS_ALLOW_LIVE_HTTP=true` **읽기 전용** 1회 스모크 로그 (주문 경로 0회 확인)
3. OpenAPI snapshot 주기 재검증 로그
4. gitleaks/trivy hard gate (설치 시)
5. 멱등/중복 가드 설계·테스트 (여전히 live submit 없이)
6. Incident 리허설 날짜 기록
7. Phase 7 오너 승인서 — **마지막**

**Bottom line:** Phase 6는 “차단이 건강한지”를 증명하는 단계입니다.  
**지금은 LIVE_READY=false 가 정답입니다.**
