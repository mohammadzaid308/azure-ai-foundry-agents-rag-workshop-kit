# Python Track

This track is self-contained and uses Foundry SDK Python samples aligned to the workshop agenda.

## Prerequisites

- Python 3.10+
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
3. Create and activate virtual environment:
   ```bash
   cd ..
   python3 -m venv .venv
   source .venv/bin/activate
   ```
4. Create `.env` from template:
   ```bash
   cp .env-template .env
   ```
5. Fill values in `.env`:
   - `FOUNDRY_PROJECT_ENDPOINT`
   - `FOUNDRY_AGENT_NAME`
   - `FOUNDRY_MODEL_DEPLOYMENT`

## Run labs

```bash
source .venv/bin/activate
set -a && source .env && set +a
pip install -r labs/responses/requirements.txt
python labs/responses/quickstart-responses.py
python labs/create-agent/quickstart-create-agent.py
python labs/chat-with-agent/quickstart-chat-with-agent.py
python labs/filesystem-rag/filesystem-rag.py
```

## Notes

- Keep using the same Foundry project for all labs.
- For shared workshop environments, participants need **Azure AI User** role.
- Facilitators should have **Azure AI Project Manager** role.

