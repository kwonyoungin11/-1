# Plan: Initial harness (2026-07-09)

## 목표
Grok 4.5용 초반세팅 — 기능 실거래 없음.

## 변경 파일
AGENTS.md, .grok/*, docs/*, scripts/grok/*, src/* scaffold, tests safety, .env.example, .gitignore

## trading safety 영향
Positive: defaults fail-closed, scans, blocked live router

## UX/UI 영향
Docs only; framework undecided

## Toss API 영향
Snapshot + notes only; no order calls

## secret 영향
.gitignore + scans; .env placeholder only

## test 전략
xUnit for defaults, live gate, routers (requires SDK)

## rollback
git revert harness commit; no production deps

## 사용자 승인 필요
- brew install --cask dotnet-sdk
- (optional) gitleaks, trivy, pwsh
- UI surface preference later
