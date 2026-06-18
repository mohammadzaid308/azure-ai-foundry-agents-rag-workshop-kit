import json
import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    PromptAgentDefinition,
    OpenApiTool,
    OpenApiFunctionDefinition,
    OpenApiAnonymousAuthDetails,
)

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
AGENT_NAME = os.environ.get("FOUNDRY_AGENT_NAME", "openapi-agent")
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")

project = AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=DefaultAzureCredential())

with open(os.path.join(os.path.dirname(__file__), "weather-openapi.json")) as f:
    spec = json.load(f)

openapi = OpenApiTool(
    openapi=OpenApiFunctionDefinition(
        name="weather",
        description="Get weather for a location.",
        spec=spec,
        auth=OpenApiAnonymousAuthDetails(),
    )
)

agent = project.agents.create_version(
    agent_name=AGENT_NAME,
    definition=PromptAgentDefinition(
        model=MODEL_DEPLOYMENT,
        instructions="Use the weather API to answer weather questions.",
        tools=[openapi],
    ),
)

openai = project.get_openai_client()
conversation = openai.conversations.create()
response = openai.responses.create(
    conversation=conversation.id,
    extra_body={"agent_reference": {"name": agent.name, "type": "agent_reference"}},
    input="What is the weather in London right now?",
)
print(response.output_text)
