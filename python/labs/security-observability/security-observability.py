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
