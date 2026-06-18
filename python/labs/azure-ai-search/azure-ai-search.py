import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    PromptAgentDefinition,
    AzureAISearchTool,
    AzureAISearchToolResource,
    AISearchIndexResource,
)

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
AGENT_NAME = os.environ.get("FOUNDRY_AGENT_NAME", "search-agent")
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")
SEARCH_CONNECTION_NAME = os.environ["FOUNDRY_SEARCH_CONNECTION_NAME"]
SEARCH_INDEX_NAME = os.environ.get("FOUNDRY_SEARCH_INDEX_NAME", "workshop-index")

project = AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=DefaultAzureCredential())

search_connection = project.connections.get(SEARCH_CONNECTION_NAME)

search = AzureAISearchTool(
    azure_ai_search=AzureAISearchToolResource(
        indexes=[
            AISearchIndexResource(
                project_connection_id=search_connection.id,
                index_name=SEARCH_INDEX_NAME,
                query_type="vector_semantic_hybrid",
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
    input="Based on the indexed documents, recommend a couple of highly rated hotels and summarize what makes them appealing.",
)
print(response.output_text)
