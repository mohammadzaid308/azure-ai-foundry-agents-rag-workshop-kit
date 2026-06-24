#!/usr/bin/env bash
set -euo pipefail

source .venv/bin/activate
set -a
source .env
set +a

# Core labs that run without extra Foundry connections (in execution order).
LABS="01-responses 02-streaming-responses 03-create-agent 04-chat-with-agent 05-agent-function \
      06-filesystem-rag 10-multi-agent-sequential 11-multi-agent-concurrent 12-evaluations 13-security-observability \
      14-mcp-server 15-evaluations-tests 16-guardrails 17-telemetry"

for lab in $LABS; do
  pip install -q -r "labs/$lab/requirements.txt"
done

python labs/01-responses/quickstart-responses.py
python labs/02-streaming-responses/streaming-responses.py
python labs/03-create-agent/quickstart-create-agent.py
python labs/04-chat-with-agent/quickstart-chat-with-agent.py
python labs/05-agent-function/agent-function.py
python labs/06-filesystem-rag/filesystem-rag.py
python labs/10-multi-agent-sequential/multi-agent-sequential.py
python labs/11-multi-agent-concurrent/multi-agent-concurrent.py
python labs/12-evaluations/evaluations.py --scenario dataset
python labs/13-security-observability/security-observability.py

# Lab 6 add-ons (run fully offline; no Foundry connection required).
python labs/14-mcp-server/mcp-server.py --offline
python -m pytest -q labs/14-mcp-server/test_offline.py
python -m pytest -q labs/15-evaluations-tests/test_bakery_quality.py
python labs/16-guardrails/guardrails.py
python -m pytest -q labs/16-guardrails/test_guardrails.py
python labs/17-telemetry/telemetry.py

echo "Grounding labs (07-bing-grounding, 08-azure-ai-search, 09-openapi-tool) and 18-capstone"
echo "require Foundry connections / your own scenario. Run them manually."
