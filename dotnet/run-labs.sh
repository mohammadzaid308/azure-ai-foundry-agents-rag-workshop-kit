#!/usr/bin/env bash
set -euo pipefail

set -a
source .env
set +a

for lab in responses create-agent chat-with-agent filesystem-rag; do
  echo "Running $lab..."
  (cd "labs/$lab" && dotnet restore && dotnet run)
done

