# Lab: Guardrails for the bakery agent

A layered guardrail pipeline that runs **before** and **after** the model:

- **Input guard** - prompt-injection / jailbreak detection, banned topics, PII redaction.
- **Output guard** - PII leak detection + redaction.
- **Optional Azure AI Content Safety** - Prompt Shields + text moderation when `CONTENT_SAFETY_ENDPOINT` is set.

## Files
| File | Purpose |
|------|---------|
| `guardrails.py` | `guard_input`, `guard_output`, and `handle()` pipeline. Console demo in `__main__`. |
| `test_guardrails.py` | pytest proving malicious inputs are blocked and PII is redacted. |

## Run offline (no Azure)
```bash
pip install -r requirements.txt
python guardrails.py     # see ALLOWED / BLOCKED decisions
pytest -q                # 5 tests
```

## Enable Azure Content Safety
```bash
export CONTENT_SAFETY_ENDPOINT=https://<your-cs-resource>.cognitiveservices.azure.com/
python guardrails.py
```
The same pipeline now also calls Content Safety; categories at severity >= 2 are flagged.

## Key idea
Guardrails are **defense in depth**: deterministic local rules catch the obvious
attacks instantly and cheaply; Content Safety adds ML-based detection for the rest.
