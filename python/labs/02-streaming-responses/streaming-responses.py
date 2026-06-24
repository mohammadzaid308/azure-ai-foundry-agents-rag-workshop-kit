import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")

# Create the Foundry project + OpenAI clients
project = AIProjectClient(
    endpoint=PROJECT_ENDPOINT,
    credential=DefaultAzureCredential(),
)
openai = project.get_openai_client()

# Stream the response as it is generated
stream = openai.responses.create(
    model=MODEL_DEPLOYMENT,
    input="Explain what Azure AI Foundry is in three short sentences.",
    stream=True,
)

for event in stream:
    # Print only the text delta events as they arrive
    if getattr(event, "type", "") == "response.output_text.delta":
        print(event.delta, end="", flush=True)
print()


# ──────────────────────────────────────────────────────────────────────────
# 👁  PORTAL OBSERVATION
#   Like Lab 1 this is a direct model call (no agent), so it won't show on the
#   Agents page. Watch usage under "Models + endpoints" (classic) /
#   "Build → Models" (new Foundry) → your deployment → Metrics, or connect
#   Application Insights (Lab 13) to capture the span under "Tracing".
#   The streaming win is client-side: note the LOWER time-to-first-token here
#   vs the non-streaming call in Lab 1 - total time is roughly the same.
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# 🏋  CHALLENGE
#
#   Measure time-to-first-token yourself:
#     1. Import `time` at the top.
#     2. Record `t0 = time.time()` before the stream.
#     3. Inside the loop, the first time you print a delta, also print
#        f"[TTFT {time.time()-t0:.3f}s]" and set a `first_seen = True`
#        flag so you only print it once.
#     4. After the loop ends, print total elapsed time.
#
#   Why does this matter?  TTFT is a key UX metric for chat apps and
#   you can see it in Application Insights as a custom metric later.
# ──────────────────────────────────────────────────────────────────────────
