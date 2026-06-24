# Lab: Evaluations as tests (CI quality gate)

The `evaluations` lab submits graded runs to Foundry. This lab turns evaluation
into **pytest assertions** so answer quality and safety regressions fail your CI
build.

## Files
| File | Purpose |
|------|---------|
| `evaluators.py` | Offline, deterministic metrics: token F1, keyword coverage, groundedness, refusal/leak detection. |
| `test_bakery_quality.py` | Quality gate over `data/quality_cases.jsonl` + adversarial gate over `data/adversarial_cases.jsonl`. |

## Run
```bash
pip install -r requirements.txt
pytest -v        # 10 tests, fully offline
```

Quality cases must clear F1 / keyword-coverage / groundedness thresholds; the
aggregate mean F1 must stay above a release floor. Adversarial cases must be
refused **without** leaking the system prompt.

## Going live
Swap the offline heuristics for Foundry's AI-assisted evaluators by calling
`client.evals.create(...)` (see the `evaluations` lab) inside a `--live` variant,
then assert on the returned scores. Wire `pytest` into your PR pipeline.
