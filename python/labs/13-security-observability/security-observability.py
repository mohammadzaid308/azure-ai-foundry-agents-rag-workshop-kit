import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")

# 1) Identity: DefaultAzureCredential uses az login locally and Managed Identity in Azure.
credential = DefaultAzureCredential()

# 2) Governance check: confirm RBAC by acquiring a Foundry data-plane token.
token = credential.get_token("https://ai.azure.com/.default")
print(f"Token acquired; expires_on={token.expires_on}")

project = AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=credential)
openai = project.get_openai_client()

# 3) Observability (optional): if APPLICATIONINSIGHTS_CONNECTION_STRING is set,
#    enable Azure Monitor OpenTelemetry tracing. Requires azure-monitor-opentelemetry.
#    opentelemetry ships as a dependency of azure-monitor-opentelemetry, so the
#    tracer is always available; without configure_azure_monitor() spans simply
#    become no-ops instead of being exported.
from opentelemetry import trace

tracer_provider = None
if os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    try:
        from azure.monitor.opentelemetry import configure_azure_monitor

        configure_azure_monitor()
        tracer_provider = trace.get_tracer_provider()
        print("Azure Monitor tracing enabled.")
    except ImportError:
        print("Install azure-monitor-opentelemetry to enable tracing.")

tracer = trace.get_tracer(__name__)

# Nested spans: a parent "handle" operation wraps two child model calls
# (draft -> refine). Each span is exported to Application Insights as a
# dependency, and the parent/child relationship is preserved in the trace
# so you can see the full operation tree in the portal.
with tracer.start_as_current_span("support.request.handle") as root:
    root.set_attribute("workshop.lab", "security-observability")
    root.set_attribute("foundry.model", MODEL_DEPLOYMENT)

    with tracer.start_as_current_span("foundry.responses.draft") as draft:
        draft.set_attribute("foundry.step", "draft")
        first = openai.responses.create(
            model=MODEL_DEPLOYMENT, input="Give one safety tip for AI agents."
        )
        tip = first.output_text
        draft.set_attribute("foundry.output.length", len(tip))
        print(f"Draft: {tip}")

    with tracer.start_as_current_span("foundry.responses.refine") as refine:
        refine.set_attribute("foundry.step", "refine")
        second = openai.responses.create(
            model=MODEL_DEPLOYMENT,
            input=f"Rewrite this as a single concise checklist item: {tip}",
        )
        print(f"Refined: {second.output_text}")

# Flush telemetry so spans are exported before the process exits.
if tracer_provider is not None:
    tracer_provider.force_flush()
    print("Telemetry flushed to Azure Monitor.")


# ──────────────────────────────────────────────────────────────────────────
# PORTAL OBSERVATION
#   1. This lab emits CUSTOM OpenTelemetry spans from direct model calls (not
#      an agent), so view them in Azure Monitor → Application Insights →
#      Transaction search (set APPLICATIONINSIGHTS_CONNECTION_STRING first).
#      Find "support.request.handle" and expand the child spans
#      (foundry.responses.draft → foundry.responses.refine); note the
#      "workshop.lab" and "foundry.output.length" attributes. In Foundry these
#      also surface under "Tracing" (classic) / "Operate → Tracing" (new).
#
#   2. App Insights → Logs: set a KQL alert on output.length dropping below a
#      threshold - a real quality signal.
#
#   3. Azure portal → your Foundry resource/project → "Access control (IAM)".
#      Confirm your user has "Foundry User" (formerly Azure AI User) or
#      "Foundry Project Manager" (formerly Azure AI Project Manager). Remove a
#      role (undo after!) and observe the 403 on the next run.
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# CHALLENGE  — Add a custom span attribute for latency SLO
#
#   The script already records a draft and a refine span.  Extend it:
#     1. Import `time`.
#     2. Before the draft span, record `t_start = time.time()`.
#     3. After the refine span, compute `total_ms = (time.time()-t_start)*1000`.
#     4. Set root.set_attribute("workshop.latency_ms", total_ms).
#     5. Add an assertion: if total_ms > 5000, print "SLO BREACH".
#   In a real system you'd emit this as a metric and page on-call.
#   BONUS: Can you add the same latency attribute to each child span?
# ──────────────────────────────────────────────────────────────────────────
