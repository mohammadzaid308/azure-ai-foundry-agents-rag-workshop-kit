# FILES.md

## Root

- `README.md` - workshop overview, prerequisites, and Foundry/RBAC requirements
- `AGENTS.md` - harness behavior contract
- `FILES.md` - file map
- `.gitignore` - excludes for local/dev artifacts

## Per track (`dotnet/` and `python/`)

- `README.md` - setup, agenda-mapped lab table, and run instructions
- `.env-template` - required and optional environment variables
- `run-labs.sh` - runs the core runnable labs end to end
- `infra/` - Terraform for Foundry account + project setup

## Labs (same set in both tracks)

- `labs/responses/` - Lab 1: first model call (Responses API)
- `labs/streaming-responses/` - Lab 1: token streaming
- `labs/create-agent/` - Lab 2: create an agent version
- `labs/chat-with-agent/` - Lab 2: multi-turn chat over a conversation
- `labs/agent-function/` - Lab 2: bakery storefront function tools + `data/products` catalog
- `labs/filesystem-rag/` - Lab 2: GiftBot retrieval over local `data/gift` corpus
- `labs/bing-grounding/` - Lab 3: Bing grounding tool
- `labs/azure-ai-search/` - Lab 3: Azure AI Search grounding tool
- `labs/openapi-tool/` - Lab 3: OpenAPI tool
- `labs/multi-agent-sequential/` - Lab 4: Frankie's Bakery support pipeline + `data/bakery` instructions
- `labs/multi-agent-concurrent/` - Lab 4: parallel agents + aggregation
- `labs/evaluations/` - Lab 5: bakery answer-quality grading (offline .NET F1 gate / Python Foundry Evaluations) + `data/*.jsonl`
- `labs/security-observability/` - Lab 5: RBAC token check + tracing
- `labs/mcp-server/` - Lab 6 (Python): FastMCP bakery server + Foundry hosted MCP tool, `data/products` catalog, offline demo + tests
- `labs/evaluations-tests/` - Lab 6 (Python): pytest quality + adversarial gate, `data/*.jsonl`
- `labs/guardrails/` - Lab 6 (Python): prompt-injection / PII / banned-topic pipeline + optional Content Safety
- `labs/telemetry/` - Lab 6 (Python): OpenTelemetry GenAI tracing + token metrics
- `labs/capstone/` - Lab 7: starter to combine everything
