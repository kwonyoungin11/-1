# Design System (Cockpit)

## Design tokens

```text
color.safe
color.paper
color.warning
color.blocked
color.danger
color.liveLocked
color.manualRequired
color.killSwitch
color.background
color.surface
color.textPrimary
color.textMuted

spacing.xs / sm / md / lg / xl
radius.sm / md / lg
font.body / mono / heading
```

## Severity mapping

| State | Token | Text example |
|-------|-------|--------------|
| Safe dry-run | color.safe | 연습 모드 — 실주문 없음 |
| Paper | color.paper | 가상 체결 기록 중 |
| Warning | color.warning | 데이터 지연 주의 |
| Blocked | color.blocked | risk gate 차단 |
| Danger | color.danger | 이상 상태 |
| Live locked | color.liveLocked | 실주문 잠김 |
| Manual | color.manualRequired | 수동 승인 필요 |
| Kill | color.killSwitch | 긴급 정지 ON |

## Designer may change

- colors, spacing, radius, typography
- layout within cockpit regions
- icons (with text labels)

## Designer must not change

- meaning of live blocked
- removing kill switch
- adding one-click live order
- hiding blocked reasons
