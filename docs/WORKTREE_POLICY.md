# Worktree 개발 정책 (오너 확정)

**확정일:** 2026-07-09  
**오너 결정:** 모든 개발 작업은 git worktree로 진행한다.

## 쉬운 설명

- **main 폴더** = 정식 버전 (안정, GitHub와 맞춤)
- **worktree 폴더** = 작업실 (기능 개발·실험)
- 작업이 검증되면 main에 합친 뒤 GitHub에 올린다.

## 규칙

1. **새 기능 / 문서 / 설정 변경**은 `main` 폴더에서 직접 하지 않는다.
2. 작업 전 반드시 worktree를 만들거나, 기존 active worktree를 사용한다.
3. worktree 경로는 프로젝트 루트의 `.worktrees/<이름>/` 이다.
4. `.worktrees/` 는 git에 올리지 않는다 (`.gitignore`).
5. `.env` 비밀키는 main의 `.env`를 심볼릭 링크로만 연결한다. 복사본을 git에 넣지 않는다.
6. worktree에서 테스트·안전 스캔 통과 후에만 main 병합을 검토한다.
7. live order 기본 차단은 worktree에서도 동일하다.
8. 작업 완료·병합 후 사용 끝난 worktree는 정리한다.

## 표준 명령

```bash
# 새 작업 공간
bash scripts/grok/new-worktree.sh <이름> <브랜치이름>

# 목록
git worktree list

# 정리 (병합 후)
git worktree remove .worktrees/<이름>
```

## 현재 기본 작업 공간

| 용도 | 경로 | 브랜치 |
|------|------|--------|
| 웨이브 통합 base | `.worktrees/wave-base` | `feature/parallel-wave-base` |
| 에이전트 병렬 작업실 | `.worktrees/pwXX-*` | `feature/pwXX-*` |
| (구) active-dev | `.worktrees/active-dev` | `feature/worktree-all-dev` |
| 정식본 | 저장소 루트 | `main` (병합만) |

## 에이전트 규칙

- Grok 에이전트는 구현 작업을 **worktree 경로에서** 수행한다.
- **병렬 웨이브:** 독립 작업마다 worktree 1개 + 브랜치 1개 + 에이전트 1개 (최대 병렬).
- 보고 시 어느 worktree/브랜치에서 작업했는지 명시한다.
- main 직접 수정 금지. 필요 시 즉시 worktree로 이전한다.
- 상세: `docs/PARALLEL_AGENTS.md`

## 개발 루프

worktree에서 작업이 끝나면 병합 전 개발 루프 검증을 돌린다.

```bash
bash scripts/grok/dev-loop.sh
```

상세: `docs/DEV_LOOP.md`

