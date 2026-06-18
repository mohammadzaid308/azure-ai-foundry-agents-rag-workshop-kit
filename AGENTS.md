# AGENTS.md

This workshop follows harness-engineering principles (explicit boundaries, observable progress, verification before completion).

## Guardrails

1. Work only inside this repository.
2. Keep `.NET` and `python` tracks isolated (no shared runtime dependencies).
3. Do not add unrelated samples.
4. Never hardcode credentials or secrets.
5. Run each lab locally before claiming success.

## Required execution behavior

1. Define scope for each change.
2. Make the smallest complete change that satisfies the lab objective.
3. Validate each lab path (`responses`, `create-agent`, `chat-with-agent`, `filesystem-rag`).
4. If a step fails, report the exact command and failure reason.
5. Do not mark complete until all required labs compile/run.

## State and observability

- Track progress per lab in commit messages.
- Keep instructions executable and copy-paste friendly.
- Keep errors explicit; do not hide failures.

