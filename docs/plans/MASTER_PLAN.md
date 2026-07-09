# MASTER PLAN — 토스 나스닥 자동매매 (실거래 목적)

**오너 목표:** 실거래 + 사용자 중심 UI/UX  
**개발 방식:** 모든 작업은 git worktree  
**안전:** live order 기본 차단, fail-closed  
**날짜:** 2026-07-09

## 한 줄

토스 Open API로 나스닥 자동매매를 **실거래까지** 하되, 오너가 cockpit으로 상태·위험·승인을 이해하며 운영한다.

## Phase 지도

| Phase | 이름 | 돈 위험 | 통과 기준 (요약) |
|-------|------|---------|------------------|
| 0 | 하네스 | 없음 | build/test/scan, live 차단 (대부분 완료) |
| 1 | 사용자 cockpit 설계 | 없음 | 오너가 화면 구조 승인 |
| 2 | 토스 읽기 전용 연결 | 매우 낮음 | 계좌·시세 읽기 + 마스킹 |
| 3 | 신호 + 리스크 게이트 | 없음(후보만) | 위험 후보 block 테스트 |
| 4 | dry-run | 없음 | 파이프 안정·감사 로그 |
| 5 | paper trading | 없음 | 기간 증거·오너 이해 |
| 6 | live readiness | 없음(아직 잠금) | 체크리스트 전부 증거 |
| 7 | 실거래 개방 (좁게) | 있음 | 소액·한도·kill switch |
| 8 | 운영 안정화 | 관리 | 장애 대응·개선 |

## 현재 위치

- Phase 0 완료에 가까움
- **지금 worktree에서:** Phase 0 마무리(정책) + Phase 1 착수 준비
- 활성 worktree: `.worktrees/active-dev`

## 작업 규칙

1. worktree에서만 구현
2. 단계 통과 증거 없이 다음 phase로 강제 이동 금지
3. 실거래(Phase 7) 전 Phase 6 필수
4. UI는 사용자 중심 cockpit 우선

## 다음 실행

1. Phase 1 상세 플랜 (`docs/plans/PHASE_01_cockpit.md`)
2. cockpit 와이어/상태 모델 오너 확인
3. Phase 2 read-only Toss 상세 플랜
