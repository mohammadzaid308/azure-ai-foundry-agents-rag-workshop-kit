import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")

project = AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=DefaultAzureCredential())
openai = project.get_openai_client()

topic = "the benefits of unit testing"

# Sequential pattern: the output of one step feeds the next (writer -> editor -> headline)
draft = openai.responses.create(
    model=MODEL_DEPLOYMENT,
    input=f"You are a writer. Write a short paragraph about {topic}.",
).output_text
print("--- Draft ---\n" + draft)

edited = openai.responses.create(
    model=MODEL_DEPLOYMENT,
    input=f"You are an editor. Tighten and improve this paragraph:\n\n{draft}",
).output_text
print("\n--- Edited ---\n" + edited)

headline = openai.responses.create(
    model=MODEL_DEPLOYMENT,
    input=f"You are a copywriter. Write one catchy headline for this text:\n\n{edited}",
).output_text
print("\n--- Headline ---\n" + headline)
