import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")

# Create project and openai clients to call Foundry API
project = AIProjectClient(
    endpoint=PROJECT_ENDPOINT,
    credential=DefaultAzureCredential(),
)
openai = project.get_openai_client()

# Run a responses API call
response = openai.responses.create(
    model=MODEL_DEPLOYMENT,
    input="What is the size of France in square miles?",
)
print(f"Response output: {response.output_text}")

# ──────────────────────────────────────────────────────────────────────────
# 👁  PORTAL OBSERVATION
#   After running, go to: Foundry portal → your project → Monitoring →
#   "Traces" (or "Activity"). You should see a new trace entry for the
#   responses.create call with its latency and the model name.
#   Nothing shows if APPLICATIONINSIGHTS_CONNECTION_STRING is not set,
#   but the call still appears in the project's audit log.
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# 🏋  CHALLENGE  (complete the code, then re-run)
#
#   The agent currently asks one hard-coded question.
#   Add a second responses.create call that:
#     1. Asks the model to convert the answer to square KILOMETRES.
#     2. Passes the previous response.output_text as context in the input
#        (hint: use a list of {"role":"user"} / {"role":"assistant"} dicts
#        as the `input` parameter, or just embed it in the next prompt string).
#     3. Prints both answers side by side.
#
#   HINT:
#     response2 = openai.responses.create(
#         model=MODEL_DEPLOYMENT,
#         input=f"Convert '{response.output_text}' from square miles to square km.",
#     )
#
#   Expected output shape:
#     France: 248573.0 square miles
#     France: 643801.0 square km
# ──────────────────────────────────────────────────────────────────────────
