"""Lab: Concurrent multi-agent (Frankie's Bakery) - fan-out / fan-in.

Same Frankie's Bakery cast as Lab 10, but a different orchestration shape.

Lab 10 (sequential) classifies a ticket and routes it to EXACTLY ONE
department (if/elif/else). This lab handles a complex ticket that legitimately
touches EVERY department at once, so instead of choosing one branch we:

    fan-out:   run menu + orders + complaints + hours specialists IN PARALLEL
               (asyncio.gather - they all see the same ticket simultaneously)
    fan-in:    bakery-synthesizer merges all four answers into one reply

                       +--> bakery-menu -------+
                       |                        |
    customer ticket ---+--> bakery-orders ------+--> bakery-synthesizer --> reply
                       |                        |
                       +--> bakery-complaints --+
                       |                        |
                       +--> bakery-hours -------+

Note on the deployed workflow agent: declarative CSDL workflows execute their
actions sequentially, so the deployed workflow.yaml invokes the four
specialists in order (it exists for portal visibility). The genuinely
concurrent fan-out is the client-driven asyncio.gather run below.
"""
import asyncio
import os
from pathlib import Path

from dotenv import load_dotenv
from azure.identity.aio import DefaultAzureCredential
from azure.ai.projects.aio import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition, WorkflowAgentDefinition

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")
WORKFLOW_NAME = os.environ.get("FOUNDRY_CONCURRENT_WORKFLOW_NAME", "bakery-concurrent-workflow")

BAKERY_DIR = Path(__file__).parent / "data" / "bakery"


def instructions_of(filename: str) -> str:
    """Pull the free-text Instructions block (between the first --- pair)."""
    text = (BAKERY_DIR / filename).read_text(encoding="utf-8")
    parts = text.split("---")
    return parts[1].strip() if len(parts) > 1 else text.strip()


# The four department specialists that fan out, plus the synthesizer that fans
# in. Same agents/knowledge as Lab 10 - only the orchestration differs.
SPECIALISTS = [
    ("bakery-menu", instructions_of("MenuAgent.md")),
    ("bakery-orders", instructions_of("OrdersAgent.md")),
    ("bakery-complaints", instructions_of("ComplaintsAgent.md")),
    ("bakery-hours", instructions_of("HoursAgent.md")),
]
SYNTHESIZER = ("bakery-synthesizer", instructions_of("SynthesizerAgent.md"))

# One complex ticket that touches all four departments at once: a wrong (nut)
# item on a placed order (complaint + order), a refund (order), a gluten-free
# menu question (menu), and a weekend-hours question (hours).
TICKET = (
    "I pre-ordered a custom birthday cake (order #4821) for Saturday pickup at your "
    "Midtown store, but it arrived topped with almonds even though I asked for nut-free "
    "- my son is allergic, so I'd like a refund. Can you also tell me the price of a "
    "6-inch gluten-free cake, and whether Midtown is open on Sunday if I need a "
    "replacement?"
)


async def main():
    async with DefaultAzureCredential() as credential:
        async with AIProjectClient(
            endpoint=PROJECT_ENDPOINT,
            credential=credential,
            allow_preview=True,  # Workflow agents are a preview feature (opt-in required).
        ) as project:
            openai = project.get_openai_client()

            # Create every agent so they are visible in the Foundry portal.
            for name, instructions in SPECIALISTS + [SYNTHESIZER]:
                agent = await project.agents.create_version(
                    agent_name=name,
                    definition=PromptAgentDefinition(model=MODEL_DEPLOYMENT, instructions=instructions),
                )
                print(f"Created agent '{agent.name}' (version {agent.version})")

            # Deploy the fan-out/fan-in shape as a workflow agent for portal
            # visibility. The CSDL lives in workflow.yaml next to this script.
            workflow_yaml = (Path(__file__).parent / "workflow.yaml").read_text(encoding="utf-8")
            workflow = await project.agents.create_version(
                agent_name=WORKFLOW_NAME,
                definition=WorkflowAgentDefinition(workflow=workflow_yaml),
            )
            print(f"Created workflow agent '{workflow.name}' (version {workflow.version})")

            async def ask(agent_name: str, text: str) -> str:
                resp = await openai.responses.create(
                    extra_body={"agent_reference": {"name": agent_name, "type": "agent_reference"}},
                    input=text,
                )
                return resp.output_text

            print("\n--- Customer ticket ---\n" + TICKET)

            # Try the single-trigger workflow agent first (server-orchestrated).
            conversation = await openai.conversations.create()
            try:
                response = await openai.responses.create(
                    conversation=conversation.id,
                    extra_body={"agent_reference": {"name": WORKFLOW_NAME, "type": "agent_reference"}},
                    input=TICKET,
                )
                print("\n--- Customer reply (single trigger) ---\n" + response.output_text)
            except Exception as exc:  # noqa: BLE001 - surface the preview-runtime failure explicitly
                # Workflow-agent runtime is preview and can be flaky for multi-step
                # flows. The workflow agent is still created and visible in the
                # portal; fall back to the genuinely concurrent client-driven run.
                print("\n[preview] Single-trigger workflow run failed on the service:")
                print("  " + str(exc).splitlines()[0][:200])
                print("[preview] Falling back to a client-driven CONCURRENT run.\n")

                # Fan-out: all four department specialists analyze the SAME ticket
                # at the same time (asyncio.gather starts them together).
                menu, orders, complaints, hours = await asyncio.gather(
                    ask("bakery-menu", TICKET),
                    ask("bakery-orders", TICKET),
                    ask("bakery-complaints", TICKET),
                    ask("bakery-hours", TICKET),
                )
                print("--- Menu (bakery-menu) ---\n" + menu + "\n")
                print("--- Orders (bakery-orders) ---\n" + orders + "\n")
                print("--- Complaints (bakery-complaints) ---\n" + complaints + "\n")
                print("--- Hours (bakery-hours) ---\n" + hours + "\n")

                # Fan-in: the synthesizer merges all four answers into one reply.
                reply = await ask(
                    "bakery-synthesizer",
                    f"Customer question:\n{TICKET}\n\n"
                    f"Menu specialist:\n{menu}\n\n"
                    f"Orders specialist:\n{orders}\n\n"
                    f"Complaints specialist:\n{complaints}\n\n"
                    f"Hours specialist:\n{hours}",
                )
                print("--- Customer reply (bakery-synthesizer) ---\n" + reply)

            print(
                f"\nDone. Open '{WORKFLOW_NAME}' in the Foundry portal (Agents / "
                "Workflows) to see the bakery fan-out / fan-in flow."
            )


if __name__ == "__main__":
    asyncio.run(main())


# ──────────────────────────────────────────────────────────────────────────
# PORTAL OBSERVATION
#   Open this workflow/agent → "Traces" tab (or the project "Tracing" page)
#   and expand the run. Because the four specialists ran concurrently
#   (asyncio.gather), their spans OVERLAP - they start at roughly the same
#   wall-clock time. Compare with Lab 10 (sequential conditional routing),
#   where only ONE specialist span appears and it is chained before the
#   synthesizer. (Workflow tracing is preview; prompt-agent tracing is GA.)
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# CHALLENGE  — Add error handling + retry to the fan-out
#
#   Currently if one of the concurrent agents fails, the whole gather()
#   call crashes.  Improve resilience:
#     1. Wrap each ask() in a try/except that returns a fallback string like
#        "FAILED: <reason>" instead of raising.
#     2. After gather, count how many specialists succeeded vs failed.
#     3. If any failed, re-issue just the failed agents (one retry) before
#        calling the synthesizer.
#   BONUS: Use asyncio.wait_for(ask(...), timeout=10) to cap each agent call
#   at 10 seconds and treat a timeout as a failure.
# ──────────────────────────────────────────────────────────────────────────
