# Lab 5: Evaluations (offline quality gate)

Grades pre-computed Frankie's Bakery support answers against ground truth using
deterministic, **offline** metrics (token-overlap F1 + exact match) over
`data/bakery_eval_dataset.jsonl`. No Azure calls — ideal as a fast CI pre-flight
before paying for LLM-judge evaluations.

```bash
cd dotnet/labs/evaluations
dotnet run
```

The companion **Python** `evaluations` lab runs the full managed Azure AI Foundry
Evaluations service (coherence, F1, violence, task adherence, intent resolution)
across dataset / model / agent / traces scenarios.
