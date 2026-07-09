# Worktree 개발 정책 — **필수 (MANDATORY)**

**확정일:** 2026-07-09  
**오너 결정:** 모든 개발 작업은 git worktree로 진행한다.  
**등급:** **필수**. 병렬 에이전트와 함께 예외 없이 적용.

## 쉬운 설명

- **main 폴더** = 정식 버전 (안정, GitHub와 맞춤) — **코딩 금지**
- **worktree 폴더** = 작업실 (기능 개발·실험)
- **에이전트마다 작업실 하나** = 병렬로 동시에 일함
- 검증되면 base → main에 합친 뒤 GitHub에 올린다.

## 필수 규칙 (MUST)

1. **MUST NOT** 새 기능 / 문서 / 설정을 `main` 폴더에서 직접 한다.
2. **MUST** 작업 전 worktree를 만들거나 기존 wave/agent worktree를 쓴다.
3. **MUST** 경로는 프로젝트 루트의 `.worktrees/<이름>/`.
4. **MUST** `.worktrees/` 는 gitignore (저장소에 올리지 않음).
5. **MUST** `.env` 는 main `.env` 심볼릭 링크만. 값 출력 금지.
6. **MUST** worktree에서 테스트·안전 스캔 통과 후에만 main 병합 검토.
7. **MUST** live order 기본 차단은 worktree에서도 동일.
8. **MUST** 독립 작업 2+ → **에이전트별 worktree 병렬 (최대 개수)**.
9. 작업 완료·병합 후 끝난 worktree는 정리한다.

## 표준 명령

```bash
# 단일 작업실
bash scripts/grok/new-worktree.sh <이름> [브랜치] [base-branch]

# 병렬 웨이브 (필수 도구)
bash scripts/grok/parallel-wave-setup.sh <wave#> <base-branch> <slug1> <slug2> ...

# 목록 / 정리
git worktree list
git worktree remove .worktrees/<이름>
```

## 현재 기본 작업 공간

| 용도 | 경로 | 브랜치 |
|------|------|--------|
| 웨이브 통합 base | `.worktrees/wave-base` | `feature/parallel-wave-base` |
| 에이전트 병렬 작업실 | `.worktrees/pwXX-*` | `feature/pwXX-*` |
| 정식본 | 저장소 루트 | `main` (병합만) |

## 에이전트 규칙

- **MUST** 구현을 worktree 경로에서 수행한다.
- **MUST** 병렬 웨이브: 독립 작업마다 worktree 1 + 브랜치 1 + 에이전트 1 (최대 병렬).
- 보고 시 worktree/브랜치를 명시한다.
- 상세 병렬 정책: `docs/PARALLEL_AGENTS.md`
