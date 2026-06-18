"""Post-lab cleanup: delete every agent and every model deployment.

Removes all agents created by the labs and all model (LLM) deployments on the
Foundry account, while leaving the Foundry account and project intact.

Usage:
    python cleanup.py            # interactive confirmation
    python cleanup.py --yes      # skip confirmation
    python cleanup.py --agents-only
    python cleanup.py --models-only

Reads FOUNDRY_PROJECT_ENDPOINT from the environment (or python/.env). The Azure
account name is derived from the endpoint host; the resource group is resolved
at runtime via the Azure CLI, so nothing is hard-coded.
"""

import argparse
import json
import os
import subprocess
import sys
from urllib.parse import urlparse

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient


def confirm(message: str, assume_yes: bool) -> bool:
    if assume_yes:
        return True
    return input(f"{message} [y/N]: ").strip().lower() in ("y", "yes")


def delete_agents(endpoint: str, assume_yes: bool) -> None:
    project = AIProjectClient(endpoint=endpoint, credential=DefaultAzureCredential())
    agents = list(project.agents.list())
    names = sorted({getattr(a, "name", None) for a in agents if getattr(a, "name", None)})

    if not names:
        print("Agents: none found.")
        return

    print(f"Agents to delete ({len(names)}): {', '.join(names)}")
    if not confirm("Delete all of these agents (all versions)?", assume_yes):
        print("Skipped agent deletion.")
        return

    for name in names:
        try:
            project.agents.delete(name, force=True)
            print(f"  deleted agent: {name}")
        except Exception as exc:  # noqa: BLE001 - report and continue
            print(f"  FAILED to delete agent {name}: {exc}")


def _az(args: list[str]) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["az", *args], capture_output=True, text=True, check=False
    )


def resolve_account(account_name: str) -> tuple[str, str]:
    """Return (account_name, resource_group) for the Foundry/AIServices account."""
    result = _az(
        [
            "cognitiveservices", "account", "list",
            "--query", f"[?name=='{account_name}'].{{name:name,rg:resourceGroup}}",
            "-o", "json",
        ]
    )
    if result.returncode != 0:
        raise RuntimeError(f"az account lookup failed: {result.stderr.strip()}")
    matches = json.loads(result.stdout or "[]")
    if not matches:
        raise RuntimeError(
            f"Could not find Cognitive Services/AIServices account '{account_name}'."
        )
    return matches[0]["name"], matches[0]["rg"]


def delete_model_deployments(account_name: str, assume_yes: bool) -> None:
    name, rg = resolve_account(account_name)
    listed = _az(
        [
            "cognitiveservices", "account", "deployment", "list",
            "-n", name, "-g", rg,
            "--query", "[].name", "-o", "json",
        ]
    )
    if listed.returncode != 0:
        raise RuntimeError(f"az deployment list failed: {listed.stderr.strip()}")
    deployments = json.loads(listed.stdout or "[]")

    if not deployments:
        print("Model deployments: none found.")
        return

    print(f"Model deployments to delete ({len(deployments)}): {', '.join(deployments)}")
    if not confirm(
        f"Delete all model deployments on account '{name}' (rg '{rg}')?", assume_yes
    ):
        print("Skipped model deployment deletion.")
        return

    for dep in deployments:
        result = _az(
            [
                "cognitiveservices", "account", "deployment", "delete",
                "-n", name, "-g", rg, "--deployment-name", dep,
            ]
        )
        if result.returncode == 0:
            print(f"  deleted deployment: {dep}")
        else:
            print(f"  FAILED to delete deployment {dep}: {result.stderr.strip()}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Delete lab agents and model deployments.")
    parser.add_argument("--yes", action="store_true", help="Skip confirmation prompts.")
    parser.add_argument("--agents-only", action="store_true", help="Only delete agents.")
    parser.add_argument("--models-only", action="store_true", help="Only delete model deployments.")
    args = parser.parse_args()

    load_dotenv()
    endpoint = os.environ.get("FOUNDRY_PROJECT_ENDPOINT")
    if not endpoint:
        print("FOUNDRY_PROJECT_ENDPOINT is not set (check python/.env).", file=sys.stderr)
        return 2

    account_name = urlparse(endpoint).hostname.split(".")[0]
    print(f"Foundry account: {account_name}  (account and project are NOT deleted)")

    do_agents = not args.models_only
    do_models = not args.agents_only

    if do_agents:
        delete_agents(endpoint, args.yes)
    if do_models:
        delete_model_deployments(account_name, args.yes)

    print("Cleanup complete.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
