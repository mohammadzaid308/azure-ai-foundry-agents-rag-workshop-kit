import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
AGENT_NAME = os.environ["FOUNDRY_AGENT_NAME"]

# Create project and openai clients to call Foundry API
project = AIProjectClient(
    endpoint=PROJECT_ENDPOINT,
    credential=DefaultAzureCredential(),
)
openai = project.get_openai_client()

# Create a conversation for multi-turn chat
conversation = openai.conversations.create()

# Chat with the agent to answer questions
response = openai.responses.create(
    conversation=conversation.id,
    extra_body={"agent_reference": {"name": AGENT_NAME, "type": "agent_reference"}},
    input="What is the size of France in square miles?",
)
print(response.output_text)

# Ask a follow-up question in the same conversation
response = openai.responses.create(
    conversation=conversation.id,
    extra_body={"agent_reference": {"name": AGENT_NAME, "type": "agent_reference"}},
    input="And what is the capital city?",
)
print(response.output_text)


# ──────────────────────────────────────────────────────────────────────────
# 👁  PORTAL OBSERVATION
#   Foundry portal → Agents → <your agent> → "Conversations".
#   After running, you should see the conversation ID here.  Click it to
#   inspect the full message history including the model's reasoning.
#   Note how both turns (country name + capital city) are stored in a
#   single conversation thread.
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# 🏋  CHALLENGE  — Add a third turn + measure context growth
#
#   Add a third openai.responses.create call to the same conversation:
#     input="What language do people speak there, and is it an EU member?"
#   Then:
#     a) Print the full conversation thread by calling
#        openai.conversations.messages.list(conversation_id=conversation.id)
#        and showing each message's role + content.
#     b) Count how many tokens are in the conversation so far using
#        response.usage.input_tokens from the last response.
#   This shows how conversation context grows with each turn.
# ──────────────────────────────────────────────────────────────────────────
