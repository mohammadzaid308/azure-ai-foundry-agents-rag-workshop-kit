# Azure AI Foundry Agents + RAG Workshop Kit

This repository is a focused 1-day, code-first workshop package with two independent tracks:

- `dotnet/`
- `python/`

It includes only agenda-relevant labs:

1. Responses quickstart
2. Create agent
3. Chat with agent
4. Filesystem RAG (local files + Foundry model)

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

1. Create or use an **Azure AI Foundry account** (the `Microsoft.CognitiveServices/accounts` resource of kind AIServices).
2. Create a **Foundry project** under that account.
3. Deploy at least one model in the project (for example `gpt-4.1-mini`).
4. Collect these values before running labs:
   - `FOUNDRY_PROJECT_ENDPOINT` in the form
     `https://<resource>.services.ai.azure.com/api/projects/<project>`
   - `FOUNDRY_MODEL_DEPLOYMENT` - the model deployment name in your project
   - `FOUNDRY_AGENT_NAME` - the agent name used/created in the labs
5. Make sure each participant can authenticate to the same tenant/subscription:
   ```bash
   az login
   az account set --subscription "<SUBSCRIPTION_ID>"
   ```

> The infra in `dotnet/infra` and `python/infra` (Terraform) can provision the account, project,
> and model deployment for you. After `terraform apply`, read the outputs to fill the values above.

## Roles (shared Foundry project)

These labs authenticate with `DefaultAzureCredential` (your `az login` identity), so each person
needs an Azure RBAC role on the **Foundry project**.

- **Azure AI  participant developersUser** 
  - Use models, run responses, create/run agents, execute all labs
  - Assign at **project scope**
- **Azure AI Project  facilitators/adminsManager** 
  - Everything an Azure AI User can do, plus manage project settings, deployments, and role assignments
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
3. Lab 3 (60 min)
4. Lab 4 Filesystem RAG (90 min)
5. Wrap-up and troubleshooting (30 min)

## Repository files

- `AGENTS. harness engineering execution rulesmd` 
- `FILES. what each folder/file is formd` 
- `dotnet/README. .NET runbookmd` 
- `python/README. Python runbookmd` 
