# TradingBot.Observability

Structured, audit-friendly logging for fail-closed trading safety.

## Purpose

- Record **what happened** (risk blocks, dry-run routes, safety gate outcomes, market/session state).
- Support cockpit “recent audit” lines and later replay / incident review.
- **Never** store secrets, tokens, account numbers, or `.env` values.

## Types

| Type | Role |
|------|------|
| `AuditSeverity` | Debug → Critical |
| `AuditEntry` | Immutable record: `Timestamp`, `Category`, `Message`, `CorrelationId`, `Severity` |
| `IAuditLog` | Append + read API |
| `InMemoryAuditLog` | Thread-safe in-memory store (tests / dry-run / early wiring) |
| `AuditMessageRedactor` | Wraps `TradingBot.Domain.SecretRedactor` |

## Rules

1. **No secret fields** on `AuditEntry`. Do not add token/account properties.
2. Free-text `Message` is **redacted on append** (`Bearer ` → `Bearer [REDACTED] `).
3. Prefer structured categories: `safety`, `risk`, `order`, `market`, `oauth`, `system`.
4. Use non-secret correlation ids (client order id, request id) — never raw tokens.
5. Live order execution still requires full live-readiness; this module only records events.

## Example

```csharp
IAuditLog log = new InMemoryAuditLog();
log.Append(
    category: "safety",
    message: "Live path blocked: kill switch active",
    correlationId: "candidate-abc",
    severity: AuditSeverity.Warning);

var recent = log.GetRecent(20);
```

## Out of scope (this phase)

- Persistent file/DB sinks
- OpenTelemetry exporters
- Toss HTTP order traffic (forbidden)
- Live order enablement

## Dependencies

- `TradingBot.Domain` only (for `SecretRedactor`).
- No extra NuGet packages required for audit log core.
