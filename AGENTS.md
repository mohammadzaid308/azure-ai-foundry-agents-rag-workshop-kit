# AGENTS.md

This workshop follows harness-engineering principles (explicit boundaries,
observable progress, verification before completion).

## Guardrails

1. Work only inside this repository.
2. Keep `.NET` and `python` tracks isolated (no shared runtime dependencies).
3. Use only the newest Azure AI Foundry SDK (Azure AI Projects 2.x + the OpenAI
   Responses/Conversations API). Do not reintroduce legacy agent APIs.
4. Do not add unrelated samples.
5. Never hardcode credentials or secrets.
6. Compile/validate each lab before claiming success.

## Required execution behavior

1. Define scope for each change.
2. Make the smallest complete change that satisfies the lab objective.
3. Validate every lab path: build all `dotnet/labs/*`, compile all `python/labs/*`.
4. If a step fails, report the exact command and failure reason.
5. Do not mark complete until all labs compile/run.

## Agenda coverage

The labs map to the 1-day agenda Lab 1 through Lab 6: SDK first steps and
streaming, building and chatting with agents (including function calling and
file RAG), grounding (Bing / Azure AI Search / OpenAPI), multi-agent
(sequential and concurrent), security and observability, and a capstone.

## State and observability

- Track progress per lab in commit messages.
- Keep instructions executable and copy-paste friendly.
- Keep errors explicit; do not hide failures.
