# Phase 1 — 사용자 중심 Cockpit 설계

**상태:** **오너 승인 완료** (2026-07-09) → Phase 2로 진행 가능  
**작업 위치:** `.worktrees/active-dev` (`feature/worktree-all-dev`)  
**돈 위험:** 없음 (화면·문서·상태 모델만, 실주문 없음)  
**실거래와의 연결:** 실거래 때 오너가 **무엇을 보고 승인·정지할지**를 지금 고정한다.

---

## 1. 목표

오너(비개발자)가 브라우저/앱을 열었을 때 **10초 안에** 아래를 이해한다.

1. 봇이 지금 뭐 하는가?  
2. 실거래가 잠겨 있는가? (기본: 잠김)  
3. 왜 주문이 안 나가는가 / 막혔는가?  
4. 내가 지금 할 일(승인·확인·긴급정지)은 무엇인가?  
5. kill switch는 어디에 있는가?

## 2. 성공 모습 (오너 관점)

- 홈만 봐도 “안전 / 주의 / 막힘 / (나중) 실거래 준비”가 **글자로** 보임  
- “매수하기” 같은 충동 버튼이 **없음**  
- 막히면 **이유 문장**이 있음  
- 실거래 관련 조작은 **불편하고 단계가 많음** (실수 방지)  
- 디자이너가 색·배치를 바꿔도 **의미(잠김/차단)는 못 바꿈**

## 3. 범위

### 이번 Phase에 포함

- 화면 지도·와이어프레임·상태 모델  
- 홈(Overview) 정보 우선순위  
- 네비게이션 구조  
- 카피(문구) 규칙  
- UI 상태 enum / cockpit projection (코드 골격)  
- 오너 승인용 체크리스트  

### 이번 Phase에 포함하지 않음

- 실제 Blazor/Avalonia 화면 구현 완료  
- 토스 API 실연결 (Phase 2)  
- 전략·실주문 (Phase 3+)  
- UI 프레임워크 최종 강제 (오너 결정 전 추천만)

## 4. 돈·보안 리스크

| 리스크 | 대응 |
|--------|------|
| 실주문 버튼 오인 | 홈에 실주문 CTA 없음. Live Lock 기본 |
| 계좌번호 노출 | 마스킹 전제, Settings는 가이드만 |
| “수익 보장” 문구 | 카피 가이드 금지 목록 |

## 5. 오너 결정 항목 (Phase 1 통과에 필요)

1. **화면 구조(이 문서의 지도) 승인**  
2. **UI 위치:** 브라우저 dashboard 권장 (미결정이어도 Phase 2 진행 가능, 구현 직전 확정)  
3. 홈에 **꼭 보고 싶은 추가 정보**가 있는지  

## 6. 산출물

| 파일 | 내용 |
|------|------|
| `docs/plans/PHASE_01_cockpit.md` | 이 플랜 |
| `docs/cockpit/SCREEN_MAP.md` | 화면 지도·우선순위 |
| `docs/cockpit/WIREFRAME.md` | 텍스트 와이어프레임 |
| `docs/cockpit/OWNER_WALKTHROUGH.md` | 하루 운영 시나리오 |
| `docs/UI_STATE_MODEL.md` | 상태 모델 갱신 |
| `src/TradingBot.Ui/*` | cockpit 상태 projection 골격 |
| `tests/TradingBot.Ui.Tests/*` | 상태 모델 테스트 |

## 7. 개발 루프 통과 조건

- secret / trading safety / `dotnet test` PASS  
- 오너가 화면 구조 **승인** (주관 게이트)  

## 8. 롤백

문서·UI 골격만 삭제/되돌리면 됨. 거래 영향 없음.

## 9. Phase 2로 넘어가는 조건

- [x] 오너 화면 구조 승인 (2026-07-09) — `docs/cockpit/PHASE_01_APPROVAL.md`  
- [x] dev-loop PASS (설계 커밋 시)  
- [x] Live Lock / Kill Switch / 차단 사유가 홈에 있는 것 합의  

**Phase 1 종료.** 다음: `docs/plans/PHASE_02_toss_readonly.md`
