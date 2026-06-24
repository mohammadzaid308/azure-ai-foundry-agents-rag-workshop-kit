"""Lab: Sequential multi-agent (Frankie's Bakery support pipeline).

Three agents hand off in order over one shared conversation:

    bakery-intake  ->  bakery-specialist  ->  bakery-synthesizer
    (classify+route)   (answer w/ knowledge)   (warm customer reply)

Specialist knowledge is loaded from the local instruction files in
./data/bakery so the bakery's real menu/orders/hours/complaints rules drive
the agent. Agent creation + runs reach Azure AI Foundry.
"""
import os
from pathlib import Path

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition, WorkflowAgentDefinition

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")
WORKFLOW_NAME = os.environ.get("FOUNDRY_WORKFLOW_NAME", "bakery-support-workflow")

BAKERY_DIR = Path(__file__).parent / "data" / "bakery"


def instructions_of(filename: str) -> str:
    """Pull the free-text Instructions block (between the first --- pair)."""
    text = (BAKERY_DIR / filename).read_text(encoding="utf-8")
    parts = text.split("---")
    return parts[1].strip() if len(parts) > 1 else text.strip()


# Build a single specialist that carries every department's knowledge.
specialist_knowledge = "\n\n".join(
    f"## {dept}\n{instructions_of(fname)}"
    for dept, fname in [
        ("Menu", "MenuAgent.md"),
        ("Orders", "OrdersAgent.md"),
        ("Complaints", "ComplaintsAgent.md"),
        ("Hours", "HoursAgent.md"),
    ]
)

project = AIProjectClient(
    endpoint=PROJECT_ENDPOINT,
    credential=DefaultAzureCredential(),
    allow_preview=True,  # Workflow agents are a preview feature (opt-in required).
)
openai = project.get_openai_client()

agent_specs = [
    (
        "bakery-intake",
        "You are Frankie's Bakery support intake. Classify the customer's message and "
        "restate it cleanly. Reply with exactly two lines:\n"
        "Route: <menu | orders | complaints | hours | else>\n"
        "Summary: <one-sentence restatement of what the customer needs>",
    ),
    (
        "bakery-specialist",
        "You are a Frankie's Bakery support specialist. Use the department knowledge "
        "below to answer the customer's request accurately. If allergens or dietary "
        "restrictions are involved, call them out explicitly.\n\n" + specialist_knowledge,
    ),
    (
        "bakery-synthesizer",
        "You are a senior Frankie's Bakery agent. Rewrite the specialist's answer into a "
        "warm, concise, on-brand reply addressed directly to the customer. Return only the reply.",
    ),
]

for name, instructions in agent_specs:
    agent = project.agents.create_version(
        agent_name=name,
        definition=PromptAgentDefinition(model=MODEL_DEPLOYMENT, instructions=instructions),
    )
    print(f"Created agent '{agent.name}' (version {agent.version})")

# Deploy the pipeline as a workflow agent so the workflow itself shows up in the
# Foundry portal. The CSDL definition lives in workflow.yaml next to this script.
workflow_yaml = (Path(__file__).parent / "workflow.yaml").read_text(encoding="utf-8")
workflow = project.agents.create_version(
    agent_name=WORKFLOW_NAME,
    definition=WorkflowAgentDefinition(workflow=workflow_yaml),
)
print(f"Created workflow agent '{workflow.name}' (version {workflow.version})")

ticket = (
    "Hi! My daughter is allergic to tree nuts. Is the almond croissant safe for her, "
    "and what gluten-free options do you have?"
)
print("\n--- Customer ticket ---\n" + ticket)

conversation = openai.conversations.create()


def run(agent_name: str, text: str) -> str:
    return openai.responses.create(
        conversation=conversation.id,
        extra_body={"agent_reference": {"name": agent_name, "type": "agent_reference"}},
        input=text,
    ).output_text


try:
    response = openai.responses.create(
        conversation=conversation.id,
        extra_body={"agent_reference": {"name": WORKFLOW_NAME, "type": "agent_reference"}},
        input=ticket,
    )
    print("\n--- Customer reply (single trigger) ---\n" + response.output_text)
except Exception as exc:  # noqa: BLE001 - surface the preview-runtime failure explicitly
    # The Foundry "workflow agent" runtime is in preview and can 500 when a
    # workflow chains multiple steps. The workflow agent is still created and
    # visible in the portal; fall back to a client-driven sequential run.
    print("\n[preview] Single-trigger workflow run failed on the service:")
    print("  " + str(exc).splitlines()[0][:200])
    print("[preview] Falling back to a client-driven sequential run of the same agents.\n")

    intake = run("bakery-intake", ticket)
    print("--- Intake (bakery-intake) ---\n" + intake + "\n")
    answer = run("bakery-specialist", f"Customer message:\n{ticket}\n\nIntake:\n{intake}")
    print("--- Specialist (bakery-specialist) ---\n" + answer + "\n")
    reply = run("bakery-synthesizer", answer)
    print("--- Customer reply (bakery-synthesizer) ---\n" + reply)

print(
    f"\nDone. Open '{WORKFLOW_NAME}' in the Foundry portal (Agents / Workflows) "
    "to see the intake -> specialist -> synthesizer workflow."
)


# ──────────────────────────────────────────────────────────────────────────
# 👁  PORTAL OBSERVATIONS (3 things to check)
#
#   1. Foundry portal → Agents → Workflows.
#      Open the bakery-support-workflow and click "View diagram" to see
#      the intake → specialist → synthesizer DAG rendered visually.
#
#   2. Foundry portal → Agents → Conversations.
#      Find the conversation used by this script. Click it to walk
#      through the step-by-step handoffs — each agent's reply is a
#      separate message in the thread.
#
#   3. Foundry portal → Monitoring → Traces.
#      The workflow run appears as a single trace with nested spans
#      for each agent handoff.  Note the span names and durations.
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# 🏋  CHALLENGE  — Add a fourth "quality-check" agent
#
#   After the synthesizer writes its final answer, route it through a
#   fourth agent whose instructions are:
#     "Review the following bakery support answer. If it mentions any
#      allergen (nut, gluten, dairy), prepend a ⚠️ WARNING. Otherwise
#      output the answer unchanged."
#
#   Steps:
#     1. Create a fourth PromptAgentDefinition (quality-checker).
#     2. Add it as the next step in the WorkflowAgentDefinition.
#     3. Re-run and ask a question about a cake with nuts.
#     4. Check the portal workflow diagram — is the new step shown?
#   HINT: WorkflowAgentDefinition takes a `steps` list; append to it.
# ──────────────────────────────────────────────────────────────────────────
