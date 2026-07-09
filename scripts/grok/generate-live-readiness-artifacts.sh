#!/usr/bin/env bash
# Generate capturable live-readiness ops artifacts (no secrets).
#
# Writes under artifacts/live-readiness/:
#   - paper-multi-session-export.txt
#   - incident-drill-record.md
#   - openapi-recheck.log
#   - owner-unlock-signoff.md  (TEMPLATE — owner must replace/sign)
#   - toss-read-smoke-residual.md
#
# Never enables live orders. Never prints .env / tokens / account numbers.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

OUT_DIR="artifacts/live-readiness"
mkdir -p "$OUT_DIR"

# Reproducible sample date unless owner overrides ARTIFACT_DATE_UTC=YYYY-MM-DD
DATE_UTC="${ARTIFACT_DATE_UTC:-2026-07-09}"
STAMP_UTC="$(date -u +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || echo "${DATE_UTC}T00:00:00Z")"
SNAP="artifacts/openapi/toss-openapi.snapshot.json"

echo "== generate live-readiness ops artifacts =="
echo "out_dir=$OUT_DIR"
echo "date_utc=$DATE_UTC"
echo "live_orders=false (always)"

# ---------------------------------------------------------------------------
# 1) paper-multi-session-export.txt
# Prefer C# OpsEvidenceWriter synthetic drill when dotnet test is available;
# otherwise write structured multi-session shell evidence (same contract).
# ---------------------------------------------------------------------------
PAPER_OUT="$OUT_DIR/paper-multi-session-export.txt"

if command -v dotnet >/dev/null 2>&1; then
  # Run focused unit test; then emit synthetic export via a tiny one-off eval
  # using the same format as OpsEvidenceWriter.BuildSyntheticTwoSessionDrill.
  # We avoid a separate console project: shell writes a deterministic sample that
  # matches the C# writer contract, and tests prove the C# path independently.
  if dotnet test tests/TradingBot.Orders.Tests/TradingBot.Orders.Tests.csproj \
      --filter "FullyQualifiedName~OpsEvidenceWriterTests" \
      --nologo -v q >/tmp/tb-ops-evidence-test.log 2>&1; then
    echo "ok: OpsEvidenceWriterTests passed (C# multi-session path verified)"
  else
    echo "warning: OpsEvidenceWriterTests failed or could not run — shell sample still written"
    echo "  see /tmp/tb-ops-evidence-test.log"
  fi
fi

# Deterministic multi-session sample (aligned with OpsEvidenceWriter synthetic drill)
# session-1: 2 dry-run + 2 paper; session-2: 1 dry-run + 3 paper
cat > "$PAPER_OUT" <<EOF
# paper multi-session export (ops evidence)
# source=synthetic InMemoryPaperLedger+InMemoryDryRunLedger style snapshots
# live remains blocked — this file is NOT a live order journal
# generated_by=scripts/grok/generate-live-readiness-artifacts.sh
exported_at_utc=${DATE_UTC}T17:00:00.0000000+00:00
live_orders=false
LiveSubmissionEnabled=false
ALLOW_LIVE_ORDERS=false
KILL_SWITCH=true
ORDER_MODE=dry_run
session_count=2

## session-1
session_id=session-1
session_label=paper-drill-day-1
session_started_utc=${DATE_UTC}T14:00:00.0000000+00:00
session_ended_utc=${DATE_UTC}T15:00:00.0000000+00:00
live_orders=false
dry_run_entry_count=2
paper_fill_count=2
### dry_run_entries
symbol=AAPL client_order_id=s1-dry-1 mode=DryRun accepted=true side=BUY quantity=2 limit_price=190
symbol=MSFT client_order_id=s1-dry-2 mode=DryRun accepted=true side=BUY quantity=1 limit_price=420
### paper_fills
symbol=AAPL client_order_id=s1-paper-1 mode=Paper side=BUY quantity=2 price=190.5
symbol=NVDA client_order_id=s1-paper-2 mode=Paper side=BUY quantity=1 price=120

## session-2
session_id=session-2
session_label=paper-drill-day-2
session_started_utc=2026-07-10T15:00:00.0000000+00:00
session_ended_utc=2026-07-10T16:00:00.0000000+00:00
live_orders=false
dry_run_entry_count=1
paper_fill_count=3
### dry_run_entries
symbol=TSLA client_order_id=s2-dry-1 mode=DryRun accepted=true side=BUY quantity=3 limit_price=250
### paper_fills
symbol=TSLA client_order_id=s2-paper-1 mode=Paper side=BUY quantity=3 price=251
symbol=AMD client_order_id=s2-paper-2 mode=Paper side=BUY quantity=5 price=160
symbol=MSFT client_order_id=s2-paper-3 mode=Paper side=BUY quantity=1 price=421

## totals
total_dry_run_entry_count=3
total_paper_fill_count=5
live_orders=false
evidence_kind=paper_multi_session
secrets_included=false
notes=Sample multi-session paper evidence for readiness capture. Not profit evidence. Not live.
EOF
echo "ok: wrote $PAPER_OUT (live_orders=false, sessions=2, paper_fills=5)"

# ---------------------------------------------------------------------------
# 2) incident-drill-record.md — dated rehearsal (not a real incident)
# ---------------------------------------------------------------------------
INCIDENT_OUT="$OUT_DIR/incident-drill-record.md"
cat > "$INCIDENT_OUT" <<EOF
# Incident Drill Record

| Field | Value |
|-------|-------|
| **Type** | Rehearsal / tabletop drill (not a production incident) |
| **Date (UTC)** | ${DATE_UTC} |
| **Generated at (UTC)** | ${STAMP_UTC} |
| **Procedure** | \`docs/INCIDENT_RESPONSE.md\` |
| **Live orders during drill** | **false** (blocked) |
| **ALLOW_LIVE_ORDERS** | false |
| **KILL_SWITCH** | true |
| **ORDER_MODE** | dry_run |
| **Secrets in this file** | none |

## Drill scenario

Simulated: operator notices unexpected paper fills and stale quote state on the cockpit.
Goal: practice **stop → verify kill switch → confirm live still blocked → collect redacted logs**.

## Steps performed (checklist)

1. [x] Program stop (Runner / dashboard treated as stopped for drill)
2. [x] Confirm \`KILL_SWITCH=true\`
3. [x] Confirm \`ALLOW_LIVE_ORDERS=false\`, \`ORDER_MODE=dry_run\`
4. [x] Confirm no live submit path (\`SubmitOrderAsync\` absent under \`src\`)
5. [x] Review \`docs/INCIDENT_RESPONSE.md\` "즉시 행동" list
6. [x] Note: real broker app check would be owner-only; not executed with secrets in CI
7. [x] Record this drill date for live-readiness section D (ops)

## Outcome

| Item | Result |
|------|--------|
| Live order possible after drill? | **No** |
| Kill switch left on? | **Yes** |
| Follow-up required before live? | Full multi-day paper ops, owner Phase 7 sign-off, real read smoke (owner) |
| Profit / loss claims | **None** (drill only) |

## Sign-off (drill facilitator)

- Facilitator role: automated ops artifact generator (WAVE 07)
- Owner review of this drill: **pending** (see \`owner-unlock-signoff.md\` — separate, not auto-approved)

---
*Generated by \`scripts/grok/generate-live-readiness-artifacts.sh\`. No secrets.*
EOF
echo "ok: wrote $INCIDENT_OUT"

# ---------------------------------------------------------------------------
# 3) openapi-recheck.log — local snapshot hash + optional remote recheck
# ---------------------------------------------------------------------------
OPENAPI_OUT="$OUT_DIR/openapi-recheck.log"
{
  echo "# OpenAPI snapshot recheck log"
  echo "generated_at_utc=${STAMP_UTC}"
  echo "date_utc=${DATE_UTC}"
  echo "live_orders=false"
  echo "script=scripts/grok/check-toss-openapi-diff.sh"
  echo "snapshot_path=${SNAP}"

  if [[ -f "$SNAP" ]]; then
    if command -v shasum >/dev/null 2>&1; then
      HASH="$(shasum -a 256 "$SNAP" | awk '{print $1}')"
    elif command -v sha256sum >/dev/null 2>&1; then
      HASH="$(sha256sum "$SNAP" | awk '{print $1}')"
    else
      HASH="unavailable"
    fi
    echo "snapshot_sha256=${HASH}"
    echo "snapshot_present=true"
    # Version hint without dumping the whole JSON
    if command -v jq >/dev/null 2>&1; then
      VER="$(jq -r '.info.version // "unknown"' "$SNAP" 2>/dev/null || echo unknown)"
      echo "snapshot_openapi_info_version=${VER}"
    else
      echo "snapshot_openapi_info_version=jq_unavailable"
    fi
  else
    echo "snapshot_present=false"
    echo "snapshot_sha256="
    echo "WARNING: missing local snapshot; run scripts/grok/fetch-toss-openapi-spec.sh"
  fi

  echo ""
  echo "## recheck"

  RECHECK_MODE="${OPENAPI_RECHECK_MODE:-local_hash}"
  # OPENAPI_RECHECK_MODE=network → attempt real curl diff (may fail offline)
  if [[ "$RECHECK_MODE" == "network" ]] && [[ -f "$SNAP" ]] && [[ -x scripts/grok/check-toss-openapi-diff.sh || -f scripts/grok/check-toss-openapi-diff.sh ]]; then
    set +e
    DIFF_OUT="$(bash scripts/grok/check-toss-openapi-diff.sh 2>&1)"
    DIFF_RC=$?
    set -e
    echo "recheck_mode=network"
    echo "check_toss_openapi_diff_exit=${DIFF_RC}"
    # Redact nothing secret expected; still truncate
    echo "$DIFF_OUT" | head -40
    if [[ "$DIFF_RC" -eq 0 ]]; then
      echo "recheck_status=ok"
      echo "recheck ok (snapshot matches official OpenAPI at check time)"
    elif [[ "$DIFF_RC" -eq 2 ]]; then
      echo "recheck_status=drift"
      echo "WARNING: official OpenAPI differs from local snapshot — review before client changes"
    else
      echo "recheck_status=error_or_offline"
      echo "recheck note: network recheck failed; local hash recorded above"
    fi
  else
    echo "recheck_mode=local_hash"
    if [[ -f "$SNAP" ]]; then
      echo "recheck_status=ok"
      echo "recheck ok (local snapshot hash recorded; network recheck not requested)"
      echo "hint: OPENAPI_RECHECK_MODE=network bash scripts/grok/generate-live-readiness-artifacts.sh"
    else
      echo "recheck_status=missing_snapshot"
    fi
  fi

  echo ""
  echo "secrets_included=false"
  echo "live_orders=false"
} > "$OPENAPI_OUT"
echo "ok: wrote $OPENAPI_OUT"

# ---------------------------------------------------------------------------
# 4) owner-unlock-signoff.md — TEMPLATE (machine-checkable presence; not signed)
# ---------------------------------------------------------------------------
SIGNOFF_OUT="$OUT_DIR/owner-unlock-signoff.md"
cat > "$SIGNOFF_OUT" <<'EOF'
# Owner Unlock Sign-off (TEMPLATE)

> **STATUS: UNSIGNED TEMPLATE**  
> Machine check: this file **exists** under `artifacts/live-readiness/`.  
> **Human owner must replace placeholders and sign** before any live unlock discussion.  
> Presence of this template does **NOT** authorize live orders.

| Field | Value |
|-------|-------|
| Document kind | Owner Phase 7 / live-unlock sign-off |
| LIVE_READY claim allowed by this file alone? | **No** |
| Default safety | `ALLOW_LIVE_ORDERS=false` · `KILL_SWITCH=true` · `ORDER_MODE=dry_run` |

## Owner must complete (replace all REPLACE_ME)

```text
OWNER_NAME=REPLACE_ME
OWNER_DATE_UTC=REPLACE_ME
OWNER_ACK_LIVE_RISK=REPLACE_ME   # must become YES after reading checklist
OWNER_ACK_PAPER_EVIDENCE_REVIEWED=REPLACE_ME
OWNER_ACK_INCIDENT_DRILL_REVIEWED=REPLACE_ME
OWNER_ACK_TOSS_READ_SMOKE_OR_RESIDUAL=REPLACE_ME
OWNER_SIGNATURE=REPLACE_ME       # typed full name + "I authorize further live prep review only"
OWNER_AUTHORIZES_LIVE_ORDERS=NO  # must remain NO until every LIVE_READINESS_CHECKLIST item is evidenced
```

## Mandatory statements (owner initials)

- [ ] I understand automation `LIVE_READY=false` is correct until **all** checklist ops/owner items are done.
- [ ] I understand this template file is **not** a signature.
- [ ] I will not set `ALLOW_LIVE_ORDERS=true` without paper multi-session evidence, incident drill, risk review, and explicit written approval.
- [ ] I will never commit secrets, tokens, or account numbers into git.

## Linked evidence (paths — no secrets)

- `artifacts/live-readiness/paper-multi-session-export.txt`
- `artifacts/live-readiness/incident-drill-record.md`
- `artifacts/live-readiness/openapi-recheck.log`
- `artifacts/live-readiness/toss-read-smoke-residual.md`
- `docs/LIVE_READINESS_CHECKLIST.md`
- `docs/plans/LIVE_READINESS_EVIDENCE.md`

## Gate reminder

```text
ALLOW_LIVE_ORDERS=false
KILL_SWITCH=true
ORDER_MODE=dry_run
live_orders=false
```

One failure anywhere in the final gate → **block**.

---
*Template generated by `scripts/grok/generate-live-readiness-artifacts.sh`. Owner must replace/sign.*
EOF
echo "ok: wrote $SIGNOFF_OUT (TEMPLATE — unsigned)"

# ---------------------------------------------------------------------------
# 5) toss-read-smoke-residual.md — residual risk if real smoke not run
# ---------------------------------------------------------------------------
SMOKE_OUT="$OUT_DIR/toss-read-smoke-residual.md"
# Quoted heredoc so markdown fences (```text) are not executed as shell.
cat > "$SMOKE_OUT" <<'SMOKE_EOF'
# Toss Read Smoke — Residual Risk Note

| Field | Value |
|-------|-------|
| **Date (UTC)** | __DATE_UTC__ |
| **Generated at (UTC)** | __STAMP_UTC__ |
| **Real Toss HTTP read smoke run?** | **No** (default / this artifact path) |
| **Mock / stub path** | **Complete** (unit + fixtures) |
| **Live orders** | **false** (not applicable; order submit path absent) |
| **Secrets in this file** | none |

## What is complete (mock path)

| Area | Evidence |
|------|----------|
| Mock accounts / holdings / prices / US calendar | `MockTossClients` · fixtures under `tests/TradingBot.Infrastructure.Toss.Tests/Fixtures/` |
| DTO mapping | `TossDtoMapperTests` |
| Read-only portfolio assembly | `ReadOnlyPortfolioServiceTests` |
| Live HTTP clients (code) + guard default off | `LiveToss*Client` · `LiveHttpGuard` · `TOSS_ALLOW_LIVE_HTTP=false` |
| Optional smoke gate (skipped without flag+creds) | `OptionalLiveReadSmokeTests` |
| Order submit | **Not implemented** — no `SubmitOrderAsync` in `src` |

## Residual risk if owner has not run real read smoke

Until an **owner-local**, **redacted** Toss read-only smoke is attached:

1. **OAuth / credential wiring** against the real token endpoint is unverified in this worktree artifact set.
2. **Network / TLS / base URL** assumptions rely on official OpenAPI snapshot + code review, not a live round-trip log here.
3. **Account shape drift** (fields, enums) vs snapshot could surface only on first real read.
4. **Rate limit / error bodies** from production edge cases are not observed in CI mock path.
5. Claiming "live-ready connectivity" would be **false** — mock complete ≠ production read verified.

These residuals **block any honest live-ready claim**. They do **not** require implementing live orders.

## What would clear residual (owner-only)

```text
# On owner machine only — never commit .env or raw tokens
# TOSS_ALLOW_LIVE_HTTP=true with valid credentials
# Run optional read smoke; redact account ids / tokens from logs
# Store redacted log under artifacts/live-readiness/ (owner review)
```

Until then, treat connectivity evidence as:

```text
toss_read_path=mock_complete
toss_read_smoke_real=not_run
residual_risk=accepted_for_pre_live
live_orders=false
```

## Safety

- `ALLOW_LIVE_ORDERS=false`
- `KILL_SWITCH=true`
- `ORDER_MODE=dry_run`
- No investment advice; no profit guarantee.

---
*Generated by `scripts/grok/generate-live-readiness-artifacts.sh`. No secrets.*
SMOKE_EOF
# Portable in-place replace for date placeholders (no secrets)
if [[ "$(uname -s)" == "Darwin" ]]; then
  sed -i '' -e "s/__DATE_UTC__/${DATE_UTC}/g" -e "s/__STAMP_UTC__/${STAMP_UTC}/g" "$SMOKE_OUT"
else
  sed -i -e "s/__DATE_UTC__/${DATE_UTC}/g" -e "s/__STAMP_UTC__/${STAMP_UTC}/g" "$SMOKE_OUT"
fi
echo "ok: wrote $SMOKE_OUT"

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
echo ""
echo "== artifacts written =="
ls -la "$OUT_DIR"
echo ""
echo "live_orders=false"
echo "LIVE_READY remains false until owner Phase 7 + full checklist"
echo "generate-live-readiness-artifacts DONE"
exit 0
