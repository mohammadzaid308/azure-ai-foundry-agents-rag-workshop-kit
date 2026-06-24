# Lab 5: Evaluations (Azure AI Foundry Evaluations SDK)

Runs the managed Foundry Evaluations service over Frankie's Bakery support data
with four scenarios:

| Scenario | What it grades |
| --- | --- |
| `dataset` | Pre-computed answers in `data/bakery_eval_dataset.jsonl` (coherence, F1, violence) |
| `model`   | Live model answers to `data/bakery_queries_only.jsonl` (coherence, violence) |
| `agent`   | Live answers from a deployed Foundry agent (task adherence, coherence) |
| `traces`  | Agent traces in Application Insights (intent resolution, violence) |

```bash
cd python/labs/evaluations
pip install -r requirements.txt
python evaluations.py --scenario dataset
```

Env: `FOUNDRY_PROJECT_ENDPOINT`, `FOUNDRY_MODEL_DEPLOYMENT` (judge + target),
`FOUNDRY_AGENT_NAME` (agent scenario), `FOUNDRY_AGENT_ID` (traces scenario).
The `dataset` scenario is the simplest first run.

> The companion **.NET** `evaluations` lab provides a deterministic offline F1
> gate that needs no Azure resources.
