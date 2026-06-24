import asyncio
import os

from dotenv import load_dotenv
from azure.identity.aio import DefaultAzureCredential
from azure.ai.projects.aio import AIProjectClient
from azure.ai.projects.models import PromptAgentDefinition, WorkflowAgentDefinition

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")
WORKFLOW_NAME = os.environ.get("FOUNDRY_CONCURRENT_WORKFLOW_NAME", "support-escalation-workflow")

# three specialist agents look at the same complaint *in parallel* (fan-out),
# then a supervisor agent merges their findings into one reply (fan-in).
COMPLAINT = (
    "I've been a customer for 3 years and I'm really frustrated. My internet has "
    "dropped every evening this week, and on top of that you charged me a $15 "
    "'equipment fee' I never agreed to. I want this sorted out today."
)

SPECIALISTS = [
    (
        "support-sentiment",
        "You are a customer-sentiment analyst. In 1-2 sentences, describe the "
        "customer's emotion and how urgent this is.",
    ),
    (
        "support-technical",
        "You are a technical support specialist. Identify the likely technical "
        "problem and list concrete troubleshooting steps.",
    ),
    (
        "support-billing",
        "You are a billing specialist. Identify any billing or refund issue and "
        "state exactly what action should be taken.",
    ),
]

SUPERVISOR = (
    "support-supervisor",
    "You are a support supervisor. Using the specialist analyses, write one "
    "warm, professional reply to the customer that addresses every point, then "
    "add a final line 'Recommended internal action: ...'.",
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
            for name, instructions in SPECIALISTS + [SUPERVISOR]:
                agent = await project.agents.create_version(
                    agent_name=name,
                    definition=PromptAgentDefinition(model=MODEL_DEPLOYMENT, instructions=instructions),
                )
                print(f"Created agent '{agent.name}' (version {agent.version})")

            # Deploy the escalation as a *workflow agent* so the flow (sentiment,
            # technical, billing -> supervisor) shows up in the Foundry portal,
            # not just the individual agents. The CSDL lives in workflow.yaml.
            workflow_path = os.path.join(os.path.dirname(__file__), "workflow.yaml")
            with open(workflow_path, "r", encoding="utf-8") as f:
                workflow_yaml = f.read()

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

            print("\n--- Customer complaint ---\n" + COMPLAINT)

            # Run the escalation with ONE trigger: a single request to the
            # workflow agent. Foundry orchestrates the specialists -> supervisor
            # server-side.
            conversation = await openai.conversations.create()
            try:
                response = await openai.responses.create(
                    conversation=conversation.id,
                    extra_body={"agent_reference": {"name": WORKFLOW_NAME, "type": "agent_reference"}},
                    input=COMPLAINT,
                )
                print("\n--- Supervisor reply (single trigger) ---\n" + response.output_text)
            except Exception as exc:  # noqa: BLE001 - surface the preview-runtime failure explicitly
                # The Foundry "workflow agent" runtime is in preview and can be
                # flaky for multi-step flows. The workflow agent is still created
                # and visible in the portal; we fall back to the genuinely
                # concurrent client-driven run (fan-out with asyncio.gather).
                print("\n[preview] Single-trigger workflow run failed on the service:")
                print("  " + str(exc).splitlines()[0][:200])
                print("[preview] Falling back to a client-driven concurrent run of the same agents.\n")

                # Fan-out: all three specialists analyze the complaint at once.
                sentiment, technical, billing = await asyncio.gather(
                    ask("support-sentiment", COMPLAINT),
                    ask("support-technical", COMPLAINT),
                    ask("support-billing", COMPLAINT),
                )
                print("--- Sentiment (support-sentiment) ---\n" + sentiment + "\n")
                print("--- Technical (support-technical) ---\n" + technical + "\n")
                print("--- Billing (support-billing) ---\n" + billing + "\n")

                # Fan-in: the supervisor merges everything into one reply.
                reply = await ask(
                    "support-supervisor",
                    f"Customer complaint:\n{COMPLAINT}\n\n"
                    f"Sentiment analysis:\n{sentiment}\n\n"
                    f"Technical analysis:\n{technical}\n\n"
                    f"Billing analysis:\n{billing}",
                )
                print("--- Supervisor reply (support-supervisor) ---\n" + reply)

            print(
                f"\nDone. Open '{WORKFLOW_NAME}' in the Foundry portal (Agents / "
                "Workflows) to see the concurrent support escalation flow."
            )


if __name__ == "__main__":
    asyncio.run(main())


# ──────────────────────────────────────────────────────────────────────────
# 👁  PORTAL OBSERVATION
#   Open this workflow/agent → "Traces" tab (or the project "Tracing" page)
#   and expand the run. Because the agents ran concurrently (asyncio.gather),
#   the individual agent spans OVERLAP - they start at roughly the same
#   wall-clock time. Compare with the sequential lab, where spans are chained
#   end-to-end. (Workflow tracing is preview; prompt-agent tracing is GA.)
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# 🏋  CHALLENGE  — Add error handling + retry
#
#   Currently if one of the concurrent agents fails, the whole gather()
#   call crashes.  Improve resilience:
#     1. Wrap each agent call in a try/except that returns a fallback
#        dict {"agent": name, "result": "FAILED: " + str(e), "ok": False}
#     2. In the aggregator, count how many agents succeeded vs failed.
#     3. If any failed, re-issue just the failed agents (one retry).
#   BONUS: Use asyncio.wait_for(..., timeout=10) to cap each agent call
#   at 10 seconds and treat a timeout as a failure.
# ──────────────────────────────────────────────────────────────────────────
