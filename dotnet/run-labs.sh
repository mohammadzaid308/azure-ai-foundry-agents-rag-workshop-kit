#!/usr/bin/env bash
set -euo pipefail

set -a
source .env
set +a

# Core labs that run without extra Foundry connections (in execution order).
for lab in 01-responses 02-streaming-responses 03-create-agent 04-chat-with-agent 05-agent-function \
           06-filesystem-rag 10-multi-agent-sequential 11-multi-agent-concurrent 12-evaluations 13-security-observability; do
  echo "=== Running $lab ==="
  (cd "labs/$lab" && dotnet restore && dotnet run)
done

echo "Grounding labs (07-bing-grounding, 08-azure-ai-search, 09-openapi-tool) and 14-capstone"
echo "require Foundry connections / your own scenario. Run them manually."
