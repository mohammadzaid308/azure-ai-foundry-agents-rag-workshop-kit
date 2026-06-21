#!/usr/bin/env bash
set -euo pipefail

set -a
source .env
set +a

# Core labs that run without extra Foundry connections.
for lab in responses streaming-responses create-agent chat-with-agent agent-function \
           filesystem-rag multi-agent-sequential multi-agent-concurrent evaluations security-observability; do
  echo "=== Running $lab ==="
  (cd "labs/$lab" && dotnet restore && dotnet run)
done

echo "Grounding labs (bing-grounding, azure-ai-search, openapi-tool) and capstone"
echo "require Foundry connections / your own scenario. Run them manually."
