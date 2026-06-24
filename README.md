# Azure AI Foundry Agents + RAG Workshop Kit

A focused 1-day, code-first workshop package with two independent tracks:

- `dotnet/`
- `python/`

Both tracks use the newest Azure AI Foundry SDK (Azure AI Projects 2.x with the
OpenAI Responses/Conversations API). There is no legacy/deprecated agent code,
and creating an agent is a workshop lab (Lab 2), not a prerequisite.

## Labs (mapped to the 1-day agenda)

| Agenda | Lab folder | Focus |
| --- | --- | --- |
| Lab 1 | `01-responses`, `02-streaming-responses` | First model call + token streaming |
| Lab 2 | `03-create-agent`, `04-chat-with-agent`, `05-agent-function`, `06-filesystem-rag` | Build/chat with agents, bakery storefront function tools, GiftBot file RAG |
| Lab 3 | `07-bing-grounding`, `08-azure-ai-search`, `09-openapi-tool` | Grounding with Bing, AI Search, OpenAPI |
| Lab 4 | `10-multi-agent-sequential`, `11-multi-agent-concurrent` | Multi-agent orchestration (Frankie's Bakery support pipeline) |
| Lab 5 | `12-evaluations`, `13-security-observability` | Quality gates (offline + Foundry Evaluations), RBAC checks + tracing |
| Lab 6 (Python) | `14-mcp-server`, `15-evaluations-tests`, `16-guardrails`, `17-telemetry` | Build an MCP server, evals-as-CI-tests, guardrails, OpenTelemetry tracing (all run offline) |
| Lab 7 | `14-capstone` (.NET) / `18-capstone` (Python) | Combine everything |

> The agent labs use a shared **Frankie's Bakery** / **GiftBot** scenario: a product
> catalog (`05-agent-function`), a family gift corpus (`06-filesystem-rag`), a bakery
> support pipeline (`10-multi-agent-sequential`), and a bakery answer-quality dataset
> (`12-evaluations`). All data ships in each lab's `data/` folder so tool logic,
> retrieval, and offline evaluation run with **no Azure calls**; only model/agent
> requests reach Foundry.

Each track has the same labs, isolated per language. See `dotnet/README.md` and
`python/README.md` for the per-lab table and run instructions.

## Prerequisites

- Azure subscription with quota for your chosen model
- Azure CLI (`az`)
- Terraform (`>=1.5`)
- Git
- One of:
  - .NET SDK 8+
  - Python 3.10+

### Azure AI Foundry prerequisites (required)

You need a working Foundry **account + project + model deployment** before any lab runs.

1. Create or use an **Azure AI Foundry account** (the
   `Microsoft.CognitiveServices/accounts` resource of kind AIServices).
2. Create a **Foundry project** under that account.
3. Deploy at least one model in the project (for example `gpt-4o`).
4. Collect these values before running labs:
   - `FOUNDRY_PROJECT_ENDPOINT` in the form
     `https://<resource>.services.ai.azure.com/api/projects/<project>`
   - `FOUNDRY_MODEL_DEPLOYMENT` - the model deployment name in your project
   - `FOUNDRY_AGENT_NAME` - the agent name created in the labs
5. Make sure each participant can authenticate to the same tenant/subscription:
   ```bash
   az login
   az account set --subscription "<SUBSCRIPTION_ID>"
   ```

> The infra in `dotnet/infra` and `python/infra` (Terraform) can provision the
> account, project, and model deployment for you. After `terraform apply`, read
> the outputs to fill the values above.

#### Optional connections for Lab 3 (grounding)

The grounding labs need Foundry connections created in your project, plus env vars:

- Bing grounding: `FOUNDRY_BING_CONNECTION_ID`
- Azure AI Search: `FOUNDRY_SEARCH_CONNECTION_NAME`, `FOUNDRY_SEARCH_INDEX_NAME`

## Roles (shared Foundry project)

These labs authenticate with `DefaultAzureCredential` (your `az login` identity),
so each person needs an Azure RBAC role on the **Foundry project**.

- **Foundry User** (participants / developers) — formerly **Azure AI User**
  - Use models, run responses, create/run agents, execute all labs
  - Assign at **project scope**
- **Foundry Project Manager** (facilitators / admins) — formerly **Azure AI Project Manager**
  - Everything a Foundry User can do, plus manage project settings, deployments,
    and role assignments
  - Assign at **project scope** (or **account scope** for workshop organizers)

> **Rename note:** Microsoft is rolling out new role names (Azure AI User → **Foundry
> User**, Azure AI Project Manager → **Foundry Project Manager**). The role IDs and
> permissions are unchanged, so the commands below use the stable role-definition IDs.

### Role assignment steps (facilitator)

1. Assign **Foundry User** to every participant at **project scope**
   (role-definition ID is rollout-proof; display name = "Foundry User", formerly "Azure AI User"):
   ```bash
   az role assignment create \
     --assignee "<participant-object-id-or-email>" \
     --role "53ca6127-db72-4b80-b1b0-d745d6d5456d" \
     --scope "<FOUNDRY_PROJECT_RESOURCE_ID>"
   ```
2. Assign **Foundry Project Manager** to facilitators at **project scope** (or account scope)
   (display name = "Foundry Project Manager", formerly "Azure AI Project Manager"):
   ```bash
   az role assignment create \
     --assignee "<facilitator-object-id-or-email>" \
     --role "eadc314b-1a2d-4efa-be10-5d325db5065e" \
     --scope "<FOUNDRY_PROJECT_RESOURCE_ID>"
   ```
3. Validate access before workshop day by running one quick model call from both tracks.

> `<FOUNDRY_PROJECT_RESOURCE_ID>` looks like:
> `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>`

## Workshop flow (5 coding hours)

1. Setup and environment check (30 min)
2. Lab 1 + Lab 2 (90 min)
3. Lab 3 grounding (60 min)
4. Lab 4 multi-agent (60 min)
5. Lab 5 + Lab 6 add-ons (MCP / evals-as-tests / guardrails / telemetry) + Lab 7 capstone (60 min)
6. Wrap-up and troubleshooting (30 min)

## Repository files

- `AGENTS.md` - harness-engineering execution rules
- `FILES.md` - what each folder/file is for
- `dotnet/README.md` - .NET runbook
- `python/README.md` - Python runbook
