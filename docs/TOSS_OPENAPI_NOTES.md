# Toss Open API — Source of Truth Notes

**조사일:** 2026-07-09  
**공식 버전 (snapshot):** OpenAPI 3.1.0 / 토스증권 Open API **1.2.2**

## Source of truth (우선순위)

1. https://developers.tossinvest.com/llms.txt  
2. https://openapi.tossinvest.com/openapi-docs/overview.md  
3. https://openapi.tossinvest.com/openapi-docs/latest/openapi.json  
4. Local pin: `artifacts/openapi/toss-openapi.snapshot.json`  
5. Summary: `docs/specs/toss-openapi-summary.json`, `docs/specs/toss-endpoints.tsv`

비공식 블로그/GitHub 예제는 참고만. 동작 확정에 쓰지 않음.

## 확인된 사실

| 항목 | 내용 |
|------|------|
| Base server | `https://openapi.tossinvest.com` |
| 연동 | **REST only** (overview 명시) |
| WebSocket | 공식 OpenAPI snapshot 기준 **없음** (`has_websocket: false`) |
| Auth | OAuth 2.0 Client Credentials → `POST /oauth2/token` |
| API 호출 | `Authorization: Bearer {access_token}` |
| 계좌/자산/주문 | 추가 헤더 `X-Tossinvest-Account: {accountSeq}` |
| Path 수 | 27 |

## 카테고리

- Auth
- Market data / stock / market info / rankings / indicators
- Account / Asset
- Order / Order history / Order info
- Conditional order

## 초기 단계 정책

- 주문 create/modify/cancel **호출 금지**
- read-only + dry-run/paper 우선
- WebSocket 구현 금지 (공식 스펙에 없음)
- 내부 UI 갱신용 SignalR은 Toss WebSocket과 무관 (향후 UI 선택 시)

## Rate limit (overview 요약)

그룹별 TPS 제한. 헤더: `X-RateLimit-*`, 429 시 `Retry-After`.  
ORDER 그룹은 피크 시간 더 낮을 수 있음.

## 에러 모델

```json
{ "error": { "requestId", "code", "message", "data" } }
```

## 스펙 갱신

```bash
bash scripts/grok/fetch-toss-openapi-spec.sh
bash scripts/grok/check-toss-openapi-diff.sh
```

diff 발생 시 클라이언트 코드 변경 전 오너에게 보고.
