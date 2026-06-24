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
        instructions="Use the weather API to answer weather questions. Always call it with format=j1 so you get JSON, then summarize the current conditions.",
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


# ──────────────────────────────────────────────────────────────────────────
# 👁  PORTAL OBSERVATION
#   Foundry portal → Agents → <agent> → Playground → ask a weather
#   question.  In the "Show details" panel you can see:
#     • The OpenAPI spec the model resolved the function from.
#     • The exact HTTP request the model formed (URL + query params).
#     • The raw JSON response before the model summarized it.
#   This is the live "tool-call trace" — compare it to the function tool
#   trace in Lab 2c to spot the difference (HTTP vs local Python call).
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# 🏋  CHALLENGE  — Add a second OpenAPI tool
#
#   A tiny free API: https://dog.ceo/api/breeds/list/all
#   1. Write a minimal OpenAPI 3.0 JSON spec for GET /api/breeds/list/all
#      (one path, no parameters, response is {message: {breeds_map}}).
#   2. Create a second OpenApiTool called "dogs" using the same
#      OpenApiAnonymousAuthDetails.
#   3. Pass both tools to the agent definition (tools=[weather_tool, dogs_tool]).
#   4. Ask: "Is it raining in London, and if so, what dog breed matches
#      the mood?"  Watch the model decide which tool to call.
# ──────────────────────────────────────────────────────────────────────────
