#!/usr/bin/env bash
set -euo pipefail

source .venv/bin/activate
set -a
source .env
set +a

# Core labs that run without extra Foundry connections.
LABS="responses streaming-responses create-agent chat-with-agent agent-function \
      filesystem-rag multi-agent-sequential multi-agent-concurrent evaluations security-observability"

for lab in $LABS; do
  pip install -q -r "labs/$lab/requirements.txt"
done

python labs/responses/quickstart-responses.py
python labs/streaming-responses/streaming-responses.py
python labs/create-agent/quickstart-create-agent.py
python labs/chat-with-agent/quickstart-chat-with-agent.py
python labs/agent-function/agent-function.py
python labs/filesystem-rag/filesystem-rag.py
python labs/multi-agent-sequential/multi-agent-sequential.py
python labs/multi-agent-concurrent/multi-agent-concurrent.py
python labs/evaluations/evaluations.py --scenario dataset
python labs/security-observability/security-observability.py

echo "Grounding labs (bing-grounding, azure-ai-search, openapi-tool) and capstone"
echo "require Foundry connections / your own scenario. Run them manually."
