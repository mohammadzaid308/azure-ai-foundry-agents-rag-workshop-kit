#!/usr/bin/env bash
set -euo pipefail

source .venv/bin/activate
set -a
source .env
set +a

pip install -r labs/responses/requirements.txt
pip install -r labs/filesystem-rag/requirements.txt

python labs/responses/quickstart-responses.py
python labs/create-agent/quickstart-create-agent.py
python labs/chat-with-agent/quickstart-chat-with-agent.py
python labs/filesystem-rag/filesystem-rag.py

