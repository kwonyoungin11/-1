# TradingBot

C# / .NET **토스증권 Open API** 기반 나스닥 자동매매 시스템 (Grok 4.5 / Grok Build).

> **실주문은 기본 차단입니다.** 목표는 안전한 연결 → 읽기 → 신호 → risk gate → dry-run/paper → (먼 나중에) live.

## 오너가 먼저 읽을 문서

1. [docs/OWNER_PLAYBOOK.md](docs/OWNER_PLAYBOOK.md)
2. [docs/MACOS_SETUP.md](docs/MACOS_SETUP.md)
3. [docs/LIVE_READINESS_CHECKLIST.md](docs/LIVE_READINESS_CHECKLIST.md)
4. [docs/UX_UI_GUIDE.md](docs/UX_UI_GUIDE.md)
5. [docs/TOSS_OPENAPI_NOTES.md](docs/TOSS_OPENAPI_NOTES.md)

## 빠른 검증

```bash
bash scripts/grok/verify.sh
```

.NET SDK 설치 후:

```bash
dotnet restore && dotnet build && dotnet test
```

## 안전 기본값

```text
ALLOW_LIVE_ORDERS=false
KILL_SWITCH=true
ORDER_MODE=dry_run
```

## 비밀키

1. `.env.example` 을 참고해 로컬 `.env` 작성 (git 제외)
2. 키를 채팅에 붙여넣지 말 것
