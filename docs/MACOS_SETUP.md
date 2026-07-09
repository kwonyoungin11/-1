# MacBook Pro 개발 환경 설치 가이드

**대상:** 비개발자 오너 + Grok 에이전트  
**날짜 기준:** 2026-07-09  
**플랫폼:** Apple Silicon (arm64)

## 지금 확인된 상태 (이 Mac)

| 항목 | 상태 |
|------|------|
| CPU | arm64 |
| macOS | 26.5.1 |
| Homebrew | 설치됨 |
| git | 설치됨 (2.50.1) |
| node/npm | 설치됨 |
| ripgrep (rg) | 설치됨 |
| jq | 설치됨 |
| grok | 설치됨 (0.2.93) |
| **dotnet SDK** | **없음 — 설치 필요** |
| **PowerShell (pwsh)** | **없음 — 선택** |
| gitleaks / trivy / osv-scanner | 미확인 (권장 설치) |

## 왜 필요한가?

- **dotnet SDK**: C# 프로그램을 빌드·테스트하는 엔진. 없으면 코드를 실행/검증할 수 없음.
- **git**: 변경 이력 저장.
- **보안 스캐너**: 비밀키가 실수로 올라가는 것을 탐지.

## 설치 명령 (오너 승인 후 직접 실행)

에이전트는 자동으로 `brew install` 하지 않습니다. 아래를 터미널에 붙여 넣기 전에 이해한 뒤 실행하세요.

```bash
# 1) .NET SDK (필수)
brew install --cask dotnet-sdk

# 2) 권장 보안/도구
brew install gitleaks trivy osv-scanner powershell

# 3) 확인
dotnet --info
dotnet --list-sdks
```

설치 후 프로젝트 폴더에서:

```bash
cd "/Users/kwon/Documents/c#"
dotnet restore
dotnet build
dotnet test
bash scripts/grok/verify.sh
```

## 사용자 승인 없이 금지

- `curl ... | sh` / `curl ... | bash`
- 무단 `brew install`
- `dotnet workload install` (필요할 때 별도 승인)

## 문제 해결

- `dotnet: command not found` → SDK 설치 후 터미널 재시작
- Apple Silicon에서 x64 SDK만 잡히면 → arm64 SDK 재설치


## 설치 결과 (2026-07-09)

| 항목 | 결과 |
|------|------|
| dotnet SDK | **10.0.301** (`brew install dotnet` via powershell dependency) |
| `brew install --cask dotnet-sdk` | sudo 암호 필요 → 이 환경에서 실패. formula `dotnet`으로 대체 |
| PowerShell | 7.6.3 |
| gitleaks | 8.30.1 |
| trivy | 0.72.0 |
| osv-scanner | 2.4.0 |

검증:

```bash
dotnet --list-sdks
dotnet restore && dotnet build && dotnet test
bash scripts/grok/verify.sh
```
