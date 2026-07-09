# Context7 API Key Fallback

## Aliases (values never logged)

```text
CONTEXT7_API_KEY_1
→ CONTEXT7_API_KEY_2
→ CONTEXT7_API_KEY_3
→ CONTEXT7_API_KEY_4
→ CONTEXT7_API_KEY_5
→ CONTEXT7_API_KEY_6
```

## Policy

1. Start at `_1`.
2. On 401/403/429/quota exhausted → next alias.
3. Log **alias only** (e.g. `Context7 request used CONTEXT7_API_KEY_3`).
4. Never print key material.
5. All fail → report failure block; use official URLs manually.

## Wrapper

```bash
bash scripts/grok/context7-key-rotation.sh "query"
pwsh scripts/grok/context7-key-rotation.ps1 "query"
```

Prefer Grok MCP Context7 when connected. This session used Context7 MCP successfully for `/dotnet/docs`.
