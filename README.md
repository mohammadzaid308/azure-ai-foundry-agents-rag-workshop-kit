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
| Lab 1 | `responses`, `streaming-responses` | First model call + token streaming |
| Lab 2 | `create-agent`, `chat-with-agent`, `agent-function`, `filesystem-rag` | Build/chat with agents, bakery storefront function tools, GiftBot file RAG |
| Lab 3 | `bing-grounding`, `azure-ai-search`, `openapi-tool` | Grounding with Bing, AI Search, OpenAPI |
| Lab 4 | `multi-agent-sequential`, `multi-agent-concurrent` | Multi-agent orchestration (Frankie's Bakery support pipeline) |
| Lab 5 | `evaluations`, `security-observability` | Quality gates (offline + Foundry Evaluations), RBAC checks + tracing |
| Lab 6 | `capstone` | Combine everything |

> The agent labs use a shared **Frankie's Bakery** / **GiftBot** scenario: a product
> catalog (`agent-function`), a family gift corpus (`filesystem-rag`), a bakery
> support pipeline (`multi-agent-sequential`), and a bakery answer-quality dataset
> (`evaluations`). All data ships in each lab's `data/` folder so tool logic,
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

- **Azure AI User** (participants / developers)
  - Use models, run responses, create/run agents, execute all labs
  - Assign at **project scope**
- **Azure AI Project Manager** (facilitators / admins)
  - Everything an Azure AI User can do, plus manage project settings, deployments,
    and role assignments
  - Assign at **project scope** (or **account scope** for workshop organizers)

### Role assignment steps (facilitator)

1. Assign **Azure AI User** to every participant at **project scope**:
   ```bash
   az role assignment create \
     --assignee "<participant-object-id-or-email>" \
     --role "Azure AI User" \
     --scope "<FOUNDRY_PROJECT_RESOURCE_ID>"
   ```
2. Assign **Azure AI Project Manager** to facilitators at **project scope** (or account scope):
   ```bash
   az role assignment create \
     --assignee "<facilitator-object-id-or-email>" \
     --role "Azure AI Project Manager" \
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
5. Lab 5 + Lab 6 capstone (60 min)
6. Wrap-up and troubleshooting (30 min)

## Repository files

- `AGENTS.md` - harness-engineering execution rules
- `FILES.md` - what each folder/file is for
- `dotnet/README.md` - .NET runbook
- `python/README.md` - Python runbook
