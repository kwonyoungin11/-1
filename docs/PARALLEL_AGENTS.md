# 병렬 에이전트 개발 정책 — **필수 (MANDATORY)**

**확정일:** 2026-07-09  
**오너 지시:** 병렬 worktree + **최대** 병렬 에이전트는 **필수 사항**이다.  
**등급:** 권장(recommended) 아님 → **필수(required)**. 예외 없음.

## 한 줄

**구현은 worktree에서만. 독립 일은 최대 에이전트로 동시에. main에서 코딩 금지.**

## 쉬운 설명

| 말 | 의미 |
|----|------|
| 병렬 에이전트 | 여러 AI 작업자가 **서로 다른 일**을 동시에 함 |
| worktree | 에이전트별 **작업실 폴더** (파일 안 덮어씀) |
| 최대 병렬 | 구역이 안 겹치면 **가능한 한 많이** 동시 실행 (목표 5–8, 최소 2) |
| 오케스트레이터 | 총괄 — 분할·합치기·안전 검사 |

## 필수 규칙 (MUST)

1. **MUST** 모든 구현·문서·설정 변경은 `.worktrees/<이름>/` 에서 한다.
2. **MUST NOT** `main` 루트에서 기능 구현한다.
3. **MUST** 독립 작업이 2개 이상이면 병렬 에이전트를 띄운다.
4. **MUST** 파일 구역이 겹치지 않는 한 **최대 병렬** (목표 5–8 에이전트).
5. **MUST** 에이전트마다 **전용 worktree + 전용 브랜치 + 전용 경로 구역**.
6. **MUST** 웨이브 시작 시 `scripts/grok/parallel-wave-setup.sh` (또는 동등) 사용.
7. **MUST** 종료 후: 요약 → 충돌 확인 → 테스트/dev-loop → base 병합 → main.
8. **MUST NOT** 실주문 개방, 키 출력, live 기본값 완화, 투자 조언 문구.
9. **MUST NOT** 한 에이전트에게 “전체 시스템 다 해”.
10. **MUST** UI 대변경 없이 상태 표시만 (현재 콕핏 레이아웃 유지).

## 위반 시

- 작업을 **중단**하고 worktree/병렬 분할을 먼저 고친다.
- main에 직접 커밋한 변경은 worktree 브랜치로 **이전**한 뒤 진행한다.
- 단일 에이전트 순차 구현으로 대체하지 않는다 (필수 위반).

## 워크트리 표준

```bash
# 통합 base
bash scripts/grok/new-worktree.sh wave-base feature/parallel-wave-base

# 최대 병렬 웨이브 (예: wave 06, 8 agents)
bash scripts/grok/parallel-wave-setup.sh 06 feature/parallel-wave-base \
  risk orders obs phase6 toss scripts domain app-tests
```

| 용도 | 경로 | 브랜치 |
|------|------|--------|
| 통합 base | `.worktrees/wave-base` | `feature/parallel-wave-base` |
| 에이전트 N | `.worktrees/pwXX-<slug>` | `feature/pwXX-<slug>` |
| 정식본 | 저장소 루트 | `main` (병합만) |

## 오케스트레이터 체크리스트 (매 웨이브)

- [ ] wave base 준비
- [ ] 작업 분할 + **경로 구역 겹침 0**
- [ ] `parallel-wave-setup.sh` 로 **최대** 에이전트 worktree 생성
- [ ] 에이전트 **동시** 기동 (순차 금지)
- [ ] 결과 base 병합
- [ ] `dotnet test` + safety scan / `dev-loop.sh`
- [ ] main/GitHub는 검증 후
- [ ] 오너 쉬운 보고

## 관련

- `docs/WORKTREE_POLICY.md`
- `AGENTS.md` Absolute rules — MANDATORY section
- `.grok/skills/parallel-worktree-max-agents/SKILL.md`
- `scripts/grok/parallel-wave-setup.sh`
