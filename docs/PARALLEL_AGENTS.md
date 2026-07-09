# 병렬 에이전트 개발 정책 (오너 확정)

**확정일:** 2026-07-09  
**오너 지시:** 모든 개발을 **병렬 worktree** + **최대 병렬 에이전트**로 진행한다.

## 쉬운 설명

| 말 | 의미 |
|----|------|
| 병렬 에이전트 | 여러 AI 작업자가 **서로 다른 일**을 동시에 함 |
| worktree | 에이전트별 **작업실 폴더** (서로 파일 안 덮어씀) |
| 오케스트레이터 | 총괄 — 목표 나누기, 결과 합치기, 안전 검사 |
| 최대 병렬 | 독립 작업이 있으면 **가능한 한 많이** 동시에 띄움 (최소 2, 목표 5–8) |

비유: 공사 현장에서 전기/배관/도장을 **동시에** 하되, 같은 벽을 두 팀이 동시에 뚫지 않게 구역을 나눔.

## 규칙 (필수)

1. **main 폴더에서 기능 구현 금지** → `.worktrees/<이름>/` 에서만 구현.
2. **2개 이상 독립 작업**이면 기본으로 병렬 에이전트.
3. **최대 병렬:** 파일 구역이 겹치지 않는 한 **한 웨이브에 에이전트를 최대로** 배치 (권장 5–8).
4. 각 에이전트는 **전용 worktree + 전용 브랜치** + **담당 경로만** 수정.
5. 공통 파일(`AGENTS.md`, `.env.example`, `TradingBot.sln`, 안전 기본값)은 **오케스트레이터만** 또는 순차 병합.
6. 종료 후: 요약 검토 → 충돌 확인 → 각 worktree `dev-loop`/`dotnet test` → base 브랜치 병합 → main.
7. **실주문·키 출력·live 기본값 완화 금지** (모든 에이전트 공통).
8. 투자 조언·수익 보장 문구 금지.
9. UI/UX 대변경 금지 (현재 콕핏 레이아웃 유지; 상태 문구만 허용).

## 워크트리 표준

```bash
# 웨이브 base (통합 브랜치)
bash scripts/grok/new-worktree.sh wave-base feature/parallel-wave-base

# 에이전트별 (base에서 분기)
cd /path/to/repo
git worktree add -b feature/pwXX-risk .worktrees/pwXX-risk feature/parallel-wave-base
git worktree add -b feature/pwXX-orders .worktrees/pwXX-orders feature/parallel-wave-base
# ... 최대 N개
ln -sfn "$(pwd)/.env" .worktrees/pwXX-risk/.env   # 값 출력 금지
```

| 용도 | 경로 예 | 브랜치 예 |
|------|---------|-----------|
| 통합 base | `.worktrees/wave-base` | `feature/parallel-wave-base` |
| 에이전트 A | `.worktrees/pwXX-risk` | `feature/pwXX-risk` |
| 정식본 | 저장소 루트 | `main` (병합만) |

## 권장 스트림 (로드맵)

| 에이전트 | worktree | 구역 | 목표 |
|----------|----------|------|------|
| A risk | `pwXX-risk` | `Risk/`, Risk.Tests | 리스크 게이트 보강 |
| B orders | `pwXX-orders` | `Orders/`, Orders.Tests | dry-run/paper 증거 |
| C obs | `pwXX-obs` | `Observability/` | 감사 로그 구조화 |
| D phase6 | `pwXX-phase6` | `docs/plans`, LIVE_READINESS* | Phase 6 증거 문서 |
| E toss | `pwXX-toss` | `Infrastructure.Toss/`, Toss.Tests | 읽기 스모크(주문 없음) |
| F app-status | `pwXX-app` | `App/` 최소, App.Tests | 연결 상태 표시만 |
| G domain | `pwXX-domain` | `Domain/`, Domain.Tests | 카탈로그·불변식 테스트 |
| H scripts | `pwXX-scripts` | `scripts/grok/` | 병렬 웨이브 헬퍼 스크립트 |

## 오케스트레이터 체크리스트

- [ ] 웨이브 base 브랜치/워크트리 준비
- [ ] 작업 분할 및 **파일 구역 공지** (겹침 0)
- [ ] 에이전트별 worktree 생성 + `.env` 링크
- [ ] **최대 병렬** 실행
- [ ] 결과 통합 (base로 merge)
- [ ] `bash scripts/grok/dev-loop.sh` (또는 동등 검증)
- [ ] main/GitHub 병합은 검증 후
- [ ] 오너에게 쉬운 보고

## 하지 말 것

- main에서 직접 코딩
- 한 에이전트에게 “전체 시스템 다 해”
- 실주문 루프를 병렬로 돌리기
- 검증 없이 강제 병합
- 에이전트끼리 같은 파일 수정
