import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition, WorkflowAgentDefinition

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")
WORKFLOW_NAME = os.environ.get("FOUNDRY_WORKFLOW_NAME", "support-ticket-workflow")

project = AIProjectClient(
    endpoint=PROJECT_ENDPOINT,
    credential=DefaultAzureCredential(),
    allow_preview=True,  # Workflow agents are a preview feature (opt-in required).
)
openai = project.get_openai_client()

#   support-triage  ->  support-resolver  ->  support-reply
#   (classify)          (figure out fix)      (write the customer reply)
#
# This is the "sequential" multi-agent pattern: a clear hand-off from one
# specialist to the next.
agent_specs = [
    (
        "support-triage",
        "You are a customer-support triage agent. Read the customer's message and "
        "classify it. Reply with exactly two lines:\n"
        "Category: <Billing | Technical | Account | Other>\n"
        "Priority: <Low | Medium | High>",
    ),
    (
        "support-resolver",
        "You are a customer-support resolution agent. Given the customer's message "
        "and its triage, write clear, numbered steps that will resolve the issue.",
    ),
    (
        "support-reply",
        "You are a senior support agent. Rewrite the resolution into a warm, "
        "empathetic reply addressed to the customer. Keep it concise and "
        "professional. Return only the reply.",
    ),
]

for name, instructions in agent_specs:
    agent = project.agents.create_version(
        agent_name=name,
        definition=PromptAgentDefinition(model=MODEL_DEPLOYMENT, instructions=instructions),
    )
    print(f"Created agent '{agent.name}' (version {agent.version})")

# Deploy the pipeline as a *workflow agent* so the workflow itself (triage ->
# resolver -> reply) shows up in the Foundry portal, not just the individual
# agents. The CSDL definition lives in workflow.yaml next to this script.
workflow_path = os.path.join(os.path.dirname(__file__), "workflow.yaml")
with open(workflow_path, "r", encoding="utf-8") as f:
    workflow_yaml = f.read()

workflow = project.agents.create_version(
    agent_name=WORKFLOW_NAME,
    definition=WorkflowAgentDefinition(workflow=workflow_yaml),
)
print(f"Created workflow agent '{workflow.name}' (version {workflow.version})")

ticket = (
    "Hi, I was charged twice for my subscription this month, and the app keeps "
    "crashing whenever I open the billing page. Can you help?"
)
print("\n--- Customer ticket ---\n" + ticket)

# Run the whole pipeline with ONE trigger: a single request to the workflow
# agent. Foundry orchestrates triage -> resolver -> reply server-side.
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
    # The Foundry "workflow agent" runtime is in preview. Some deployments
    # currently return a 500 ("Cannot interpret incoming message ...
    # ActionExecutorResult") when a workflow chains more than one agent step.
    # The workflow agent itself is still created and visible in the portal; we
    # fall back to driving the same agents over one shared conversation so the
    # lab still produces a result.
    print("\n[preview] Single-trigger workflow run failed on the service:")
    print("  " + str(exc).splitlines()[0][:200])
    print("[preview] Falling back to a client-driven sequential run of the same agents.\n")

    triage = run("support-triage", ticket)
    print("--- Triage (support-triage) ---\n" + triage + "\n")
    resolution = run("support-resolver", f"Customer message:\n{ticket}\n\nTriage:\n{triage}")
    print("--- Resolution (support-resolver) ---\n" + resolution + "\n")
    reply = run("support-reply", resolution)
    print("--- Customer reply (support-reply) ---\n" + reply)

print(
    f"\nDone. Open '{WORKFLOW_NAME}' in the Foundry portal (Agents / Workflows) "
    "to see the triage -> resolver -> reply workflow."
)
