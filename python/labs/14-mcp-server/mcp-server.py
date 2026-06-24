"""Lab: connect a Foundry agent to your own MCP server.

This is the Azure side of the MCP lab. It registers the locally running
bakery MCP server (bakery_mcp_server.py) with a Foundry agent using the hosted
MCP tool, then asks the agent a question that requires the bakery tools.

Prereqs:
    1. Start the server in another terminal:  python bakery_mcp_server.py --http
    2. Expose it on a public HTTPS URL the Foundry service can reach
       (e.g. `devtunnel host -p 8000` or ngrok) and set BAKERY_MCP_SERVER_URL.

Offline smoke test (no Azure, no tunnel needed):
    python mcp-server.py --offline
"""
import argparse
import asyncio
import os

import bakery_mcp_server as srv
import bakery_store as store


def offline_demo():
    """Exercise the MCP tools in-process to prove they work without Azure."""
    tools = asyncio.run(srv.mcp.list_tools())
    print(f"MCP server exposes {len(tools)} tools: {[t.name for t in tools]}\n")

    print("search_products('chocolate'):")
    for p in store.search_products("chocolate"):
        print(f"  {p['product_id']}  {p['name']}  ${p['price']}")

    bread = store.list_products(category="Bread")
    print(f"\nlist_products(category='Bread') -> {len(bread)} items")

    first = bread[0]["product_id"]
    result = store.place_order(first, quantity=2, customer="workshop")
    print(f"\nplace_order({first}, 2): ok={result['ok']} total=${result['order']['total']}")

    print(f"list_orders('workshop') -> {len(store.list_orders(customer='workshop'))} order(s)")


def foundry_demo():
    from dotenv import load_dotenv
    from azure.identity import DefaultAzureCredential
    from azure.ai.projects import AIProjectClient
    from azure.ai.projects.models import PromptAgentDefinition, MCPTool

    load_dotenv()
    endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
    agent_name = os.environ.get("FOUNDRY_AGENT_NAME", "bakery-mcp-agent")
    model = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")
    server_url = os.environ["BAKERY_MCP_SERVER_URL"]  # public https URL ending in /mcp

    project = AIProjectClient(endpoint=endpoint, credential=DefaultAzureCredential())

    mcp_tool = MCPTool(
        server_label="frankies_bakery",
        server_url=server_url,
        require_approval="never",
        allowed_tools=["list_products", "search_products", "place_order", "list_orders"],
    )

    agent = project.agents.create_version(
        agent_name=agent_name,
        definition=PromptAgentDefinition(
            model=model,
            instructions=(
                "You are Frankie's Bakery assistant. Use the MCP tools to answer "
                "questions about products and to place orders. Never invent products."
            ),
            tools=[mcp_tool],
        ),
    )

    openai = project.get_openai_client()
    conversation = openai.conversations.create()
    response = openai.responses.create(
        conversation=conversation.id,
        extra_body={"agent_reference": {"name": agent.name, "type": "agent_reference"}},
        input="What chocolate items do you have, and order me two of the cheapest one.",
    )
    print(response.output_text)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--offline", action="store_true", help="Run the in-process tool demo")
    args = parser.parse_args()
    if args.offline:
        offline_demo()
    else:
        foundry_demo()


# ──────────────────────────────────────────────────────────────────────────
# 👁  PORTAL OBSERVATION (live Foundry path only)
#   After running the Foundry path (not --offline):
#     Foundry portal → Agents → <agent> → Playground.
#     Ask: "Do you have any seasonal bread?"
#     In the "Show details" panel, expand the MCP tool call.  You'll see:
#       • The MCP server URL the model sent the request to.
#       • The tool name (list_products) and arguments ({available_only:true}).
#       • The raw JSON array returned by your server.
#     This is exactly what you built — your server, Foundry's agent.
# ──────────────────────────────────────────────────────────────────────────

# ──────────────────────────────────────────────────────────────────────────
# 🏋  CHALLENGE  — Add a "get_recommendations" tool
#
#   Extend bakery_mcp_server.py with a new @mcp.tool() function:
#
#     @mcp.tool()
#     def get_recommendations(budget: float, dietary: str = "") -> list:
#         """Return up to 3 products under `budget` that match dietary tag."""
#         # TODO: filter store.list_products() by price <= budget
#         # and dietary tag in product['tags'] or product['ingredients']
#         # return the top 3 sorted by rating
#
#   Then:
#     1. Implement the body.
#     2. Add a pytest case to test_offline.py.
#     3. Add the new tool name to `allowed_tools` in mcp-server.py.
#     4. Ask the agent: "I have $10 and I'm gluten-free. Any suggestions?"
# ──────────────────────────────────────────────────────────────────────────
