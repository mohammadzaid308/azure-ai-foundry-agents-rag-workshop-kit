"""Lab: Sequential multi-agent with conditional (if/else) routing.

Scenario: Frankie's Bakery customer support. This lab shows a REAL sequential
workflow that branches:

    bakery-orchestrator        (classify the ticket -> route)
            |
            v  ConditionGroup / if-elif-else  (exactly ONE branch fires)
    +-------+--------+-----------+---------+
    |       |        |           |         |
   menu   orders  complaints   hours     else (out of scope)
    |       |        |           |
    +-------+--------+-----------+
            v
    bakery-synthesizer          (warm customer-facing reply)

Contrast with Lab 11 (concurrent): there, ALL department specialists run in
parallel (fan-out) and a synthesizer merges them (fan-in). Here, the
orchestrator picks exactly ONE specialist - the essence of conditional routing.

Department knowledge is loaded from the local instruction files in
./data/bakery so the bakery's real menu/orders/complaints/hours rules drive the
specialists. Agent creation + runs reach Azure AI Foundry.
"""
import json
import os
import re
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


project = AIProjectClient(
    endpoint=PROJECT_ENDPOINT,
    credential=DefaultAzureCredential(),
    allow_preview=True,  # Workflow agents are a preview feature (opt-in required).
)
openai = project.get_openai_client()

# One orchestrator (router), four department specialists, one synthesizer.
# The orchestrator only emits a route; each specialist owns its department
# knowledge; the synthesizer writes the final customer-facing message.
agent_specs = [
    ("bakery-orchestrator", instructions_of("Orchestrator_agent.md")),
    ("bakery-menu", instructions_of("MenuAgent.md")),
    ("bakery-orders", instructions_of("OrdersAgent.md")),
    ("bakery-complaints", instructions_of("ComplaintsAgent.md")),
    ("bakery-hours", instructions_of("HoursAgent.md")),
    ("bakery-synthesizer", instructions_of("SynthesizerAgent.md")),
]

for name, instructions in agent_specs:
    agent = project.agents.create_version(
        agent_name=name,
        definition=PromptAgentDefinition(model=MODEL_DEPLOYMENT, instructions=instructions),
    )
    print(f"Created agent '{agent.name}' (version {agent.version})")

# Deploy the conditional pipeline as a workflow agent so the BRANCHING shows up
# in the Foundry portal (Agents / Workflows). The CSDL uses a ConditionGroup
# (switch/case) to route to exactly one department. Definition lives in
# workflow.yaml next to this script.
workflow_yaml = (Path(__file__).parent / "workflow.yaml").read_text(encoding="utf-8")
workflow = project.agents.create_version(
    agent_name=WORKFLOW_NAME,
    definition=WorkflowAgentDefinition(workflow=workflow_yaml),
)
print(f"Created workflow agent '{workflow.name}' (version {workflow.version})")

# A single-intent ticket so the router picks ONE branch. Try changing it to an
# orders / complaints / hours question and watch the chosen route change.
ticket = (
    "Hi! My daughter is allergic to tree nuts. Is the almond croissant safe for her, "
    "and what gluten-free options do you have?"
)
print("\n--- Customer ticket ---\n" + ticket)

ROUTE_TO_AGENT = {
    "menu": "bakery-menu",
    "orders": "bakery-orders",
    "complaints": "bakery-complaints",
    "hours": "bakery-hours",
}

OUT_OF_SCOPE = (
    "That question is outside what I can help with at Frankie's Bakery. I can help "
    "with menu items and allergens, your orders, complaints, and store hours - try "
    "one of those and I'll get you the right answer."
)

conversation = openai.conversations.create()


def run(agent_name: str, text: str) -> str:
    return openai.responses.create(
        conversation=conversation.id,
        extra_body={"agent_reference": {"name": agent_name, "type": "agent_reference"}},
        input=text,
    ).output_text


def parse_route(text: str) -> str:
    """Pull {"route": "..."} out of the orchestrator's reply; default to else."""
    try:
        match = re.search(r"\{.*\}", text, re.S)
        route = json.loads(match.group(0)).get("route", "else") if match else "else"
    except (json.JSONDecodeError, AttributeError):
        route = "else"
    route = str(route).lower().strip()
    return route if route in ROUTE_TO_AGENT else "else"


try:
    response = openai.responses.create(
        conversation=conversation.id,
        extra_body={"agent_reference": {"name": WORKFLOW_NAME, "type": "agent_reference"}},
        input=ticket,
    )
    print("\n--- Customer reply (single trigger, server-routed) ---\n" + response.output_text)
except Exception as exc:  # noqa: BLE001 - surface the preview-runtime failure explicitly
    # The Foundry "workflow agent" runtime is in preview and can 500 on multi-step
    # conditional flows. The workflow agent is still created and visible in the
    # portal; fall back to a client-driven run that performs the SAME routing -
    # this is where the if/elif/else branching is demonstrated explicitly.
    print("\n[preview] Single-trigger workflow run failed on the service:")
    print("  " + str(exc).splitlines()[0][:200])
    print("[preview] Falling back to a client-driven conditional run.\n")

    # Step 1: orchestrator classifies the ticket into a route.
    routing = run("bakery-orchestrator", ticket)
    route = parse_route(routing)
    print(f"--- Orchestrator (bakery-orchestrator) ---\nroute = {route}\n")

    # Step 2: BRANCH - exactly one department specialist handles the ticket.
    if route in ROUTE_TO_AGENT:
        specialist_name = ROUTE_TO_AGENT[route]
        specialist_answer = run(specialist_name, ticket)
        print(f"--- Specialist ({specialist_name}) ---\n{specialist_answer}\n")

        # Step 3: synthesizer turns the specialist JSON into a warm reply.
        synth_input = (
            f"Customer question: {ticket}\n"
            f"Specialist answer: {specialist_answer}"
        )
        reply = run("bakery-synthesizer", synth_input)
        print("--- Customer reply (bakery-synthesizer) ---\n" + reply)
    else:
        # else-branch: nothing matched - return the catch-all message.
        print("--- Customer reply (out of scope) ---\n" + OUT_OF_SCOPE)

print(
    f"\nDone. Open '{WORKFLOW_NAME}' in the Foundry portal (Agents / Workflows) to "
    "see the orchestrator -> conditional routing -> synthesizer graph."
)


# ──────────────────────────────────────────────────────────────────────────
# PORTAL OBSERVATIONS (3 things to check)
#
#   1. Microsoft Foundry portal → "Agents" → "Workflows" → open
#      bakery-support-workflow. The graph shows the orchestrator fanning into a
#      ConditionGroup with one branch per department, then merging into the
#      synthesizer. This is the visual form of the if/elif/else above.
#
#   2. Run the workflow in the Playground with different tickets (a menu
#      question, an order question, a complaint, an hours question) and watch a
#      DIFFERENT branch light up each time.
#
#   3. Open the workflow's "Traces" tab: the run is one trace; only the chosen
#      branch's agent span appears (the others never execute). Compare with Lab
#      11, where all four specialist spans appear and OVERLAP in time.
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# CHALLENGE  — Add a fifth branch + a guard
#
#   1. Add a "feedback" branch: create a bakery-feedback agent and a new
#      ConditionGroup case (route = "feedback") in workflow.yaml, then extend
#      ROUTE_TO_AGENT above so the client path can reach it too.
#   2. Add a guard BEFORE the synthesizer: if the chosen specialist's JSON has
#      "escalate": true or "allergen_flag": true, prepend a "ESCALATED:" line
#      to the synthesizer input so the customer reply flags it.
#   3. Re-run with a complaint about an allergic reaction and confirm the guard
#      fires. Which branch did the orchestrator pick?
# ──────────────────────────────────────────────────────────────────────────
