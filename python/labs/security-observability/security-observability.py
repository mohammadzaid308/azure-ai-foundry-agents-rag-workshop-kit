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
span_cm = None
if os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING"):
    try:
        from azure.monitor.opentelemetry import configure_azure_monitor
        from opentelemetry import trace

        configure_azure_monitor()
        span_cm = trace.get_tracer(__name__).start_as_current_span("foundry.responses.create")
        print("Azure Monitor tracing enabled.")
    except ImportError:
        print("Install azure-monitor-opentelemetry to enable tracing.")

if span_cm:
    with span_cm:
        response = openai.responses.create(
            model=MODEL_DEPLOYMENT, input="Give one safety tip for AI agents."
        )
else:
    response = openai.responses.create(
        model=MODEL_DEPLOYMENT, input="Give one safety tip for AI agents."
    )

print(response.output_text)
