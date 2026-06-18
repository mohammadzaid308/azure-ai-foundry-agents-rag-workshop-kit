import json
import os

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4.1-mini")

project = AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=DefaultAzureCredential())
openai = project.get_openai_client()


def get_weather(location: str) -> str:
    return f"It is 21C and sunny in {location}."


# 1) Declare a local tool the model can call
tools = [
    {
        "type": "function",
        "name": "get_weather",
        "description": "Get the current weather for a city.",
        "parameters": {
            "type": "object",
            "properties": {"location": {"type": "string"}},
            "required": ["location"],
        },
    }
]

question = "What is the weather in Seattle?"

# 2) First call: the model decides whether to call the function
response = openai.responses.create(
    model=MODEL_DEPLOYMENT,
    input=[{"role": "user", "content": question}],
    tools=tools,
)

# 3) Execute any requested calls and return the outputs to the model
tool_outputs = []
for item in response.output:
    if getattr(item, "type", "") == "function_call":
        args = json.loads(item.arguments)
        result = get_weather(args["location"])
        tool_outputs.append(
            {"type": "function_call_output", "call_id": item.call_id, "output": result}
        )

# 4) Second call: the model produces the final grounded answer
if tool_outputs:
    response = openai.responses.create(
        model=MODEL_DEPLOYMENT,
        previous_response_id=response.id,
        input=tool_outputs,
        tools=tools,
    )

print(response.output_text)
