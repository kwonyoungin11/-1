# CURRENT_STATE

```text
현재 날짜: 2026-07-09
프로젝트: C# / .NET 기반 토스증권 Open API 나스닥 자동매매 시스템
개발 에이전트: Grok 4.5 / Grok Build
사용자 역할: 비개발자 오너

## 오너 확정 목표 (2026-07-09)
최종 목적: 실거래 (live trading)
UI/UX 원칙: 사용자 중심 (비개발자 오너가 상태·위험·다음 행동을 바로 이해)

## 해석 (에이전트 합의)
- dry-run / paper / risk gate / checklist 는 "최종 목적"이 아니라
  실거래를 안전하게 열기 위한 필수 경로이다.
- 실거래를 미루는 프로젝트가 아니다. 검증 없는 실거래는 하지 않는다.
- UI는 개발자 디버그 화면이 아니라, 오너 cockpit (상태·차단 사유·승인·긴급정지 중심).

## 안전 상태 (현재)
API key: .env 사용자 입력 (값 출력 금지)
.env: git 제외, TOSS_* 정리됨
Trading safety: ALLOW_LIVE_ORDERS=false, KILL_SWITCH=true, ORDER_MODE=dry_run
live order 가능 여부: 아직 불가능 (준비 단계)
Mac: dotnet 10.0.301, pwsh, gitleaks, trivy, osv-scanner 설치됨
build/test: PASS

## 다음 작업 (실거래 목적 로드맵)
1. 사용자 중심 cockpit 요구사항 고정 (무엇을 보면 안심/행동하는가)
2. read-only Toss 연결 (계좌/시세) — 실거래 전 필수 기초
3. risk gate + dry-run + paper 로 실거래 자격 쌓기
4. live readiness checklist 전부 증거화 후 live 개방
```
