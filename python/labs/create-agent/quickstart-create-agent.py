import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
AGENT_NAME = os.environ["FOUNDRY_AGENT_NAME"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")

# Create project client to call Foundry API
project = AIProjectClient(
    endpoint=PROJECT_ENDPOINT,
    credential=DefaultAzureCredential(),
)

# Create an agent with a model and instructions
agent = project.agents.create_version(
    agent_name=AGENT_NAME,
    definition=PromptAgentDefinition(
    model=MODEL_DEPLOYMENT,
        instructions="You are a helpful assistant that answers general questions",
    ),
)
print(f"Agent created (id: {agent.id}, name: {agent.name}, version: {agent.version})")

# ──────────────────────────────────────────────────────────────────────────
# 👁  PORTAL OBSERVATION
#   Foundry portal → Agents.  After running, find the agent by the name
#   in FOUNDRY_AGENT_NAME.  Click it and note:
#     • Version number (starts at 1; re-running increments it).
#     • Model deployment name linked to it.
#     • System prompt / instructions as stored server-side.
#   Try editing the instructions in the portal, save, then re-list
#   versions — you'll see a new version entry without running code.
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# 🏋  CHALLENGE
#
#   A PromptAgentDefinition can also accept a `temperature` parameter
#   and a `top_p` parameter to control creativity.
#
#   Modify the definition to:
#     a) Set temperature=0.2 (more deterministic).
#     b) Add a second create_version call with temperature=1.0
#        and a *different* agent name (e.g. "creative-agent").
#     c) Print both agents' ids and version numbers.
#
#   HINT:  PromptAgentDefinition(model=..., instructions=..., temperature=0.2)
#   Then go to the portal and compare the two agents in the Agents list.
# ──────────────────────────────────────────────────────────────────────────
