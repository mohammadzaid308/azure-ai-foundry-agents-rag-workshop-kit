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
