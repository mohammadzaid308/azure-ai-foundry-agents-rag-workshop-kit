import json
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

# Map each grounding reference (doc_0, doc_1, ...) to a real hotel name.
# The Azure AI Search tool returns the retrieved documents (in order) inside an
# `azure_ai_search_call_output` item; doc_N corresponds to documents[N].
retrieved_docs: list[dict] = []
for item in response.output:
    if getattr(item, "type", "") == "azure_ai_search_call_output":
        payload = getattr(item, "output", None)
        if payload:
            try:
                retrieved_docs = json.loads(payload).get("documents", [])
            except json.JSONDecodeError:
                retrieved_docs = []


def doc_label(title: str) -> str:
    """Resolve a doc_N reference to its hotel name (first line of content)."""
    if title.startswith("doc_"):
        try:
            doc = retrieved_docs[int(title.split("_", 1)[1])]
            name = (doc.get("content") or "").strip().splitlines()
            if name:
                return name[0].strip()
        except (ValueError, IndexError):
            pass
    return title


# Collect citation annotations (the text span they ground + their source ref).
spans: list[tuple[int, int, str]] = []
for item in response.output:
    for content in getattr(item, "content", None) or []:
        for annotation in getattr(content, "annotations", None) or []:
            title = getattr(annotation, "title", None)
            start = getattr(annotation, "start_index", None)
            end = getattr(annotation, "end_index", None)
            if title is not None and start is not None and end is not None:
                spans.append((start, end, doc_label(title)))

# Number each unique source by the order it first appears in the answer.
sources: list[str] = []
for _, _, label in sorted(spans, key=lambda s: s[0]):
    if label not in sources:
        sources.append(label)

# Rewrite inline markers (e.g. 【4:0†source】) with clean [n] references.
# Replace from the end so earlier indices stay valid.
text = response.output_text
for start, end, label in sorted(spans, key=lambda s: s[0], reverse=True):
    number = sources.index(label) + 1
    text = text[:start] + f"[{number}]" + text[end:]

print(text)
if sources:
    print("\nCitations:")
    for index, label in enumerate(sources, start=1):
        print(f"  [{index}] {label}")
