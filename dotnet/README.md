# .NET Track

This track is self-contained and uses Foundry SDK C# samples aligned to the workshop agenda.

## Prerequisites

- .NET SDK 8+
- Azure CLI
- Terraform 1.5+

## Setup

1. Login and select subscription:
   ```bash
   az login
   az account set --subscription "<SUBSCRIPTION_ID>"
   export ARM_SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
   ```
2. Provision infrastructure:
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
4. Fill values in `.env`:
   - `FOUNDRY_PROJECT_ENDPOINT`
   - `FOUNDRY_AGENT_NAME`
   - `FOUNDRY_MODEL_DEPLOYMENT`

## Run labs

```bash
set -a && source .env && set +a
cd labs/responses && dotnet restore && dotnet run
cd ../create-agent && dotnet restore && dotnet run
cd ../chat-with-agent && dotnet restore && dotnet run
cd ../filesystem-rag && dotnet restore && dotnet run
```

## Notes

- Keep using the same Foundry project for all labs.
- For shared workshop environments, participants need **Azure AI User** role.
- Facilitators should have **Azure AI Project Manager** role.

