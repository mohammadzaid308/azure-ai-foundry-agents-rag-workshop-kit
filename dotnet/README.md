# .NET Track

This track is self-contained and uses the newest Azure AI Foundry SDK
(Azure AI Projects 2.x with the OpenAI Responses/Conversations API).
There is no legacy `Azure.AI.Agents.Persistent` code. Creating an agent is a
lab (Lab 2), not a prerequisite.

## Prerequisites

- .NET SDK 8+
- Azure CLI
- Terraform 1.5+
- An Azure AI Foundry project (or provision one with the Terraform in `infra/`)

## Setup

1. Login and select subscription:
   ```bash
   az login
   az account set --subscription "<SUBSCRIPTION_ID>"
   export ARM_SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
   ```
2. Provision infrastructure (optional if you already have a project):
   ```bash
   cd infra
   cp example.tfvars terraform.tfvars
   terraform init -upgrade
   terraform apply
   ```
3. Create `.env` from template:
   ```bash
   cd ..
   cp .env-template .env
   ```
4. Fill values in `.env` (see `.env-template` for the full list).

## Labs (mapped to the 1-day agenda)

| Agenda | Lab folder | What you build |
| --- | --- | --- |
| Lab 1: SDK first steps | `01-responses` | First model call with the Responses API |
| Lab 1: Streaming | `02-streaming-responses` | Token streaming with `CreateResponseStreamingAsync` |
| Lab 2: Build an agent | `03-create-agent` | Create an agent version with `AgentAdministrationClient` |
| Lab 2: Chat with an agent | `04-chat-with-agent` | Multi-turn chat over a conversation |
| Lab 2: Function calling | `05-agent-function` | Bakery storefront tools (list/get/place order) over local product catalog |
| Lab 2: File RAG | `06-filesystem-rag` | GiftBot retrieval over local family gift corpus (`data/gift`) |
| Lab 3: Bing grounding | `07-bing-grounding` | Ground answers in live web results |
| Lab 3: Azure AI Search | `08-azure-ai-search` | Ground answers in a search index |
| Lab 3: OpenAPI tool | `09-openapi-tool` | Call an external API via OpenAPI |
| Lab 4: Sequential | `10-multi-agent-sequential` | Frankie's Bakery support pipeline (intake to specialist to synthesizer) |
| Lab 4: Concurrent | `11-multi-agent-concurrent` | Parallel agents plus aggregation |
| Lab 5: Evaluations | `12-evaluations` | Bakery answer-quality grading (offline token-F1 gate, no Azure) |
| Lab 5: Security and observability | `13-security-observability` | RBAC token check plus tracing |
| Lab 6: Capstone | `14-capstone` | Starter to combine everything |

> Grounding labs (Lab 3) need Foundry connections. Create the Bing / Azure AI
> Search connections in your project and set the matching env vars before running.

## Run labs

Each lab is a standalone console app:

```bash
set -a && source .env && set +a
cd labs/responses && dotnet restore && dotnet run
```

Or run the core runnable labs end to end:

```bash
./run-labs.sh
```

## Notes

- Keep using the same Foundry project for all labs.
- Participants need the **Foundry User** role on the project (formerly **Azure AI User**).
- Facilitators who manage project settings need **Foundry Project Manager** (formerly **Azure AI Project Manager**).
- The role IDs/permissions are unchanged; you may still see the old names during rollout.
