# UI State Model

## Top-level BotState (examples)

| State | Live orders | Meaning |
|-------|-------------|---------|
| HarnessReady | blocked | Scaffold only |
| ReadOnlyConnected | blocked | API read OK |
| SignalOnly | blocked | Strategy computes, no route |
| DryRunActive | blocked | Candidates simulated |
| PaperActive | blocked | Virtual fills |
| LiveLocked | blocked | Explicit lock |
| LiveReadyPendingApproval | blocked | Waiting owner |
| LiveArmed | still gated | All flags + approval (future) |
| Error | blocked | Fail-closed |
| KillSwitchActive | blocked | Emergency |

## Rule

Any unknown / missing / stale / API error → UI shows **blocked** and disables live actions.
