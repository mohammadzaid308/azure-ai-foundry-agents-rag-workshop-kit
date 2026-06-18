import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    PromptAgentDefinition,
    AzureAISearchTool,
    AzureAISearchToolResource,
    AzureAISearchIndex,
)

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
AGENT_NAME = os.environ.get("FOUNDRY_AGENT_NAME", "search-agent")
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4.1-mini")
SEARCH_CONNECTION_NAME = os.environ["FOUNDRY_SEARCH_CONNECTION_NAME"]
SEARCH_INDEX_NAME = os.environ.get("FOUNDRY_SEARCH_INDEX_NAME", "workshop-index")

project = AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=DefaultAzureCredential())

search = AzureAISearchTool(
    azure_ai_search=AzureAISearchToolResource(
        indexes=[
            AzureAISearchIndex(
                connection_name=SEARCH_CONNECTION_NAME,
                index_name=SEARCH_INDEX_NAME,
            )
        ]
    )
)

agent = project.agents.create_version(
    agent_name=AGENT_NAME,
    definition=PromptAgentDefinition(
        model=MODEL_DEPLOYMENT,
        instructions="Answer questions using only the provided Azure AI Search index.",
        tools=[search],
    ),
)

openai = project.get_openai_client()
conversation = openai.conversations.create()
response = openai.responses.create(
    conversation=conversation.id,
    extra_body={"agent_reference": {"name": agent.name, "type": "agent_reference"}},
    input="Summarize what the indexed documents say about our return policy.",
)
print(response.output_text)
