# CURRENT_STATE

```text
현재 날짜: 2026-07-09
프로젝트: C# / .NET 토스증권 Open API 나스닥 자동매매
최종 목적: 실거래 (live)
UI: 사용자 중심 cockpit
개발 방식: 모든 작업 git worktree
검증 방식: 개발 루프 공식 채택 (docs/DEV_LOOP.md, scripts/grok/dev-loop.sh)
  - secret + safety + owner-readiness + dotnet test
  - 최대 5회 (상한 10), safety BLOCK 즉시 중단
  - 실주문 루프 아님

활성 worktree: /Users/kwon/Documents/c#/.worktrees/active-dev
활성 브랜치: feature/worktree-all-dev
정식본: main

안전: ALLOW_LIVE_ORDERS=false, KILL_SWITCH=true, ORDER_MODE=dry_run
다음: Phase 1 cockpit 상세 (worktree + dev-loop)
```
