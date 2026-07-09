# Component Spec (Cockpit)

## KillSwitchBanner

- Always visible in shell
- States: active (default) / inactive (requires strong confirmation to show)
- Action: activate is one click; deactivate is multi-step (future)

## LiveLockBadge

- Shows locked/unlocked
- Unlocked never default

## RiskGatePanel

- List of checks with pass/block + human reason

## OrderCandidateCard

- Symbol, side, qty, limits
- Explicit label: "후보 — 실주문 아님"
- No primary button labeled buy/sell live

## AccountSnapshotCard

- Masked account
- Holdings summary
- Refresh time + staleness indicator

## AuditLogList

- Human sentences, redacted secrets
