"""Lab: Function tools (Frankie's Bakery storefront assistant).

The model is given three local tools backed by the JSON product catalog in
./data/products. The catalog read happens locally, so the tool logic works
offline; only the model calls reach Azure AI Foundry.
"""
import json
import os
from pathlib import Path

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")

DATA_DIR = Path(__file__).parent / "data" / "products"
ORDERS: list[dict] = []


def _catalog() -> list[dict]:
    return [json.loads(p.read_text()) for p in sorted(DATA_DIR.glob("*.json"))]


def list_products(category: str | None = None) -> str:
    items = _catalog()
    if category:
        items = [p for p in items if p["category"].lower() == category.lower()]
    summary = [
        {"product_id": p["product_id"], "name": p["name"],
         "category": p["category"], "price": p["price"],
         "availability": p["availability"]}
        for p in items
    ]
    return json.dumps(summary)


def get_product(product_id: str) -> str:
    for p in _catalog():
        if p["product_id"].lower() == product_id.lower():
            return json.dumps(p)
    return json.dumps({"error": f"No product {product_id}"})


def place_order(product_id: str, quantity: int) -> str:
    match = [p for p in _catalog() if p["product_id"].lower() == product_id.lower()]
    if not match:
        return json.dumps({"error": f"No product {product_id}"})
    p = match[0]
    order = {
        "order_id": f"ORD-{len(ORDERS) + 1:04d}",
        "product_id": p["product_id"],
        "name": p["name"],
        "quantity": quantity,
        "line_total": round(p["price"] * quantity, 2),
    }
    ORDERS.append(order)
    return json.dumps(order)


DISPATCH = {"list_products": list_products, "get_product": get_product, "place_order": place_order}

tools = [
    {
        "type": "function", "name": "list_products",
        "description": "List bakery products, optionally filtered by category (Bread, Cake, Pastry, ...).",
        "parameters": {"type": "object",
                       "properties": {"category": {"type": "string"}}, "required": []},
    },
    {
        "type": "function", "name": "get_product",
        "description": "Get full detail (ingredients, rating, availability) for one product by its product_id.",
        "parameters": {"type": "object",
                       "properties": {"product_id": {"type": "string"}}, "required": ["product_id"]},
    },
    {
        "type": "function", "name": "place_order",
        "description": "Place an order for a product_id and quantity. Returns an order confirmation.",
        "parameters": {"type": "object",
                       "properties": {"product_id": {"type": "string"},
                                      "quantity": {"type": "integer"}},
                       "required": ["product_id", "quantity"]},
    },
]

project = AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=DefaultAzureCredential())
openai = project.get_openai_client()

question = (
    "I'd like two loaves of your Challah Bread. "
    "First confirm it exists and tell me the price, then place the order."
)

response = openai.responses.create(
    model=MODEL_DEPLOYMENT,
    input=[{"role": "user", "content": question}],
    tools=tools,
)

# Tool-call loop: keep resolving function calls until the model produces text.
for _ in range(5):
    tool_outputs = []
    for item in response.output:
        if getattr(item, "type", "") == "function_call":
            args = json.loads(item.arguments)
            result = DISPATCH[item.name](**args)
            tool_outputs.append(
                {"type": "function_call_output", "call_id": item.call_id, "output": result}
            )
    if not tool_outputs:
        break
    response = openai.responses.create(
        model=MODEL_DEPLOYMENT,
        previous_response_id=response.id,
        input=tool_outputs,
        tools=tools,
    )

print(response.output_text)


# ──────────────────────────────────────────────────────────────────────────
# 👁  PORTAL OBSERVATION
#   IMPORTANT: this lab resolves tool calls in a CLIENT-SIDE loop against a
#   direct model call - it does NOT create an agent, so there is no Agents-page
#   entry and no server-side tool-call trace. To watch tool calls server-side:
#     • Connect Application Insights (Lab 13) and inspect the function-call
#       spans in Azure Monitor / the "Tracing" page.
#     • Or attach the same functions to a real agent and use the agent
#       Playground - Lab 9 (openapi-tool) does exactly that and shows the calls
#       under the agent's "Traces" / "Show details" view. Compare the two.
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# 🏋  CHALLENGE  — Add a "get_order_history" function tool
#
#   The agent currently calls list_products / get_product / place_order.
#   Add a new Python function `get_order_status(order_id: str) -> dict`
#   that reads from data/orders.json and returns the order or an error.
#   Steps:
#     1. Define the Python function (reading the local JSON file).
#     2. Add its schema to the `tools` list in the same format as the
#        existing functions (type/function/name/description/parameters).
#     3. Add a handler case in the tool-call dispatch loop.
#     4. Test by asking: "What is the status of order ORD-0001?"
#        (place an order first so the file exists).
#   BONUS: What happens if you ask for an order that does not exist?
#          Can you make the agent give a friendly error?
# ──────────────────────────────────────────────────────────────────────────
