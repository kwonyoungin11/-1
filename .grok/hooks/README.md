# Project hooks

Scripts:

- `pre-tool-safety.sh` — blocks env dumps, destructive git, pipe-to-shell, live bypass attempts
- `post-tool-reminders.sh` — verification reminders by path

If Grok requires trusting project hooks, run the product’s `/hooks-trust` (or equivalent) once.

Hooks do not replace `scripts/grok/check-*.sh`.
