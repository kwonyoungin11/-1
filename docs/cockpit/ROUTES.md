# Cockpit 라우트 표 (MVP 8)

**날짜:** 2026-07-09  
**범위:** Blazor 페이지 경로 · 메뉴 · 오너용 설명  
**규칙:** 실주문 CTA 없음. 메뉴에 “매수 실행 / 실주문” 링크 금지.

관련: `docs/cockpit/SCREEN_MAP.md`, `docs/cockpit/WIREFRAME.md`, `docs/cockpit/BINDING_MODEL.md`

---

## MVP 8 라우트

| ID | 화면 (오너 이름) | 경로 | Razor 파일 | 네비 라벨 | 상태 | 바인딩 / 메모 |
|----|------------------|------|------------|-----------|------|----------------|
| S01 | Overview (홈) | `/` | `Components/Pages/Home.razor` | Overview (홈) | **골격 완료** | `CockpitDashboardModel.CreateSafeDefault()` |
| S02 | Bot State | `/bot-state` | `Components/Pages/BotState.razor` | Bot State (봇 상태) | 준비 중 | 홈 봇 상태 카드 확장 |
| S03 | Live Lock + Kill Switch | `/live-lock` | `Components/Pages/LiveLock.razor` | Live Lock (돈 보호) | 준비 중 | 잠금·긴급 정지 설명만 |
| S04 | Risk Gate | `/risk` | `Components/Pages/Risk.razor` | Risk (리스크 게이트) | 준비 중 | `RiskGateRowViewModel` 목록 예정 |
| S05 | Order Candidate | `/candidates` | `Components/Pages/Candidates.razor` | Candidates (주문 후보) | 준비 중 | 후보 ≠ 실주문, `IsLive` 항상 false |
| S06 | Account Snapshot | `/account` | `Components/Pages/Account.razor` | Account (계좌) | 준비 중 | Phase 2 읽기 데이터, 마스킹 |
| S07 | Audit Log | `/audit` | `Components/Pages/Audit.razor` | Audit (감사 로그) | 준비 중 | 비밀 비표시 |
| S08 | Settings / Key Guide | `/settings` | `Components/Pages/Settings.razor` | Settings (설정) | 준비 중 | 키 안내만, 값 미표시 |

---

## 레이아웃 · 네비

| 파일 | 역할 |
|------|------|
| `Components/Layout/MainLayout.razor` | 사이드바 + 본문 셸 |
| `Components/Layout/NavMenu.razor` | MVP 8 링크 스텁 |
| `Components/Layout/*.razor.css` | 기본 레이아웃 스타일 |
| `Components/Pages/Home.razor.css` | 홈 카드 배치 |

네비 그룹:

```text
홈
  └─ Overview
상태 · 보호
  ├─ Bot State
  ├─ Live Lock
  └─ Risk
후보 · 계좌
  ├─ Candidates
  └─ Account
기록 · 설정
  ├─ Audit
  └─ Settings
```

---

## 홈 화면 정보 우선순위 (구현 반영)

1. 실거래 잠금 (`Snapshot.LiveLock`)  
2. 긴급 정지 (`Snapshot.KillSwitchActive`)  
3. 봇 상태 한 문장  
4. 차단·경고 / 리스크 게이트 요약  
5. 연결·시장·계좌 요약  
6. 지금 할 일  
7. 주문 후보 (실주문 아님)  
8. 최근 감사 로그  

**의도적으로 없는 것:** 매수 실행, 실주문 제출, Live 기본 ON, 잠금 원클릭 해제.

---

## Agent 경계

| Agent | 담당 | 비고 |
|-------|------|------|
| **C (본 문서)** | Pages / Layout / ROUTES.md | 이 경로만 수정 |
| **A** | `Program.cs`, 프로젝트 스캐폴드, DI | Pages와 충돌 금지 |
| **B 등** | 공유 컴포넌트·서비스 | 홈은 서비스 주입 전 `CreateSafeDefault()` |

Web 프로젝트가 아직 솔루션에 없을 수 있습니다. 페이지 파일은  
`src/TradingBot.Web/Components/**` 에 두었고, 호스트(`Program.cs`, `.csproj`)는 Agent A가 연결합니다.

---

## 빌드

- 페이지·레이아웃만으로는 단독 빌드 불가 (호스트 프로젝트 필요).  
- `TradingBot.Ui` 모델은 기존 솔루션에서 테스트 가능:  
  `dotnet test tests/TradingBot.Ui.Tests`  
- Web 호스트 추가 후: `dotnet build src/TradingBot.Web`

---

## 금지 라우트 (의도적 미구현)

| 경로 예 | 이유 |
|---------|------|
| `/orders/live`, `/buy`, `/sell` | 실주문 UI 금지 |
| `/unlock-live` 원클릭 | 다단계·준비 게이트 전 불가 |

---

## 변경 이력

| 날짜 | 내용 |
|------|------|
| 2026-07-09 | Agent C: MVP 8 스텁 + Overview 골격 + 본 문서 초안 |
