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

- `labs/01-responses/` - Lab 1: first model call (Responses API)
- `labs/02-streaming-responses/` - Lab 1: token streaming
- `labs/03-create-agent/` - Lab 2: create an agent version
- `labs/04-chat-with-agent/` - Lab 2: multi-turn chat over a conversation
- `labs/05-agent-function/` - Lab 2: bakery storefront function tools + `data/products` catalog
- `labs/06-filesystem-rag/` - Lab 2: GiftBot retrieval over local `data/gift` corpus
- `labs/07-bing-grounding/` - Lab 3: Bing grounding tool
- `labs/08-azure-ai-search/` - Lab 3: Azure AI Search grounding tool
- `labs/09-openapi-tool/` - Lab 3: OpenAPI tool
- `labs/10-multi-agent-sequential/` - Lab 4: Frankie's Bakery support pipeline + `data/bakery` instructions
- `labs/11-multi-agent-concurrent/` - Lab 4: parallel agents + aggregation
- `labs/12-evaluations/` - Lab 5: bakery answer-quality grading (offline .NET F1 gate / Python Foundry Evaluations) + `data/*.jsonl`
- `labs/13-security-observability/` - Lab 5: RBAC token check + tracing
- `labs/14-mcp-server/` - Lab 6 (Python): FastMCP bakery server + Foundry hosted MCP tool, `data/products` catalog, offline demo + tests
- `labs/15-evaluations-tests/` - Lab 6 (Python): pytest quality + adversarial gate, `data/*.jsonl`
- `labs/16-guardrails/` - Lab 6 (Python): prompt-injection / PII / banned-topic pipeline + optional Content Safety
- `labs/17-telemetry/` - Lab 6 (Python): OpenTelemetry GenAI tracing + token metrics
- `labs/15-capstone/` (.NET) / `labs/18-capstone/` (Python) - Lab 7: starter to combine everything
